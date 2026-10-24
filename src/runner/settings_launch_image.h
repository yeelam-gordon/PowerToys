// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#pragma once

#include <Windows.h>
#include <wil/resource.h>

#include <string>

class SettingsLaunchImage
{
public:
    HRESULT Open(const std::wstring& requestedPath)
    {
        path.clear();
        file.reset(CreateFileW(requestedPath.c_str(), GENERIC_READ, FILE_SHARE_READ, nullptr, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, nullptr));
        if (!file)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }

        constexpr DWORD flags = FILE_NAME_NORMALIZED | VOLUME_NAME_DOS;
        DWORD length = GetFinalPathNameByHandleW(file.get(), nullptr, 0, flags);
        if (length == 0)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        path.resize(length);
        const DWORD written = GetFinalPathNameByHandleW(file.get(), path.data(), length, flags);
        if (written == 0)
        {
            return HRESULT_FROM_WIN32(GetLastError());
        }
        if (written >= length)
        {
            return HRESULT_FROM_WIN32(ERROR_INSUFFICIENT_BUFFER);
        }
        path.resize(written);

        // Keep the ordinary file handle until CreateProcessW returns. It prevents
        // file replacement and ancestor renames; this resolved path avoids junction aliases.
        return S_OK;
    }

    HANDLE File() const
    {
        return file.get();
    }

    // Exact extended-length DOS path from the retained handle; do not strip its \\?\ prefix.
    const std::wstring& Path() const
    {
        return path;
    }

private:
    wil::unique_hfile file;
    std::wstring path;
};
