# CropAndLock Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / inputs (target app, window state):**
- **Mode: Reparent / Thumbnail / Screenshot?**
- **OS / build / monitor layout (count, DPI, negative-origin?):**
- **Theme (dark/light):**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Original window wrong position/size after closing crop | `ReparentCropAndLockWindow.cpp::RestoreOriginalState`/`SaveOriginalState` | Reparent restore | Reparent restore/offset |
| First drag of cropped window fails / can't move | `ReparentCropAndLockWindow.cpp` `MessageHandler`, restore state | Reparent | Reparent restore/offset |
| Selection wrong/off-screen with multiple monitors | `OverlayWindow.cpp::SetupOverlay`; `DisplaysUtil.h::ComputeAllDisplaysUnion` | Multi-monitor coords | Multi-monitor |
| Black / partial capture (browser, GPU, ZoomIt) | `ScreenshotCropAndLockWindow.cpp::CropAndLock` (`PrintWindow`) | GPU/protected capture | Screenshot black image |
| White border around cropped content | `ScreenshotCropAndLockWindow.cpp` / mode choice | GPU capture edge | Screenshot black image |
| Cropped window "stopped updating" | `main.cpp::ProcessCommand` (which mode?); Screenshot is frozen by design | Mode expectation | Doesn't update |
| Fullscreen target doesn't refresh (thumbnail) | `ThumbnailCropAndLockWindow.cpp` DWM clone | DWM live-clone limit | Doesn't update |
| Title bar white / ignores dark theme | `main.cpp::handleTheme` → `SetImmersiveDarkMode` | Theming | Theme title bar |
| Hotkey does nothing (incl. Hyper-V/VM window) | `dllmain.cpp::on_hotkey`/`get_hotkeys`; conflict detection | Hotkey activation | Hotkey activation |
| Wrong mode activates for a shortcut | `dllmain.cpp` `get_hotkeys`/`on_hotkey` index mismatch | Hotkey index drift | Hotkey activation |
| Module enables then disables on clean install | `dllmain.cpp::is_enabled_by_default` vs `EnabledModules.cs` | Default parity | Default-enabled parity |
| "Can't run as standalone" / exe won't start alone | `main.cpp` PID arg + instance mutex | Lifecycle guard | Review Rules |
| GDI/handle leak over repeated screenshots | `ScreenshotCropAndLockWindow.cpp` DC/bitmap cleanup | GDI leak | GDI resource leaks |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Identify the **mode** first — Reparent, Thumbnail, and Screenshot have very different code paths.
3. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
4. Reproduce with the reporter's monitor layout, target app, and theme.
5. Manually validate the fix across all three modes (no unit tests exist).
