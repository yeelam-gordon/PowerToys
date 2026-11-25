// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#include <Windows.h>
#include <winrt/base.h>

#include <memory>
#include <string>

#include <CppUnitTest.h>

namespace
{
    struct LaunchState
    {
        bool elevated = true;
        bool linkedElevated = false;
        bool linkedAvailable = true;
        bool executableAvailable = true;
        bool launchSucceeds = true;
        bool waited = false;
        bool waitSucceeds = true;
        bool useRealSearch = false;
        int normalLaunches = 0;
        int limitedLaunches = 0;
        HANDLE linkedToken = nullptr;
        DWORD exitCode = 37;
        std::wstring executable = L"C:\\Custom PowerShell\\pwsh.exe";
        std::wstring launchedExecutable;
        std::wstring command;
    } state;

    BOOL WINAPI OpenToken(HANDLE, DWORD, PHANDLE token)
    {
        *token = CreateEventW(nullptr, TRUE, TRUE, nullptr);
        return *token != nullptr;
    }

    BOOL WINAPI TokenInformation(HANDLE token, TOKEN_INFORMATION_CLASS information, LPVOID value, DWORD, PDWORD)
    {
        if (information == TokenElevation)
        {
            static_cast<TOKEN_ELEVATION*>(value)->TokenIsElevated = token == state.linkedToken ? state.linkedElevated : state.elevated;
            return TRUE;
        }

        if (information == TokenLinkedToken && state.linkedAvailable)
        {
            state.linkedToken = CreateEventW(nullptr, TRUE, TRUE, nullptr);
            static_cast<TOKEN_LINKED_TOKEN*>(value)->LinkedToken = state.linkedToken;
            return state.linkedToken != nullptr;
        }

        SetLastError(ERROR_NO_SUCH_LOGON_SESSION);
        return FALSE;
    }

    DWORD WINAPI FindPowerShell(LPCWSTR path, LPCWSTR name, LPCWSTR extension, DWORD length, LPWSTR result, LPWSTR* filePart)
    {
        if (state.useRealSearch)
        {
            return SearchPathW(path, name, extension, length, result, filePart);
        }

        Microsoft::VisualStudio::CppUnitTestFramework::Assert::AreEqual(L"pwsh.exe", name);
        if (!state.executableAvailable)
        {
            SetLastError(ERROR_FILE_NOT_FOUND);
            return 0;
        }

        if (!length)
        {
            return static_cast<DWORD>(state.executable.size() + 1);
        }

        wcscpy_s(result, length, state.executable.c_str());
        return static_cast<DWORD>(state.executable.size());
    }

    BOOL RecordLaunch(LPCWSTR executable, LPWSTR command, LPPROCESS_INFORMATION process)
    {
        state.launchedExecutable = executable;
        state.command = command;
        if (!state.launchSucceeds)
        {
            SetLastError(ERROR_ACCESS_DENIED);
            return FALSE;
        }

        process->hProcess = CreateEventW(nullptr, TRUE, TRUE, nullptr);
        process->hThread = CreateEventW(nullptr, TRUE, TRUE, nullptr);
        return TRUE;
    }

    BOOL WINAPI StartNormally(LPCWSTR executable, LPWSTR command, LPSECURITY_ATTRIBUTES, LPSECURITY_ATTRIBUTES, BOOL inheritHandles, DWORD, LPVOID, LPCWSTR, LPSTARTUPINFOW, LPPROCESS_INFORMATION process)
    {
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsFalse(inheritHandles != FALSE);
        ++state.normalLaunches;
        return RecordLaunch(executable, command, process);
    }

    BOOL WINAPI StartWithToken(HANDLE token, DWORD logonFlags, LPCWSTR executable, LPWSTR command, DWORD flags, LPVOID environment, LPCWSTR, LPSTARTUPINFOW, LPPROCESS_INFORMATION process)
    {
        using Microsoft::VisualStudio::CppUnitTestFramework::Assert;
        Assert::IsTrue(token == state.linkedToken);
        Assert::AreEqual<DWORD>(LOGON_WITH_PROFILE, logonFlags);
        Assert::IsTrue((flags & CREATE_UNICODE_ENVIRONMENT) != 0);
        Assert::IsNotNull(environment);
        ++state.limitedLaunches;
        return RecordLaunch(executable, command, process);
    }

    BOOL WINAPI ExitCode(HANDLE, LPDWORD code)
    {
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::IsTrue(state.waited);
        *code = state.exitCode;
        return TRUE;
    }

    DWORD WINAPI WaitForScript(HANDLE, DWORD timeout)
    {
        Microsoft::VisualStudio::CppUnitTestFramework::Assert::AreEqual<DWORD>(INFINITE, timeout);
        state.waited = true;
        if (!state.waitSucceeds)
        {
            SetLastError(ERROR_INVALID_HANDLE);
            return WAIT_FAILED;
        }

        return WAIT_OBJECT_0;
    }

    struct PathFixture
    {
        std::wstring directory;
        std::wstring originalPath;
        bool pathChanged = false;

        ~PathFixture()
        {
            if (pathChanged)
            {
                SetEnvironmentVariableW(L"PATH", originalPath.empty() ? nullptr : originalPath.c_str());
            }

            if (!directory.empty())
            {
                DeleteFileW((directory + L"\\pwsh.exe").c_str());
                RemoveDirectoryW(directory.c_str());
            }
        }
    };
}

