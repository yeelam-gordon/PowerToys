// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cwchar>
#include <exception>
#include <filesystem>
#include <fstream>
#include <regex>
#include <string>
#include <vector>

namespace ReportPrivacy
{
    inline std::string RedactSecurityKeys(const std::string& text)
    {
        // Settings use a value wrapper; older reports can also contain a direct string.
        static const std::regex keyValue(R"re(("SecurityKey"\s*:\s*)(?:"(?:[^"\\]|\\.)*"|\{\s*"value"\s*:\s*"(?:[^"\\]|\\.)*"\s*\}))re");
        static const std::regex keyName(R"("SecurityKey"\s*:\s*)");
        static const std::string privateValue = "\"<private_data>\"";
        auto redacted = std::regex_replace(text, keyValue, "$1" + privateValue);

        for (auto property = std::sregex_iterator(redacted.begin(), redacted.end(), keyName); property != std::sregex_iterator(); ++property)
        {
            const auto valueOffset = property->position() + property->length();
            if (redacted.compare(valueOffset, privateValue.size(), privateValue) != 0)
            {
                // An unknown value has no reliable end; discard the rest of this staging copy.
                redacted.replace(valueOffset, std::string::npos, privateValue);
                break;
            }
        }

        return redacted;
    }

    inline std::vector<std::filesystem::path> RedactReportSecurityKeysAndRemoveFailedFiles(const std::filesystem::path& reportDirectory)
    {
        std::vector<std::filesystem::path> removedFiles;
        for (const auto& entry : std::filesystem::recursive_directory_iterator(reportDirectory))
        {
            const auto extension = entry.path().extension();
            if (_wcsicmp(extension.c_str(), L".json") != 0 && _wcsicmp(extension.c_str(), L".log") != 0 && _wcsicmp(extension.c_str(), L".txt") != 0)
            {
                continue;
            }

            try
            {
                if (!entry.is_regular_file())
                {
                    continue;
                }

                std::ifstream input;
                input.exceptions(std::ios::failbit | std::ios::badbit);
                input.open(entry.path(), std::ios::binary);
                std::string text(static_cast<std::size_t>(entry.file_size()), '\0');
                input.read(text.data(), static_cast<std::streamsize>(text.size()));
                input.close();
                const auto redacted = RedactSecurityKeys(text);
                if (redacted != text)
                {
                    std::ofstream output;
                    output.exceptions(std::ios::failbit | std::ios::badbit);
                    output.open(entry.path(), std::ios::binary | std::ios::trunc);
                    output << redacted;
                    output.close();
                }
            }
            catch (const std::exception&)
            {
                std::filesystem::remove(entry.path());
                removedFiles.push_back(entry.path());
            }
        }

        return removedFiles;
    }
}
