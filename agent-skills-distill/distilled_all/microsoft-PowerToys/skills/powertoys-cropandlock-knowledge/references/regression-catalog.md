# CropAndLock Regression Catalog

Fuller list of regressions, key decisions, and open bug reports for the PowerToys **CropAndLock**
module. Progressive-disclosure companion to `SKILL.md` (keeps the main file lean). Every entry is
grounded in a real PR/issue and a source location. Treat map rows as hypotheses to confirm in source.

## Architecture facts (decisions)

- **Separate process model.** The Runner DLL (`CropAndLockModuleInterface/dllmain.cpp`) launches
  `PowerToys.CropAndLock.exe` via `ShellExecuteExW`, passing the Runner PID; the exe watches the
  parent with `ProcessWaiter::OnProcessTerminate` and exits when the parent dies. The DLL signals the
  exe through named Win32 events (`CROP_AND_LOCK_{REPARENT,THUMBNAIL,SCREENSHOT,EXIT}_EVENT` in
  `common/interop/shared_constants.h`). The exe refuses to run standalone (requires the PID arg) and
  enforces a single instance via `Local\PowerToys_CropAndLock_InstanceMutex` (`main.cpp`).
- **Three modes, one interface.** `CropAndLockWindow` (`CropAndLockWindow.h`) is implemented by
  `ReparentCropAndLockWindow`, `ThumbnailCropAndLockWindow`, and `ScreenshotCropAndLockWindow`;
  `main.cpp::ProcessCommand(CropAndLockType)` picks one per hotkey. `CropAndLockType` lives in
  `SettingsWindow.h`.
- **DPI awareness is deliberate.** `main.cpp` sets `PER_MONITOR_AWARE_V2` and comments that
  reparenting across DPI contexts has documented consequences (links the `SetParent` remarks). Reparent
  geometry uses `AdjustWindowRectExForDpi` with the target monitor's DPI.
- **Reference frames differ by mode.** Reparent uses `GetWindowRect`; Thumbnail and Screenshot use
  `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`. All add the client/window delta via
  `WindowRectUtil.h::ClientAreaInScreenSpace`. The overlay works in the all-displays union
  (`DisplaysUtil.h::ComputeAllDisplaysUnion`).

## Feature additions (PRs)

