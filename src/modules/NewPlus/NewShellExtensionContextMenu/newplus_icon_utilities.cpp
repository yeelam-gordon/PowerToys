#include "pch.h"
// pch.h first
#include "newplus_icon_utilities.h"
#include <mutex>
#include <unordered_map>

#pragma comment(lib, "Shlwapi.lib")

namespace newplus::icon_utilities
{

HICON load_icon_from_resource_spec(std::wstring icon_resource, const int icon_x, const int icon_y)
{
    if (icon_resource.empty())
    {
        return nullptr;
    }

    WCHAR expanded_icon_resource[MAX_PATH] = { 0 };
    const DWORD expanded_length = ExpandEnvironmentStringsW(icon_resource.c_str(), expanded_icon_resource, ARRAYSIZE(expanded_icon_resource));
    if (expanded_length > 0 && expanded_length <= ARRAYSIZE(expanded_icon_resource))
    {
        icon_resource = expanded_icon_resource;
    }

    const int icon_index = PathParseIconLocationW(icon_resource.data());

    HICON large_icon = nullptr;
    HICON small_icon = nullptr;
    if (SUCCEEDED(SHDefExtractIconW(icon_resource.c_str(), icon_index, 0, &large_icon, &small_icon, MAKELONG(icon_x, icon_y))))
    {
        if (small_icon)
        {
            if (large_icon)
            {
                DestroyIcon(large_icon);
            }

            return small_icon;
        }

        if (large_icon)
        {
            return large_icon;
        }
    }

    return static_cast<HICON>(LoadImageW(nullptr, icon_resource.c_str(), IMAGE_ICON, icon_x, icon_y, LR_LOADFROMFILE));
}

std::wstring get_explorer_icon(std::filesystem::path path, bool is_directory)
{
    // Cache by file extension — directories are excluded because their icon can
    // change via desktop.ini without a DLL reload.
    if (!is_directory)
    {
        static std::unordered_map<std::wstring, std::wstring> s_icon_cache;
        static std::mutex s_icon_cache_mutex;
        std::lock_guard lock(s_icon_cache_mutex);
        const std::wstring key = path.extension().wstring();
        const auto it = s_icon_cache.find(key);
        if (it != s_icon_cache.end())
            return it->second;

        SHFILEINFO shell_file_info = { 0 };
        const std::wstring filepath = path.wstring();
        SHGetFileInfo(filepath.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICONLOCATION);
        const std::wstring icon_path = shell_file_info.szDisplayName;
        if (!icon_path.empty())
        {
            std::wstring icon_resource = icon_path + L"," + std::to_wstring(shell_file_info.iIcon);
            s_icon_cache[key] = icon_resource;
            return icon_resource;
        }

        WCHAR icon_resource_specifier[MAX_PATH] = { 0 };
        DWORD buffer_length = MAX_PATH;
        AssocQueryString(ASSOCF_INIT_IGNOREUNKNOWN, ASSOCSTR_DEFAULTICON,
                         key.c_str(), NULL, icon_resource_specifier, &buffer_length);
        std::wstring icon_resource = icon_resource_specifier;
        s_icon_cache[key] = icon_resource;
        return icon_resource;
    }

    // Directories: always read fresh from the shell
    SHFILEINFO shell_file_info = { 0 };
    const std::wstring filepath = path.wstring();
    SHGetFileInfo(filepath.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICONLOCATION);
    const std::wstring icon_path = shell_file_info.szDisplayName;
    if (!icon_path.empty())
    {
        return icon_path + L"," + std::to_wstring(shell_file_info.iIcon);
    }

    WCHAR icon_resource_specifier[MAX_PATH] = { 0 };
    DWORD buffer_length = MAX_PATH;
    AssocQueryString(ASSOCF_INIT_IGNOREUNKNOWN, ASSOCSTR_DEFAULTICON,
                     L"", NULL, icon_resource_specifier, &buffer_length);
    return icon_resource_specifier;
}

HICON get_explorer_icon_handle(std::filesystem::path path)
{
    SHFILEINFO shell_file_info = { 0 };
    const std::wstring filepath = path.wstring();
    SHGetFileInfo(filepath.c_str(), 0, &shell_file_info, sizeof(shell_file_info), SHGFI_ICON);
    if (shell_file_info.hIcon)
    {
        return shell_file_info.hIcon;
    }

    WCHAR icon_resource_specifier[MAX_PATH] = { 0 };
    DWORD buffer_length = MAX_PATH;
    const std::wstring extension = path.extension().wstring();
    AssocQueryString(ASSOCF_INIT_IGNOREUNKNOWN, ASSOCSTR_DEFAULTICON,
                     extension.c_str(), NULL, icon_resource_specifier, &buffer_length);
    const std::wstring icon_resource = icon_resource_specifier;
    const auto icon_x = GetSystemMetrics(SM_CXSMICON);
    const auto icon_y = GetSystemMetrics(SM_CYSMICON);
    return load_icon_from_resource_spec(icon_resource, icon_x, icon_y);
}

}
