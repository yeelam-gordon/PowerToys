# CropAndLock Regression Evidence Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split:** `SKILL.md` owns reusable architecture guidance, regression playbooks, guardrails, review
> rules, and pitfalls. This catalog retains the supporting chronology, PR/issue links, exact source
> anchors, reviewer decisions, unresolved symptom clusters, and caveats.

## Decision and feature chronology

| Evidence | Decision / change | Exact source anchor | Reviewer decision / caveat |
|---|---|---|---|
| [#35562](https://github.com/microsoft/PowerToys/issues/35562) → [#38044](https://github.com/microsoft/PowerToys/pull/38044) | Applied system theme to cropped-window title bars on creation and theme changes. Related issue [#28348](https://github.com/microsoft/PowerToys/issues/28348) remains open. | `CropAndLock/main.cpp::handleTheme`; `theme_listener`; `ThemeHelpers::SetImmersiveDarkMode` | The open issue also mentions title-bar/background behavior; the PR does not establish that every geometry/theme symptom shared one cause. |
| [#31799](https://github.com/microsoft/PowerToys/issues/31799), [#33071](https://github.com/microsoft/PowerToys/issues/33071) → [#40720](https://github.com/microsoft/PowerToys/pull/40720) | Added frozen Screenshot mode and its hotkey. | `CropAndLock/SettingsWindow.h::CropAndLockType`; `main.cpp::ProcessCommand`; `ScreenshotCropAndLockWindow.cpp::CropAndLock`; `CropAndLockModuleInterface/dllmain.cpp::{get_hotkeys,on_hotkey}` | Review required shortcut conflict-detection registration. Review also identified `PrintWindow` limitations and unpaired/leak-on-throw GDI resources. No unit tests; validation was manual. |
| [#42073](https://github.com/microsoft/PowerToys/pull/42073) → [#43484](https://github.com/microsoft/PowerToys/pull/43484) | Introduced, then reverted, Hybrid CRT. | Module project/runtime configuration and Runner module unloading | Reviewer/product decision: clean DLL teardown outranked binary-size savings because modules failed to unload safely at quit. |
| [#44639](https://github.com/microsoft/PowerToys/pull/44639) | Replaced bare relative project paths with `$(RepoRoot)`. | CropAndLock `.vcxproj` imports/properties | Preserve `Microsoft.Cpp.Default.props` import order; build-system evidence, not module behavior. |
| [#47144](https://github.com/microsoft/PowerToys/pull/47144) | Changed CropAndLock among eight modules to disabled by default and aligned Runner/settings defaults. | `CropAndLockModuleInterface/dllmain.cpp::is_enabled_by_default`; Settings UI `EnabledModules.cs` | Both declarations must agree to avoid clean-install flicker and DSC/compliance mismatch. |

## Reviewer decision record: Screenshot mode

Review threads on [#40720](https://github.com/microsoft/PowerToys/pull/40720) established:

| Decision | Exact source anchor | Qualification |
|---|---|---|
| `PrintWindow(..., PW_RENDERFULLCONTENT)` is not universal; hardware-accelerated, protected, and some DWM surfaces may return black/partial output or fail. | `ScreenshotCropAndLockWindow.cpp::CropAndLock` | Failure should be treated as an expected capture limitation, not proof of a crop-coordinate defect. |
| Every `GetDC`, `CreateCompatibleDC`, `CreateCompatibleBitmap`, and `SelectObject` needs paired cleanup/restoration on success and throwing paths. | `ScreenshotCropAndLockWindow.cpp::CropAndLock`; `WM_PAINT` | Review repeatedly requested RAII/scoped cleanup because `winrt::check_bool` can bypass manual releases. |
| A new hotkey must participate in Settings shortcut conflict detection. | `CropAndLockModuleInterface/dllmain.cpp::{get_hotkeys,on_hotkey}` plus Settings conflict registration | Numeric hotkey ordering is a separate source invariant; this decision covers registration. |

## Open and historical symptom clusters

### Reparent geometry, interaction, and restoration

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#34813](https://github.com/microsoft/PowerToys/issues/34813) | Original window has the wrong offset after closing the crop. | `ReparentCropAndLockWindow.cpp::{SaveOriginalState,RestoreOriginalState,DisconnectTarget}` | Restoration ordering/style/placement are investigation anchors, not a confirmed root cause in this ledger. |
| [#45666](https://github.com/microsoft/PowerToys/issues/45666) | “Crop and Lock lost an ability.” | Reparent/live-mode behavior; `main.cpp::ProcessCommand` | Ambiguous report; confirm issue details and mode. |
| [#42495](https://github.com/microsoft/PowerToys/issues/42495) | First drag of a cropped window fails. | Reparent host interaction; `ReparentCropAndLockWindow.cpp` | Open symptom; do not equate automatically with restore offset. |
| [#42494](https://github.com/microsoft/PowerToys/issues/42494) | Small cropped windows cannot be moved. | Cropped host hit testing / interaction | Source anchor needs confirmation before changes. |
| [#47344](https://github.com/microsoft/PowerToys/issues/47344) | Cropped-window position is not remembered. | Cropped-window placement lifecycle | Persistence expectation may differ by mode/session. |

### Multi-monitor and coordinate space

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#36485](https://github.com/microsoft/PowerToys/issues/36485) | CropAndLock is broken with multiple screens. | `OverlayWindow.cpp::SetupOverlay`; `DisplaysUtil.h::ComputeAllDisplaysUnion` | Test negative-origin layouts. The broad report does not prove every mode shares one geometry defect. |

### Screenshot and capture compatibility

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#48850](https://github.com/microsoft/PowerToys/issues/48850) | Screenshot mode produces a black image for ZoomIt annotations after an update. | `ScreenshotCropAndLockWindow.cpp::CropAndLock`; `PrintWindow(PW_RENDERFULLCONTENT)` | Environment/content-specific; compare with Thumbnail before attributing to crop math. |
| [#42744](https://github.com/microsoft/PowerToys/issues/42744) | White border appears in Brave capture. | Screenshot extended-frame bounds and `PrintWindow` path | Browser/DWM rendering caveat. |

### Live-update expectations

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#38104](https://github.com/microsoft/PowerToys/issues/38104) | Cropped fullscreen window does not update. | `main.cpp::ProcessCommand`; `ThumbnailCropAndLockWindow.cpp`; Screenshot mode dispatch | Screenshot is frozen by design; Thumbnail may stall for fullscreen/DWM-occluded targets. Establish the selected mode first. |

### Activation and integration

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#42558](https://github.com/microsoft/PowerToys/issues/42558) | Shortcut does not work inside a Hyper-V VM window. | `CropAndLockModuleInterface/dllmain.cpp::{on_hotkey,get_hotkeys}`; Runner centralized hook | Environment-specific; guest/host input routing may dominate. |
| [#41806](https://github.com/microsoft/PowerToys/issues/41806) | Modifiers are released prematurely with multiple hook-based modules. | Runner/shared keyboard-hook infrastructure | Cross-module report, not CropAndLock-local proof. |
| [#43791](https://github.com/microsoft/PowerToys/issues/43791), [#43250](https://github.com/microsoft/PowerToys/issues/43250) | Shortcut editor flashes the shortcut while keys are held. | Settings shortcut editor/conflict path | Cross-module Settings behavior. |
| [#46168](https://github.com/microsoft/PowerToys/issues/46168), [#46524](https://github.com/microsoft/PowerToys/issues/46524) | Activation requested through Command Palette / Dock. | Integration entry points | [#46524](https://github.com/microsoft/PowerToys/issues/46524) is a duplicate; feature/integration evidence, not module regression. |

### Restricted targets and environment-specific reports

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#43455](https://github.com/microsoft/PowerToys/issues/43455) | System-window cropping does not work. | Target eligibility and mode-specific capture/reparent calls | System/elevated/protected windows have different restrictions. |
| [#38646](https://github.com/microsoft/PowerToys/issues/38646) | Microsoft Store interaction fails. | Target-window selection and mode behavior | Environment-specific; root cause unverified. |

## Stable source facts that qualify the evidence

- `CropAndLockModuleInterface/dllmain.cpp` launches `PowerToys.CropAndLock.exe` with the Runner PID,
  signals `CROP_AND_LOCK_{REPARENT,THUMBNAIL,SCREENSHOT,EXIT}_EVENT`, and the exe exits with its parent.
  `main.cpp` requires the PID and guards `Local\PowerToys_CropAndLock_InstanceMutex`.
- `main.cpp` selects `ReparentCropAndLockWindow`, `ThumbnailCropAndLockWindow`, or
  `ScreenshotCropAndLockWindow` through `ProcessCommand(CropAndLockType)`.
- The process is `PER_MONITOR_AWARE_V2`. Reparent geometry uses `GetWindowRect` and
  `AdjustWindowRectExForDpi`; Thumbnail/Screenshot use
  `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`. All use
  `WindowRectUtil.h::ClientAreaInScreenSpace`, while overlay selection uses
  `DisplaysUtil.h::ComputeAllDisplaysUnion`.

## Evidence boundaries

- CropAndLock has no unit-test suite recorded in [#40720](https://github.com/microsoft/PowerToys/pull/40720).
  Mode, monitor topology, DPI, maximized state, target rendering technology, and theme therefore
  remain manual-validation dimensions.
- Open reports are symptom clusters. Source anchors identify where to begin verification; they do not
  convert issue descriptions into confirmed root causes.
