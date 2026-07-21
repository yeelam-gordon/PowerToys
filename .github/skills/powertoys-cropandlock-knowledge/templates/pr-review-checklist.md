# CropAndLock PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces. CropAndLock has **no unit tests** —
require manual validation across the three modes, multiple monitors, and dark/light theme.

## General (any CropAndLock PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] Manual test notes cover Reparent, Thumbnail, and Screenshot modes as relevant.
- [ ] No bare relative paths in `.vcxproj`; uses `$(RepoRoot)`; `Microsoft.Cpp.*.props` import order unchanged.

## Runner DLL / hotkeys / settings (`CropAndLockModuleInterface/dllmain.cpp`)
- [ ] `get_hotkeys` order and `on_hotkey` `hotkeyId` branches kept in lockstep (0/1/2 = reparent/thumbnail/screenshot).
- [ ] Any new hotkey registered with settings shortcut conflict detection.
- [ ] `is_enabled_by_default()` matches `settings-ui` `EnabledModules.cs` default (`false`).
- [ ] `parse_hotkey` tolerates malformed JSON (keeps prior value; no throw escaping).
- [ ] GPO gate (`gpo_policy_enabled_configuration`) still honored.

## Exe lifecycle / overlay (`CropAndLock/main.cpp`, `OverlayWindow.cpp`)
- [ ] Standalone guard intact (requires Runner PID arg + single-instance mutex).
- [ ] `ProcessWaiter` parent-exit teardown and exit-event handling preserved.
- [ ] Overlay geometry stays in all-displays-union space; shade insets clamped non-negative.
- [ ] ESC-to-cancel and empty-rect early-exit paths preserved.
- [ ] New cropped windows get `handleTheme()` applied on creation.

## Reparent mode (`ReparentCropAndLockWindow.cpp`, `ChildWindow.cpp`)
- [ ] `SaveOriginalState`/`RestoreOriginalState` round-trip for normal **and** maximized targets.
- [ ] Restore reverses everything: position → `SetParent(nullptr)` → `WINDOWPLACEMENT` → clear `WS_CHILD`.
- [ ] Focus forwarding (`WM_MOUSEACTIVATE`/`WM_ACTIVATE` → target) unchanged unless intended.
- [ ] DPI handled via `AdjustWindowRectExForDpi` with the monitor's DPI.

## Thumbnail mode (`ThumbnailCropAndLockWindow.cpp`)
- [ ] Uses `DWMWA_EXTENDED_FRAME_BOUNDS` for geometry; `ComputeDestRect` aspect-fit preserved.
- [ ] Thumbnail released (`m_thumbnail.reset`) on disconnect; updated on `WM_SIZE`/`WM_SIZING`.

## Screenshot mode (`ScreenshotCropAndLockWindow.cpp`)
- [ ] `PrintWindow` failure treated as expected (GPU/protected) — meaningful error, not a hard throw.
- [ ] Every GDI acquire paired with release on all paths (`GetDC`/`ReleaseDC`, `CreateCompatibleDC`/`DeleteDC`, bitmaps `DeleteObject`, restored `SelectObject`); RAII preferred.
- [ ] No leak on the throwing path (failed `check_bool`).

## Build / SDK / deps
- [ ] After CppWinRT/WinAppSDK/VS/.NET bumps: exe builds and all three modes smoke-tested.
- [ ] Shutdown/teardown still clean (no size-vs-safety regressions like Hybrid CRT — #43484).
