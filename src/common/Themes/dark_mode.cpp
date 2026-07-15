// Native Win32 dark-mode helpers built on top of the undocumented
// uxtheme.dll ordinals shipped with Windows 10 1903+ / Windows 11.
//
// Reference: https://github.com/microsoft/PowerToys/issues/31813
// Precedent: src/modules/ZoomIt/ZoomIt/Utility.cpp
#include "dark_mode.h"
#include "theme_helpers.h"

#include <mutex>

namespace
{
    enum class PreferredAppMode
    {
        Default,
        AllowDark,
        ForceDark,
        ForceLight,
        Max
    };

    using fnSetPreferredAppMode = PreferredAppMode(WINAPI*)(PreferredAppMode appMode);
    using fnShouldAppsUseDarkMode = bool(WINAPI*)();
    using fnFlushMenuThemes = void(WINAPI*)();

    fnSetPreferredAppMode pSetPreferredAppMode = nullptr;
    fnShouldAppsUseDarkMode pShouldAppsUseDarkMode = nullptr;
    fnFlushMenuThemes pFlushMenuThemes = nullptr;

    std::once_flag init_flag;
    std::once_flag dark_menu_brush_init_flag;

    // Mirrors the surface color used by ZoomIt's dark menus for visual
    // consistency across PowerToys-owned native menus.
    constexpr COLORREF DarkMenuSurfaceColor = RGB(45, 45, 45);

    class UxThemeLibrary
    {
    public:
        HMODULE Get()
        {
            if (!module)
            {
                module = GetModuleHandleW(L"uxtheme.dll");
                if (!module)
                {
                    module = LoadLibraryExW(L"uxtheme.dll", nullptr, LOAD_LIBRARY_SEARCH_SYSTEM32);
                    owns_module = module != nullptr;
                }
            }

            return module;
        }

        ~UxThemeLibrary()
        {
            if (owns_module && module)
            {
                FreeLibrary(module);
            }
        }

    private:
        HMODULE module = nullptr;
        bool owns_module = false;
    };

    class MenuBrush
    {
    public:
        void Create(COLORREF color)
        {
            brush = CreateSolidBrush(color);
        }

        HBRUSH Get() const
        {
            return brush;
        }

        ~MenuBrush()
        {
            if (brush)
            {
                DeleteObject(brush);
            }
        }

    private:
        HBRUSH brush = nullptr;
    };

    UxThemeLibrary ux_theme_library;
    MenuBrush dark_menu_brush;

    void EnsureOrdinalsLoaded()
    {
        std::call_once(init_flag, []() {
            HMODULE hUxTheme = ux_theme_library.Get();
            if (!hUxTheme)
            {
                return;
            }

            pSetPreferredAppMode = reinterpret_cast<fnSetPreferredAppMode>(
                GetProcAddress(hUxTheme, MAKEINTRESOURCEA(135)));
            pShouldAppsUseDarkMode = reinterpret_cast<fnShouldAppsUseDarkMode>(
                GetProcAddress(hUxTheme, MAKEINTRESOURCEA(132)));
            pFlushMenuThemes = reinterpret_cast<fnFlushMenuThemes>(
                GetProcAddress(hUxTheme, MAKEINTRESOURCEA(136)));
        });
    }

    HBRUSH GetDarkMenuBrush()
    {
        std::call_once(dark_menu_brush_init_flag, []() {
            dark_menu_brush.Create(DarkMenuSurfaceColor);
        });

        return dark_menu_brush.Get();
    }

    void ApplyPreferredAppMode()
    {
        if (!pSetPreferredAppMode)
        {
            return;
        }

        const bool dark = DarkMode::IsDarkModeEnabled();
        pSetPreferredAppMode(dark ? PreferredAppMode::ForceDark : PreferredAppMode::ForceLight);

        if (pFlushMenuThemes)
        {
            pFlushMenuThemes();
        }
    }
}

void DarkMode::Initialize()
{
    EnsureOrdinalsLoaded();
    ApplyPreferredAppMode();
}

void DarkMode::Refresh()
{
    Initialize();
}

bool DarkMode::IsDarkModeEnabled()
{
    if (pShouldAppsUseDarkMode)
    {
        return pShouldAppsUseDarkMode();
    }

    return ThemeHelpers::GetSystemTheme() == Theme::Dark;
}

void DarkMode::ApplyToMenu(HMENU menu)
{
    if (!menu)
    {
        return;
    }

    EnsureOrdinalsLoaded();
    if (!pSetPreferredAppMode)
    {
        return;
    }

    MENUINFO mi = { sizeof(mi) };
    mi.fMask = MIM_BACKGROUND | MIM_APPLYTOSUBMENUS;

    if (IsDarkModeEnabled())
    {
        mi.hbrBack = GetDarkMenuBrush();
    }
    else
    {
        mi.hbrBack = nullptr;
    }

    SetMenuInfo(menu, &mi);
}
