// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <Windows.h>
#include <CppUnitTest.h>
#include <random>

#include "..\BugReportTool\ReportPrivacy.h"

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace BugReportToolUnitTests
{
    TEST_CLASS (ReportPrivacyTests)
    {
    public:
        TEST_METHOD (RedactsCurrentAndLegacySettingsValues)
        {
            AssertTextEquals(
                std::string(R"({"properties":{"SecurityKey":"<private_data>","UseService":{"value":true}}})"),
                ReportPrivacy::RedactSecurityKeys(R"({"properties":{"SecurityKey":{"value":"current-key"},"UseService":{"value":true}}})"));
            AssertTextEquals(
                std::string(R"({"SecurityKey":"<private_data>"})"),
                ReportPrivacy::RedactSecurityKeys(R"({"SecurityKey":"legacy-key"})"));
        }

        TEST_METHOD (RedactsHistoricalLogsAndPreservesOtherDiagnostics)
        {
            const std::string prefix = "[2026-11-02 20:30:43.709546] [p-7480] [t-39400] [info] dispatch_received_json: ";
            const std::string suffix = "\r\n[info] another diagnostic\r\n";
            AssertTextEquals(
                prefix + R"({"powertoys":{"MouseWithoutBorders":{"properties":{"SecurityKey":"<private_data>"}}}})" + suffix,
                ReportPrivacy::RedactSecurityKeys(prefix + R"({"powertoys":{"MouseWithoutBorders":{"properties":{"SecurityKey":{"value":"historical-key"}}}}})" + suffix));
        }

        TEST_METHOD (HandlesWhitespaceEscapesEmptyAndRepeatedKeys)
        {
            AssertTextEquals(
                std::string("{\"SecurityKey\" : \"<private_data>\",\"other\":{\"SecurityKey\":\"<private_data>\"}}"),
                ReportPrivacy::RedactSecurityKeys("{\"SecurityKey\" : {\r\n\"value\" : \"escaped\\\"key\\\\value\"},\"other\":{\"SecurityKey\":\"\"}}"));
        }

        TEST_METHOD (RedactsEscapedValuesInsideNestedObjectsAndArrays)
        {
            AssertTextEquals(
                R"({"entries":[{"properties":{"SecurityKey":"<private_data>"}},{"SecurityKey":"<private_data>"}],"diagnostic":{"SecurityKeyState":"changed"}})",
                ReportPrivacy::RedactSecurityKeys(R"({"entries":[{"properties":{"SecurityKey":{"value":"quote\"backslash\\line\nunicode\u263a"}}},{"SecurityKey":"array-fixture"}],"diagnostic":{"SecurityKeyState":"changed"}})"));
        }

        TEST_METHOD (LeavesAlreadyRedactedAndUnrelatedContentUnchanged)
        {
            const std::string text = R"({"SecurityKey":"<private_data>","Name":"test","Enabled":true})";
            AssertTextEquals(text, ReportPrivacy::RedactSecurityKeys(text));
            const std::string diagnostic = "[info] SecurityKey was changed\r\n";
            AssertTextEquals(diagnostic, ReportPrivacy::RedactSecurityKeys(diagnostic));
        }

        TEST_METHOD (RedactsRemainderForUnrecognizedOrTruncatedValues)
        {
            for (const auto& text : {
                     R"({"SecurityKey":{"unexpected":"unrecognized-key"}})",
                     R"({"SecurityKey":{"value":"truncated-key)",
                     R"({"SecurityKey":{"value":{"nested":"nested-fixture"}}})",
                     R"({"SecurityKey":["array-fixture"]})",
                     R"({"SecurityKey":null})" })
            {
                AssertTextEquals(R"({"SecurityKey":"<private_data>")", ReportPrivacy::RedactSecurityKeys(text));
            }
        }

        TEST_METHOD (RedactsRemainderForAdditionalWrapperContent)
        {
            AssertTextEquals(
                R"({"SecurityKey":"<private_data>")",
                ReportPrivacy::RedactSecurityKeys(R"({"SecurityKey":{"value":"<private_data>","other":"sibling-fixture"}})"));
            AssertTextEquals(
                R"([info] dispatch_received_json: {"SecurityKey":"<private_data>")",
                ReportPrivacy::RedactSecurityKeys(R"([info] dispatch_received_json: {"SecurityKey":{"value":"first-fixture","other":{"nested":"sibling-fixture"}}})"));
        }

        TEST_METHOD (RedactsKnownValuesBeforeDiscardingUnrecognizedRemainder)
        {
            AssertTextEquals(
                R"({"diagnostic":"retained","entries":[{"SecurityKey":"<private_data>"},{"SecurityKey":"<private_data>"},{"SecurityKey":"<private_data>"},{"SecurityKey":"<private_data>")",
                ReportPrivacy::RedactSecurityKeys(
                    R"({"diagnostic":"retained","entries":[{"SecurityKey":"first"},{"SecurityKey":{"value":"second"}},{"SecurityKey":"third"},{"SecurityKey":["fourth"]},{"SecurityKey":{"unexpected":"fifth"}}]})"));
        }

        TEST_METHOD (RedactsCopiedBackupsAndLogsWithoutChangingSourceSettings)
        {
            TemporaryDirectory temporary;
            const auto source = temporary.path / L"source" / L"settings.json";
            const auto report = temporary.path / L"report";
            const auto backup = report / L"MouseWithoutBorders.backup" / L"settings.json";
            const auto log = report / L"RunnerLogs" / L"old" / L"runner-log.log";
            const std::string original = R"({"properties":{"SecurityKey":{"value":"fixture-key"}}})";
            Write(source, original);
            Write(backup, original);
            Write(log, "[info] dispatch_received_json: " + original);
            Write(report / L"unrelated.txt", "diagnostic\r\n");

            Assert::IsTrue(ReportPrivacy::RedactReportSecurityKeysAndGetExcludedFiles(report).empty());

            AssertTextEquals(original, Read(source));
            AssertTextEquals(std::string(R"({"properties":{"SecurityKey":"<private_data>"}})"), Read(backup));
            Assert::IsTrue(Read(log).find("fixture-key") == std::string::npos);
            AssertTextEquals(std::string("diagnostic\r\n"), Read(report / L"unrelated.txt"));
        }

        TEST_METHOD (RedactsCollectedFilesRegardlessOfExtensionCase)
        {
            TemporaryDirectory temporary;
            for (const auto& extension : { L".JSON", L".Log", L".tXt" })
            {
                Write(temporary.path / (std::wstring(L"settings") + extension), R"({"SecurityKey":"fixture-key"})");
            }

            Assert::IsTrue(ReportPrivacy::RedactReportSecurityKeysAndGetExcludedFiles(temporary.path).empty());

            for (const auto& extension : { L".JSON", L".Log", L".tXt" })
            {
                AssertTextEquals(
                    R"({"SecurityKey":"<private_data>"})",
                    Read(temporary.path / (std::wstring(L"settings") + extension)));
            }
        }

        TEST_METHOD (ExcludesFilesWithReadOrWriteFailuresAndContinues)
        {
            TemporaryDirectory temporary;
            const auto file = temporary.path / L"settings.json";
            Write(file, R"({"SecurityKey":"fixture-key"})");
            for (const DWORD sharing : { DWORD{ 0 }, DWORD{ FILE_SHARE_READ } })
            {
                Write(temporary.path / L"before.json", R"({"SecurityKey":"before-fixture"})");
                Write(temporary.path / L"after.log", R"({"SecurityKey":"after-fixture"})");
                const auto handle = CreateFileW(file.c_str(), GENERIC_READ, sharing, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr);
                Assert::IsTrue(handle != INVALID_HANDLE_VALUE);
                try
                {
                    const auto excludedFiles = ReportPrivacy::RedactReportSecurityKeysAndGetExcludedFiles(temporary.path);
                    Assert::AreEqual(size_t{ 1 }, excludedFiles.size());
                    Assert::IsTrue(excludedFiles.front() == file);
                    AssertTextEquals(R"({"SecurityKey":"<private_data>"})", Read(temporary.path / L"before.json"));
                    AssertTextEquals(R"({"SecurityKey":"<private_data>"})", Read(temporary.path / L"after.log"));
                }
                catch (...)
                {
                    CloseHandle(handle);
                    throw;
                }
                CloseHandle(handle);
                AssertTextEquals(R"({"SecurityKey":"fixture-key"})", Read(file));
            }
        }

        TEST_METHOD (TemporaryDirectoryPreservesExistingDirectories)
        {
            TemporaryDirectory existing;
            const auto retained = existing.path / L"retained.txt";
            Write(retained, "retained fixture");
            std::filesystem::path created;
            {
                TemporaryDirectory temporary(existing.path);
                created = temporary.path;
            }

            Assert::IsTrue(std::filesystem::exists(retained), L"An existing fixture directory was removed.");
            Assert::IsTrue(created != existing.path, L"An existing fixture directory was reused.");
            AssertTextEquals("retained fixture", Read(retained));
        }

    private:
        static void AssertTextEquals(const std::string& expected, const std::string& actual)
        {
            Assert::IsTrue(expected == actual, L"File content comparison failed.");
        }

        struct TemporaryDirectory
        {
            static std::filesystem::path GenerateCandidatePath()
            {
                return std::filesystem::temp_directory_path() / ("BugReportPrivacyTests-" + std::to_string(std::random_device{}()));
            }

            std::filesystem::path path;

            explicit TemporaryDirectory(const std::filesystem::path& candidatePath = GenerateCandidatePath()) :
                path(candidatePath)
            {
                while (!std::filesystem::create_directory(path))
                {
                    path = GenerateCandidatePath();
                }
            }

            ~TemporaryDirectory()
            {
                std::error_code error;
                std::filesystem::remove_all(path, error);
            }
        };

        static void Write(const std::filesystem::path& file, const std::string& text)
        {
            std::filesystem::create_directories(file.parent_path());
            std::ofstream output(file, std::ios::binary);
            output.exceptions(std::ios::failbit | std::ios::badbit);
            output << text;
        }

        static std::string Read(const std::filesystem::path& file)
        {
            std::ifstream input(file, std::ios::binary);
            input.exceptions(std::ios::failbit | std::ios::badbit);
            return { std::istreambuf_iterator<char>{ input }, std::istreambuf_iterator<char>{} };
        }
    };
}
