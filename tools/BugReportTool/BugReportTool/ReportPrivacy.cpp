// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include "ReportPrivacy.h"

#include <algorithm>
#include <array>
#include <cstdio>
#include <cstring>
#include <cwchar>
#include <exception>
#include <fstream>
#include <optional>
#include <regex>
#include <string_view>

namespace
{
    constexpr std::string_view securityKeyName = "\"SecurityKey\"";
    constexpr std::string_view privateValue = "\"<private_data>\"";
    constexpr std::size_t readBufferSize = 64 * 1024;

    std::optional<std::uintmax_t> FindFirstSecurityKey(std::ifstream& input)
    {
        std::array<char, readBufferSize + securityKeyName.size()> buffer;
        std::size_t retained = 0;
        std::uintmax_t offset = 0;
        do
        {
            input.read(buffer.data() + retained, readBufferSize);
            const auto count = retained + static_cast<std::size_t>(input.gcount());
            const auto match = std::string_view(buffer.data(), count).find(securityKeyName);
            if (match != std::string_view::npos)
            {
                return offset + match;
            }
            if (input.fail() && !input.eof())
            {
                throw std::ios::failure("Failed to scan report file");
            }
            retained = std::min(count, securityKeyName.size() - 1);
            offset += count - retained;
            std::memmove(buffer.data(), buffer.data() + count - retained, retained);
        } while (!input.eof());

        return std::nullopt;
    }

    void RedactLargeReportFile(const std::filesystem::path& file, std::uintmax_t keyOffset)
    {
        // Keep the diagnostic prefix, but never load or run regex over the large remainder.
        std::filesystem::resize_file(file, keyOffset);
        std::ofstream output;
        output.exceptions(std::ios::failbit | std::ios::badbit);
        output.open(file, std::ios::binary | std::ios::app);
        output << securityKeyName << ':' << privateValue;
        output.close();
    }

    void RedactReportFile(const std::filesystem::directory_entry& entry)
    {
        std::ifstream input;
        input.exceptions(std::ios::failbit | std::ios::badbit);
        input.open(entry.path(), std::ios::binary);
        input.exceptions(std::ios::badbit);
        const auto keyOffset = FindFirstSecurityKey(input);
        if (!keyOffset)
        {
            return;
        }

        if (entry.file_size() > ReportPrivacy::MaxRegexFileSize)
        {
            input.close();
            RedactLargeReportFile(entry.path(), *keyOffset);
            return;
        }

        input.clear();
        input.seekg(0);
        if (!input)
        {
            throw std::ios::failure("Failed to rewind report file");
        }
        std::string text;
        text.reserve(static_cast<std::size_t>(std::min<std::uintmax_t>(entry.file_size(), ReportPrivacy::MaxRegexFileSize + 1)));
        std::array<char, readBufferSize> buffer;
        while (input && text.size() <= ReportPrivacy::MaxRegexFileSize)
        {
            const auto count = std::min(buffer.size(), ReportPrivacy::MaxRegexFileSize + 1 - text.size());
            input.read(buffer.data(), static_cast<std::streamsize>(count));
            text.append(buffer.data(), static_cast<std::size_t>(input.gcount()));
        }
        if (input.fail() && !input.eof())
        {
            throw std::ios::failure("Failed to read report file");
        }
        input.close();

        if (text.size() > ReportPrivacy::MaxRegexFileSize)
        {
            RedactLargeReportFile(entry.path(), *keyOffset);
        }
        else if (ReportPrivacy::RedactSecurityKeys(text))
        {
            std::ofstream output;
            output.exceptions(std::ios::failbit | std::ios::badbit);
            output.open(entry.path(), std::ios::binary | std::ios::trunc);
            output << text;
            output.close();
        }
    }

