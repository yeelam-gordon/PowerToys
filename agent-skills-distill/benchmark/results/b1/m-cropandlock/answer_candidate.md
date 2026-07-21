# Bug Localization Answer: CropAndLock

**CULPRIT_FILES:**
1. `src/modules/CropAndLock/CropAndLock/main.cpp` (primary)

**CULPRIT_FUNCTIONS:**
- `ProcessCommand()` lambda `windowCroppedCallback` (lines ~148-175)
- Missing: `handleTheme()` function and `theme_listener` integration

**FIX:**
Cropped windows are created without applying the system dark/light theme to their title bars. Need to add a `theme_listener` for system theme changes and a `handleTheme()` function that calls `ThemeHelpers::SetImmersiveDarkMode(window->Handle(), isDark)` on every newly created cropped window and whenever the system theme changes.

**CITED_FIX_PR:**
#38044

**CONFIDENCE:**
high

**USED_SKILL:**
The skill's regression entry "Theme: white/wrong cropped-window title bar" matched the terse symptom "theme", pointing to the need for `SetImmersiveDarkMode` application on window creation and theme change events via `handleTheme()` and `theme_listener`.
