// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#include "pch.h"
#include <array>

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace
{
    struct deletion_call
    {
        HKEY root;
        std::wstring path;
    };

    thread_local std::array<LSTATUS, 4> deletion_results{};
    thread_local std::vector<deletion_call> deletion_calls;

    LSTATUS WINAPI test_reg_delete_tree(HKEY root, LPCWSTR path)
    {
        const auto index = deletion_calls.size();
        deletion_calls.push_back({ root, path });
        return deletion_results.at(index);
    }

    // Substitute only in this translation unit, after the Windows/PCH declarations.
#define RegDeleteTreeW test_reg_delete_tree
#include <clean_video_conference.h>
#undef RegDeleteTreeW

    void verify_cleanup(const std::array<LSTATUS, 4>& results, LSTATUS expected)
    {
        deletion_results = results;
        deletion_calls.clear();
        Assert::AreEqual(expected, clean_video_conference());
        Assert::AreEqual(size_t{ 4 }, deletion_calls.size());

        const std::array<HKEY, 4> roots{ HKEY_CLASSES_ROOT, HKEY_CLASSES_ROOT, HKEY_LOCAL_MACHINE, HKEY_LOCAL_MACHINE };
        const std::array<const wchar_t*, 4> paths{
            L"CLSID\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A764}",
            L"CLSID\\{860BB310-5D01-11D0-BD3B-00A0C911CE86}\\Instance\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A764}",
            L"Software\\WOW6432Node\\Classes\\CLSID\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A732}",
            L"Software\\WOW6432Node\\Classes\\CLSID\\{860BB310-5D01-11D0-BD3B-00A0C911CE86}\\Instance\\{31AD75E9-8C3A-49C8-B9ED-5880D6B4A732}"
        };
        for (size_t index = 0; index < roots.size(); ++index)
        {
            Assert::IsTrue(roots[index] == deletion_calls[index].root);
            Assert::AreEqual(std::wstring{ paths[index] }, deletion_calls[index].path);
        }
    }
}

namespace UnitTestsCommonUtils
{
    TEST_CLASS(CleanVideoConferenceTests)
    {
    public:
        TEST_METHOD(AllSuccess)
        {
            verify_cleanup({ ERROR_SUCCESS, ERROR_SUCCESS, ERROR_SUCCESS, ERROR_SUCCESS }, ERROR_SUCCESS);
        }

        TEST_METHOD(AllAbsent)
        {
            verify_cleanup({ ERROR_FILE_NOT_FOUND, ERROR_FILE_NOT_FOUND, ERROR_FILE_NOT_FOUND, ERROR_FILE_NOT_FOUND }, ERROR_SUCCESS);
            verify_cleanup({ ERROR_PATH_NOT_FOUND, ERROR_PATH_NOT_FOUND, ERROR_PATH_NOT_FOUND, ERROR_PATH_NOT_FOUND }, ERROR_SUCCESS);
        }

        TEST_METHOD(MixedSuccessAndAbsence)
        {
            verify_cleanup({ ERROR_SUCCESS, ERROR_FILE_NOT_FOUND, ERROR_PATH_NOT_FOUND, ERROR_SUCCESS }, ERROR_SUCCESS);
        }

        TEST_METHOD(FailureAtEachPositionStillAttemptsEveryTarget)
        {
            for (size_t index = 0; index < deletion_results.size(); ++index)
            {
                std::array<LSTATUS, 4> results{};
                results[index] = ERROR_ACCESS_DENIED;
                verify_cleanup(results, ERROR_ACCESS_DENIED);
            }
        }

        TEST_METHOD(FirstUnexpectedErrorIsRetained)
        {
            verify_cleanup({ ERROR_FILE_NOT_FOUND, ERROR_ACCESS_DENIED, ERROR_INVALID_HANDLE, ERROR_PATH_NOT_FOUND }, ERROR_ACCESS_DENIED);
            verify_cleanup({ ERROR_INVALID_HANDLE, ERROR_ACCESS_DENIED, ERROR_PATH_NOT_FOUND, ERROR_SUCCESS }, ERROR_INVALID_HANDLE);
        }

        TEST_METHOD(FailedCleanupCanBeRetried)
        {
            verify_cleanup({ ERROR_SUCCESS, ERROR_ACCESS_DENIED, ERROR_SUCCESS, ERROR_SUCCESS }, ERROR_ACCESS_DENIED);
            verify_cleanup({ ERROR_FILE_NOT_FOUND, ERROR_SUCCESS, ERROR_PATH_NOT_FOUND, ERROR_FILE_NOT_FOUND }, ERROR_SUCCESS);
        }
    };
}