    void RemoveFailedReportPath(
        const std::filesystem::path& path,
        const std::filesystem::path& reportDirectory,
        const std::exception& failure,
        std::vector<std::filesystem::path>& removedPaths)
    {
        wprintf_s(L"Failed to sanitize report path %ls: %hs\n", path.lexically_relative(reportDirectory).c_str(), failure.what());
        std::error_code error;
        auto removed = std::filesystem::remove(path, error);
        if (error)
        {
            removed = std::filesystem::remove_all(path) != 0;
        }
        if (removed)
        {
            removedPaths.push_back(path);
        }
    }

    void RedactReportDirectory(
        const std::filesystem::path& directory,
        const std::filesystem::path& reportDirectory,
        std::vector<std::filesystem::path>& removedPaths)
    {
        std::vector<std::filesystem::directory_entry> entries;
        try
        {
            for (const auto& entry : std::filesystem::directory_iterator(directory))
            {
                entries.push_back(entry);
            }
        }
        catch (const std::filesystem::filesystem_error& error)
        {
            if (directory == reportDirectory)
            {
                throw;
            }
            RemoveFailedReportPath(directory, reportDirectory, error, removedPaths);
            return;
        }

        for (const auto& entry : entries)
        {
            bool isDirectory;
            try
            {
                isDirectory = entry.is_directory();
            }
            catch (const std::filesystem::filesystem_error& error)
            {
                RemoveFailedReportPath(entry.path(), reportDirectory, error, removedPaths);
                continue;
            }
            if (isDirectory)
            {
                RedactReportDirectory(entry.path(), reportDirectory, removedPaths);
                continue;
            }

            const auto extension = entry.path().extension();
            if (_wcsicmp(extension.c_str(), L".json") != 0 && _wcsicmp(extension.c_str(), L".log") != 0 && _wcsicmp(extension.c_str(), L".txt") != 0)
            {
                continue;
            }
            try
            {
                if (entry.is_regular_file())
                {
                    RedactReportFile(entry);
                }
            }
            catch (const std::exception& error)
            {
                RemoveFailedReportPath(entry.path(), reportDirectory, error, removedPaths);
            }
        }
    }
}

bool ReportPrivacy::RedactSecurityKeys(std::string& text)
{
    auto offset = text.find(securityKeyName);
    if (offset == std::string::npos)
    {
        return false;
    }
    if (text.size() > MaxRegexFileSize)
    {
        text.replace(offset, std::string::npos, std::string(securityKeyName) + ':' + std::string(privateValue));
        return true;
    }

    static const std::regex keyProperty(R"("SecurityKey"\s*:\s*)");
    static const std::regex keyValue(R"re("(?:[^"\\]|\\.)*"|\{\s*"value"\s*:\s*"(?:[^"\\]|\\.)*"\s*\})re");
    bool changed = false;
    do
    {
        std::smatch property;
        if (!std::regex_search(text.cbegin() + offset, text.cend(), property, keyProperty, std::regex_constants::match_continuous))
        {
            offset += securityKeyName.size();
            continue;
        }
        const auto valueOffset = offset + property.length();
        std::smatch value;
        const auto recognized = std::regex_search(text.cbegin() + valueOffset, text.cend(), value, keyValue, std::regex_constants::match_continuous);
        const auto valueLength = recognized ? static_cast<std::size_t>(value.length()) : text.size() - valueOffset;
        if (text.compare(valueOffset, valueLength, privateValue) != 0)
        {
            text.replace(valueOffset, valueLength, privateValue);
            changed = true;
        }
        if (!recognized)
        {
            break;
        }
        offset = valueOffset + privateValue.size();
    } while ((offset = text.find(securityKeyName, offset)) != std::string::npos);

    return changed;
}

std::vector<std::filesystem::path> ReportPrivacy::RedactReportSecurityKeysAndRemoveFailedPaths(const std::filesystem::path& reportDirectory)
{
    std::vector<std::filesystem::path> removedPaths;
    RedactReportDirectory(reportDirectory, reportDirectory, removedPaths);
    return removedPaths;
}
