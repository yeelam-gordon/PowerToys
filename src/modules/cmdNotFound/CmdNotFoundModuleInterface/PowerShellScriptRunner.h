// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <winrt/base.h>

#include <memory>
#include <string>

#pragma comment(lib, "Advapi32.lib")

namespace CmdNotFoundScripts
{
    inline DWORD RunPowerShellScript(const std::wstring& arguments)
    {
        winrt::handle token;
        if (!OpenProcessToken(GetCurrentProcess(), TOKEN_QUERY, token.put()))
        {
            return GetLastError();
        }

        TOKEN_ELEVATION elevation{};
        DWORD size = 0;
        if (!GetTokenInformation(token.get(), TokenElevation, &elevation, sizeof(elevation), &size))
        {
            return GetLastError();
        }

        winrt::handle limitedToken;
        if (elevation.TokenIsElevated)
        {
            // Use this account's linked token, not Explorer's possibly different account/profile.
            TOKEN_LINKED_TOKEN linked{};
            if (!GetTokenInformation(token.get(), TokenLinkedToken, &linked, sizeof(linked), &size))
            {
                return GetLastError();
            }

            limitedToken.attach(linked.LinkedToken);
            if (!GetTokenInformation(limitedToken.get(), TokenElevation, &elevation, sizeof(elevation), &size))
            {
                return GetLastError();
            }

            if (elevation.TokenIsElevated)
            {
                return ERROR_ELEVATION_REQUIRED;
            }
        }

        const DWORD length = SearchPathW(nullptr, L"pwsh.exe", nullptr, 0, nullptr, nullptr);
        if (!length)
        {
            return GetLastError();
        }

        std::wstring executable(length, L'\0');
        const DWORD written = SearchPathW(nullptr, L"pwsh.exe", nullptr, length, executable.data(), nullptr);
        if (!written)
        {
            return GetLastError();
        }

        if (written >= length)
        {
            return ERROR_INSUFFICIENT_BUFFER;
        }

        executable.resize(written);
        std::wstring command = L"\"" + executable + L"\" " + arguments;
        STARTUPINFOW startup{ sizeof(startup) };
        PROCESS_INFORMATION process{};
        BOOL started = FALSE;
        DWORD launchError = ERROR_SUCCESS;
        if (limitedToken)
        {
            std::unique_ptr<wchar_t, decltype(&FreeEnvironmentStringsW)> environment(GetEnvironmentStringsW(), FreeEnvironmentStringsW);
            if (!environment)
            {
                return GetLastError();
            }

            started = CreateProcessWithTokenW(limitedToken.get(),
                                              LOGON_WITH_PROFILE,
                                              executable.c_str(),
                                              command.data(),
                                              CREATE_NO_WINDOW | CREATE_UNICODE_ENVIRONMENT,
                                              environment.get(),
                                              nullptr,
                                              &startup,
                                              &process);
            launchError = started ? ERROR_SUCCESS : GetLastError();
        }
        else
        {
            started = CreateProcessW(executable.c_str(),
                                     command.data(),
                                     nullptr,
                                     nullptr,
                                     FALSE,
                                     CREATE_NO_WINDOW,
                                     nullptr,
                                     nullptr,
                                     &startup,
                                     &process);
            launchError = started ? ERROR_SUCCESS : GetLastError();
        }

        if (!started)
        {
            return launchError;
        }

        winrt::handle processHandle{ process.hProcess };
        winrt::handle threadHandle{ process.hThread };
        if (WaitForSingleObject(processHandle.get(), INFINITE) != WAIT_OBJECT_0)
        {
            return GetLastError();
        }

        DWORD exitCode = 0;
        if (!GetExitCodeProcess(processHandle.get(), &exitCode))
        {
            return GetLastError();
        }

        return exitCode;
    }
}
