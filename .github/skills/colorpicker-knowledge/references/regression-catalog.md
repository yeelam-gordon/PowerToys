# Color Picker Regression Evidence Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split:** `SKILL.md` owns the reusable symptom-to-guardrail playbooks and review rules. This
> catalog preserves the underlying PR/issue evidence, exact source anchors, reviewer decisions,
> unresolved clusters, chronology, and limitations.

## Decision and fix chronology

| Evidence | Decision or observed regression | Exact source anchor | Reviewer decision / caveat |
|---|---|---|---|
| [#38220](https://github.com/microsoft/PowerToys/issues/38220), [#41107](https://github.com/microsoft/PowerToys/issues/41107), [#43170](https://github.com/microsoft/PowerToys/issues/43170), [#44329](https://github.com/microsoft/PowerToys/issues/44329), [#45446](https://github.com/microsoft/PowerToys/issues/45446) | Wrong sampled colors on HDR, wide-gamut, or high-bit-depth displays established a persistent capture-accuracy cluster. | `ColorPickerUI/Mouse/MouseInfoProvider.cs::GetPixelColor`; `Graphics.CopyFromScreen` into `Bitmap(Format32bppArgb)`, then `GetPixel(0,0)` | Source proves the GDI/32-bpp capture path, but not tone-mapping, color-space, or clamping behavior. Real HDR hardware and authoritative platform evidence are required before assigning a cause. |
| [#39194](https://github.com/microsoft/PowerToys/issues/39194), [#39196](https://github.com/microsoft/PowerToys/issues/39196), [#40643](https://github.com/microsoft/PowerToys/issues/40643), [#43283](https://github.com/microsoft/PowerToys/issues/43283), [#45446](https://github.com/microsoft/PowerToys/issues/45446) | Recorded active-monitor, mixed-DPI positioning, missing drag-line, caption-button, startup-outline, and refresh/capture symptoms. | `ColorPickerUI/Helpers/MonitorResolutionHelper.cs::{AllMonitors,IsPrimary,GetCurrentMonitorDpi}`; `ZoomWindowHelper.ShowZoomWindow`; `MouseInfoProvider.GetMainDisplayRefreshRate` | WPF DIPs and physical pixels differ by monitor. The zoom code's iterative `PointFromScreen` centering and primary-monitor refresh-rate assumption are source facts; the issue set does not establish one shared root cause. |
| [#41806](https://github.com/microsoft/PowerToys/issues/41806), [#43250](https://github.com/microsoft/PowerToys/issues/43250), [#43791](https://github.com/microsoft/PowerToys/issues/43791), [#44404](https://github.com/microsoft/PowerToys/issues/44404), [#44963](https://github.com/microsoft/PowerToys/issues/44963), [#48822](https://github.com/microsoft/PowerToys/issues/48822) | Activation failures include editor flashing, partial-key triggers, lost modifiers across hook-based modules, focus dependence, and shortcuts not firing. | Runner `ColorPicker/dllmain.cpp::{parse_hotkey,on_hotkey,get_hotkeys}` → `SHOW_COLOR_PICKER_SHARED_EVENT`; `ColorPickerUI/ViewModels/MainViewModel.cs`; standalone `Keyboard/KeyboardMonitor.cs::{Hook_KeyboardPressed,SetActivationKeys}`; `Keyboard/GlobalKeyboardHook.cs`; session hook via `AppStateHandler` events | There are two activation mechanisms plus a lightweight active-session hook. Issue symptoms must be assigned to the correct path before diagnosis. |
| [#40900](https://github.com/microsoft/PowerToys/issues/40900) | Requested an “only pick a color” activation option. | Activation-action settings and `AppStateHandler` session routing | Feature request, not regression evidence. |
| [#42261](https://github.com/microsoft/PowerToys/issues/42261) → [#45367](https://github.com/microsoft/PowerToys/pull/45367) | Fixed insufficient contrast in the Color Utility dialog. | `ColorPickerUI/Views/*.xaml`; `ColorPickerUI/Controls/*.xaml` | The other accessibility reports [#42237](https://github.com/microsoft/PowerToys/issues/42237), [#42238](https://github.com/microsoft/PowerToys/issues/42238), and [#42257](https://github.com/microsoft/PowerToys/issues/42257) remain open and are not resolved by this PR. |
| [#44639](https://github.com/microsoft/PowerToys/pull/44639) | Standardized project paths on `$(RepoRoot)` and centralized toolset handling. | Color Picker `.vcxproj` project imports/properties | Reviewer decision: a local `PlatformToolset` exception needs documentation and a follow-up; preserve `Microsoft.Cpp.*.props` ordering. Cross-cutting build evidence, not Color Picker behavior. |
| [#46679](https://github.com/microsoft/PowerToys/pull/46679) | Corrected Decimal test expectations and misleading CIELAB/Natural Color test names. `%Dv` is packed BGR: `R + G*256 + B*65536` (red `255`, blue `16711680`). | `src/common/ManagedCommon/ColorFormatHelper.cs::{GetStringRepresentation,GetDefaultFormat}`; `ColorPickerUI/Helpers/ColorRepresentationHelper.cs`; `ColorPickerUI.UnitTests/Helpers/ColorFormatConversionTest.cs` | Reviewer evidence: expected values and test names must reflect channel order/axis actually asserted. Shared helper changes affect consumers outside Color Picker. |
| [#46729](https://github.com/microsoft/PowerToys/pull/46729) | Preserved PowerShell warning visibility by redirecting warning stream `3>&1` instead of suppressing it. | Color Picker-touching build scripts | Cross-cutting build convention only. |
| [#47892](https://github.com/microsoft/PowerToys/issues/47892), [#48299](https://github.com/microsoft/PowerToys/issues/48299) | Reported virtual/working-set memory growth during Color Picker sessions. | `MouseInfoProvider.GetPixelColor` per-tick `Bitmap`/`Graphics`; `ZoomWindowHelper` static `_bmp`/`_graphics`; show/hide timer lifecycle | High-frequency allocation is observable in source, but the reports do not prove the static zoom objects are leaks. |
| [#48467](https://github.com/microsoft/PowerToys/pull/48467), [#48842](https://github.com/microsoft/PowerToys/pull/48842) | Added/adjusted UI-test infrastructure touching Color Picker. | Test `.csproj`; hidden UIA `TextBlock` hooks in XAML | Reviewer decisions: test-only hooks use `AutomationProperties.AccessibilityView="Raw"`; retain repository `TreatWarningsAsErrors=true`. Cross-cutting test evidence. |
| [#48762](https://github.com/microsoft/PowerToys/pull/48762) | Fixed the picker window appearing inside its own zoom image by excluding the HWND from capture around `CopyFromScreen`. | `ColorPickerUI/Helpers/ZoomWindowHelper.cs::SetZoomImage`; `WindowCaptureExclusionHelper.cs::{Exclude,Include}` | Restore `WDA_NONE` in `finally`; exclusion requires Windows 10 2004+ (build 19041). Failure is logged once (`hasLoggedFailure`) and must not crash. |

## Open and environment-specific symptom clusters

| Report | Observed symptom | Investigation anchor | Status / caveat |
|---|---|---|---|
| [#43018](https://github.com/microsoft/PowerToys/issues/43018) | White rectangle stuck at startup. | App initialization; `AppStateHandler`; `MainWindow` | Closed; root cause not retained. |
| [#42781](https://github.com/microsoft/PowerToys/issues/42781) | Settings crash. | Settings/window initialization | Closed; broad symptom. |
| [#38602](https://github.com/microsoft/PowerToys/issues/38602) | First click registers incorrectly. | Mouse/session state | Root cause not retained. |
| [#38236](https://github.com/microsoft/PowerToys/issues/38236) | Sampled color value differs from the expected value. | Capture sampling, color conversion, and displayed format | Color-value discrepancy evidence; root cause is not retained, so verify the issue and current conversion path before use. |

## Stable source facts that qualify the evidence

- Runner-launched mode uses the centralized Runner hook and shared event; detached mode installs the
  permanent `GlobalKeyboardHook`; an active session separately hooks Esc/Space/Enter/arrows.
- `KeyboardMonitor.SetActivationKeys` re-derives string key names. `_activationShortcutPressed`
  suppresses repeats, and an empty shortcut intentionally matches nothing.
- `MouseInfoProvider.GetMainDisplayRefreshRate` reads the primary monitor, not necessarily the monitor
  under the cursor.
- `ZoomWindowHelper` deliberately reuses static bitmap/graphics objects. `GetPixelColor` creates and
  disposes 1×1 GDI objects per timer tick while the picker is shown.

## Evidence boundaries

- Cross-cutting build/CI/UI-test PRs ([#48467](https://github.com/microsoft/PowerToys/pull/48467),
  [#48842](https://github.com/microsoft/PowerToys/pull/48842),
  [#44304](https://github.com/microsoft/PowerToys/pull/44304),
  [#41280](https://github.com/microsoft/PowerToys/pull/41280),
  [#45420](https://github.com/microsoft/PowerToys/pull/45420),
  [#37651](https://github.com/microsoft/PowerToys/pull/37651)) are retained only when they yielded a
  concrete reviewer decision; touching a project file does not make them behavioral evidence.
- HDR accuracy, mixed-DPI behavior, and memory growth require representative hardware and runtime
  measurement. Issue links identify symptom history, not proof of the proposed mechanism.