#define OpenProcessToken OpenToken
#define GetTokenInformation TokenInformation
#define SearchPathW FindPowerShell
#define CreateProcessW StartNormally
#define CreateProcessWithTokenW StartWithToken
#define GetExitCodeProcess ExitCode
#define WaitForSingleObject WaitForScript
#include "../CmdNotFoundModuleInterface/PowerShellScriptRunner.h"
#undef OpenProcessToken
#undef GetTokenInformation
#undef SearchPathW
#undef CreateProcessW
#undef CreateProcessWithTokenW
#undef GetExitCodeProcess
#undef WaitForSingleObject

using namespace Microsoft::VisualStudio::CppUnitTestFramework;

namespace CmdNotFoundUnitTests
{
    TEST_CLASS (PowerShellScriptRunnerTests)
    {
    public:
        TEST_METHOD_INITIALIZE(Initialize)
            {
                state = {};
            }

            TEST_METHOD (ElevatedCallerUsesOwnLinkedTokenAndWaitsForExit)
            {
                Assert::AreEqual<DWORD>(37, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile -File \"script with spaces.ps1\""));
                Assert::AreEqual(1, state.limitedLaunches);
                Assert::AreEqual(0, state.normalLaunches);
                Assert::AreEqual(state.executable, state.launchedExecutable);
                Assert::AreEqual(std::wstring(L"\"C:\\Custom PowerShell\\pwsh.exe\" -NoProfile -File \"script with spaces.ps1\""), state.command);
            }

            TEST_METHOD (NonElevatedCallerPreservesOrdinaryPowerShellLaunch)
            {
                state.elevated = false;
                Assert::AreEqual<DWORD>(37, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile"));
                Assert::AreEqual(1, state.normalLaunches);
                Assert::AreEqual(0, state.limitedLaunches);
            }

            TEST_METHOD (MissingLinkedTokenDoesNotFallBackToElevatedLaunch)
            {
                state.linkedAvailable = false;
                Assert::AreEqual<DWORD>(ERROR_NO_SUCH_LOGON_SESSION, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile"));
                Assert::AreEqual(0, state.normalLaunches + state.limitedLaunches);
            }

            TEST_METHOD (ElevatedLinkedTokenDoesNotLaunch)
            {
                state.linkedElevated = true;
                Assert::AreEqual<DWORD>(ERROR_ELEVATION_REQUIRED, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile"));
                Assert::AreEqual(0, state.normalLaunches + state.limitedLaunches);
            }

            TEST_METHOD (FailedLimitedLaunchPreservesErrorWithoutFallback)
            {
                state.launchSucceeds = false;
                Assert::AreEqual<DWORD>(ERROR_ACCESS_DENIED, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile"));
                Assert::AreEqual(1, state.limitedLaunches);
                Assert::AreEqual(0, state.normalLaunches);
            }

            TEST_METHOD (MissingPowerShellDoesNotLaunch)
            {
                state.executableAvailable = false;
                Assert::AreEqual<DWORD>(ERROR_FILE_NOT_FOUND, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile"));
                Assert::AreEqual(0, state.normalLaunches + state.limitedLaunches);
            }

            TEST_METHOD (FailedWaitIsNotReportedAsScriptSuccess)
            {
                state.waitSucceeds = false;
                Assert::AreEqual<DWORD>(ERROR_INVALID_HANDLE, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile"));
            }

            TEST_METHOD (UserPathPowerShellUsesLimitedTokenWithoutRestrictingInstallLocation)
            {
                PathFixture fixture;
                const DWORD pathLength = GetEnvironmentVariableW(L"PATH", nullptr, 0);
                fixture.originalPath.resize(pathLength);
                if (pathLength)
                {
                    const DWORD written = GetEnvironmentVariableW(L"PATH", fixture.originalPath.data(), pathLength);
                    Assert::IsTrue(written < pathLength);
                    fixture.originalPath.resize(written);
                }

                wchar_t temporary[MAX_PATH]{};
                Assert::IsTrue(GetTempPathW(MAX_PATH, temporary) != 0);
                const auto directory = std::wstring(temporary) + L"PowerToys CmdNotFound " + std::to_wstring(GetCurrentProcessId()) + L" " + std::to_wstring(GetTickCount64());
                Assert::IsTrue(CreateDirectoryW(directory.c_str(), nullptr) != FALSE);
                fixture.directory = directory;
                const auto executable = directory + L"\\pwsh.exe";
                {
                    const HANDLE created = CreateFileW(executable.c_str(), GENERIC_WRITE, 0, nullptr, CREATE_NEW, FILE_ATTRIBUTE_NORMAL, nullptr);
                    Assert::IsTrue(created != INVALID_HANDLE_VALUE);
                    winrt::handle file{ created };
                }

                Assert::IsTrue(SetEnvironmentVariableW(L"PATH", directory.c_str()) != FALSE);
                fixture.pathChanged = true;
                state.useRealSearch = true;
                Assert::AreEqual<DWORD>(37, CmdNotFoundScripts::RunPowerShellScript(L"-NoProfile"));
                Assert::AreEqual(executable, state.launchedExecutable);
                Assert::AreEqual(1, state.limitedLaunches);
                Assert::AreEqual(0, state.normalLaunches);
            }
    };
}