| PR | What | Durable lesson |
|---|---|---|
| [#38044](https://github.com/microsoft/PowerToys/pull/38044) | Theme-aware cropped windows | Apply `SetImmersiveDarkMode` to every cropped window on create + theme change (`handleTheme`). Closes #28348, fixes #35562. |
| [#40720](https://github.com/microsoft/PowerToys/pull/40720) | "Screenshot" (non-updating / frozen) mode | New mode = new hotkey → must register with **shortcut conflict detection**; `PrintWindow` fails on GPU/protected windows; GDI paths need pairing/RAII. No unit tests — manual validation. Closes #31799, #33071. |
| [#47144](https://github.com/microsoft/PowerToys/pull/47144) | Default 8 modules to disabled | `is_enabled_by_default()` (C++) must equal `EnabledModules.cs` (C#) or clean installs flicker + DSC gap. |
| [#43484](https://github.com/microsoft/PowerToys/pull/43484) | Revert Hybrid CRT | Binary-size win reverted: module DLLs failed to unload safely at quit. Safety > size. |
| [#42073](https://github.com/microsoft/PowerToys/pull/42073) | Hybrid CRT (later reverted) | See #43484. |
| [#44639](https://github.com/microsoft/PowerToys/pull/44639) | `$(RepoRoot)` project paths | No bare relative paths; `Microsoft.Cpp.Default.props` import order is sensitive — don't reorder. |

## Regression / bug reports (issues)

Reparent geometry & restore:
- [#34813](https://github.com/microsoft/PowerToys/issues/34813) — wrong offset of the window when closing crop and lock.
- [#45666](https://github.com/microsoft/PowerToys/issues/45666) — "Crop and Lock lost an ability."
- [#42495](https://github.com/microsoft/PowerToys/issues/42495) — first drag of a cropped window always fails.
- [#42494](https://github.com/microsoft/PowerToys/issues/42494) — can't move small cropped windows.
- → Confirm in `ReparentCropAndLockWindow.cpp` `SaveOriginalState`/`RestoreOriginalState`/`DisconnectTarget`.

Multi-monitor / coordinates:
- [#36485](https://github.com/microsoft/PowerToys/issues/36485) — broken for multiple screens.
- → `OverlayWindow.cpp::SetupOverlay`, `DisplaysUtil.h::ComputeAllDisplaysUnion`.

Capture (Screenshot / GPU windows):
- [#48850](https://github.com/microsoft/PowerToys/issues/48850) — Screenshot mode black image (ZoomIt annotations) after update.
- [#42744](https://github.com/microsoft/PowerToys/issues/42744) — white border in Brave.
- → `ScreenshotCropAndLockWindow.cpp::CropAndLock` (`PrintWindow` PW_RENDERFULLCONTENT).

Live-update expectations:
- [#38104](https://github.com/microsoft/PowerToys/issues/38104) — cropped window in fullscreen doesn't update (Screenshot is frozen by design; Thumbnail can stall on fullscreen/DWM-occluded).
- → `main.cpp::ProcessCommand`, `ThumbnailCropAndLockWindow.cpp`.

Theme:
- [#35562](https://github.com/microsoft/PowerToys/issues/35562) — title bar white instead of system theme + thumbnail marked area off (fixed by #38044).
- → `main.cpp::handleTheme`.

Hotkey / activation:
- [#42558](https://github.com/microsoft/PowerToys/issues/42558) — shortcut doesn't work in a Hyper-V VM window.
- [#41806](https://github.com/microsoft/PowerToys/issues/41806) — modifier keys prematurely released when multiple hook-based modules active (cross-module).
- [#43791](https://github.com/microsoft/PowerToys/issues/43791) / [#43250](https://github.com/microsoft/PowerToys/issues/43250) — shortcut editors flash the shortcut while held (cross-module, Settings-side).
- → `dllmain.cpp` `on_hotkey`/`get_hotkeys`; Settings conflict detection.

Other environment-specific / open:
- [#43455](https://github.com/microsoft/PowerToys/issues/43455) — system window cropping doesn't work.
- [#38646](https://github.com/microsoft/PowerToys/issues/38646) — bug with Microsoft Store.
- [#47344](https://github.com/microsoft/PowerToys/issues/47344) — cropped window position not remembered.
- [#46168](https://github.com/microsoft/PowerToys/issues/46168) / [#46524](https://github.com/microsoft/PowerToys/issues/46524) — activation via Command Palette / Dock (integration; #46524 duplicate).

## Review-comment lessons (from PR threads)

- **`PrintWindow` isn't universal** — review of #40720 flagged that it fails for hardware-accelerated
  or protected windows; handle the failure with a message instead of throwing via `check_bool`.
- **GDI RAII** — review of #40720 repeatedly flagged unreleased DCs (`GetDC(nullptr)`), unpaired
  `SelectObject`, and leak-on-throw in the screenshot path; pair every acquire or use scoped wrappers.
- **Conflict detection is mandatory for new shortcuts** — during #40720 the author had to update for the
  newly added shortcut conflict-detection framework before merge.
- **Build hygiene** — #44639 / #42073 threads: keep `Microsoft.Cpp.*.props` include order; prefer
  `$(RepoRoot)` over `..\..\..\`; don't clutter the repo root with one-off build-rule files.

## Testing note

CropAndLock ships **no unit tests** (stated in #40720). Every behavior change needs manual validation
across the three modes, at least two monitors (including a negative-origin layout), a maximized target,
and both dark and light themes.
