// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <cwchar>
#include <filesystem>
#include <fstream>
#include <iterator>
#include <regex>
#include <stdexcept>
#include <string>

namespace ReportPrivacy
{
    inline std::string RedactSecurityKeys(const std::string& text)
    {
        // Settings use a value wrapper; older reports can also contain a direct string.
        static const std::regex keyValue(R"re(("SecurityKey"\s*:\s*)(?:"(?:[^"\\]|\\.)*"|\{\s*"value"\s*:\s*"(?:[^"\\]|\\.)*"\s*\}))re");
        static const std::regex keyName(R"("SecurityKey"\s*:)");
        auto redacted = std::regex_replace(text, keyValue, "$1\"<private_data>\"");

        if (std::regex_search(std::regex_replace(redacted, keyValue, ""), keyName))
        {
            throw std::runtime_error("Unable to redact a SecurityKey property");
        }

        return redacted;
    }

    inline void RedactReportSecurityKeys(const std::filesystem::path& reportDirectory)
    {
        for (const auto& entry : std::filesystem::recursive_directory_iterator(reportDirectory))
        {
            const auto extension = entry.path().extension();
            if (!entry.is_regular_file() || (_wcsicmp(extension.c_str(), L".json") != 0 && _wcsicmp(extension.c_str(), L".log") != 0 && _wcsicmp(extension.c_str(), L".txt") != 0))
            {
                continue;
            }

            std::ifstream input;
            input.exceptions(std::ios::failbit | std::ios::badbit);
            input.open(entry.path(), std::ios::binary);
            const std::string text{ std::istreambuf_iterator<char>{ input }, std::istreambuf_iterator<char>{} };
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
    }
}
