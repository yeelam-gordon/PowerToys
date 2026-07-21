# Color Picker Regression Catalog

Fuller regression/issue list backing the Regression Playbooks in `SKILL.md`. Each entry:
symptom class → where in source → root cause → guardrail → evidence. Confirm in source before acting.

## 1. GDI screen capture is sRGB — wrong color on HDR / wide-gamut
- **Where:** `ColorPickerUI/Mouse/MouseInfoProvider.cs::GetPixelColor` — `Graphics.CopyFromScreen`
  into a 1×1 `Bitmap(Format32bppArgb)`, `GetPixel(0,0)`.
- **Root cause:** GDI reads the composited **sRGB 8-bpc** desktop; HDR (scRGB/10-bit) and wide-gamut
  content is tone-mapped/clamped before GDI, so the sample ≠ on-screen value.
- **Guardrail:** treat HDR color accuracy as a known display-pipeline gap; verify any capture change on
  real HDR hardware before claiming a fix.
- **Evidence:** #44329 (closed), #41107 (closed), #45446, #43170 (closed), #38220 (Lab-by-numbers).

## 2. Zoom captures the Color Picker window itself
- **Where:** `ColorPickerUI/Helpers/ZoomWindowHelper.cs::SetZoomImage` +
  `Helpers/WindowCaptureExclusionHelper.cs`.
- **Root cause:** the magnifier grabs a cursor-centered screen rect while the picker window is visible.
- **Guardrail:** `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` before capture, restore `WDA_NONE`
  in `finally`; Win10 2004+ only; log-once on failure (`hasLoggedFailure`), never crash.
- **Evidence:** PR #48762 ("Fix the main window UI appearing in the zoomed-in view").

## 3. Dual activation architecture (+ a third session hook)
- **Where:** Runner: `ColorPicker/dllmain.cpp` `parse_hotkey`/`on_hotkey`/`get_hotkeys` →
  `SHOW_COLOR_PICKER_SHARED_EVENT`; app: `ViewModels/MainViewModel.cs` `NativeEventWaiter`;
  standalone: `Keyboard/KeyboardMonitor.cs` + `Keyboard/GlobalKeyboardHook.cs`.
- **Details:** Runner-launched builds do **not** run `KeyboardMonitor` permanently; a lightweight hook
  is started per active session (via `AppStateHandler` events) to catch Esc/Space/Enter/arrows.
  Standalone/detached (`App.IsRunningDetachedFromPowerToys()`) runs the permanent hook.
- **Root cause of bugs:** the mechanisms are maintained separately; string-based key matching must be
  re-derived on setting change (`SetActivationKeys`); global low-level hook observes editor keystrokes.
- **Guardrail:** keep both in sync; empty shortcut arms nothing; preserve `_activationShortcutPressed`
  latch; fallback default Win+Shift+C in `parse_hotkey`.
- **Evidence:** #43791/#43250 (editor flashing, closed/open), #44963 (partial-key trigger),
  #44404 (keys don't work), #48822 (only works with PT window focused), #41806 (shared-hook modifier
  release across modules), #40900 ("only pick a color" activation option request).

## 4. Color-format specifier strings
- **Where:** `src/common/ManagedCommon/ColorFormatHelper.cs` (`GetStringRepresentation`,
  `GetDefaultFormat`, char map incl. `Dv` = "Decimal value (BGR) int"); wrapper + name localization in
  `ColorPickerUI/Helpers/ColorRepresentationHelper.cs`.
- **Root cause:** `%Dv` packs **BGR** (`R + G*256 + B*65536`) → Red=255, Blue=16711680; CIELAB/NCol
  axis naming is easy to get backwards.
- **Guardrail:** confirm channel order; name tests after the asserted axis; test known colors.
- **Evidence:** PR #46679 — swapped Decimal expected values fixed; `ConvertToCIELAB_Blue_HasNegativeA`
  renamed to `HasNegativeB`; `ConvertToNaturalColor_*_ReturnsX0` renamed to `HueStartsWithX`.

## 5. Multi-monitor / per-monitor DPI
- **Where:** `ColorPickerUI/Helpers/MonitorResolutionHelper.cs` (`AllMonitors`, `IsPrimary`,
  `GetCurrentMonitorDpi`); `ZoomWindowHelper.ShowZoomWindow` (iterative `PointFromScreen` centering);
  `MouseInfoProvider.GetMainDisplayRefreshRate` (primary-monitor `EnumDisplaySettingsW`).
- **Root cause:** DIP vs physical-pixel mismatch across monitors of different DPI.
- **Guardrail:** keep the convergence loop; convert at boundaries; refresh rate is primary-only.
- **Evidence:** #39194 (open on active monitor, closed), #45446, #39196 (drag line missing),
  #43283 (caption buttons), #40643 (selector outlines taskbar icon on startup).

## 6. Memory / GDI resource growth
- **Where:** `MouseInfoProvider.GetPixelColor` (per-tick `Bitmap`+`Graphics`, refresh-rate cadence);
  `ZoomWindowHelper` static `_bmp`/`_graphics`.
- **Root cause:** high-frequency GDI allocation while the picker is shown.
- **Guardrail:** per-tick objects in `using`; timer bound to show/hide lifecycle.
- **Evidence:** #47892 (virtual memory leak), #48299 (~230MB).

## 7. Startup / window state
- **Where:** app init, `AppStateHandler` show/hide, `MainWindow`.
- **Evidence:** #43018 (white rectangle stuck at startup, closed), #42781 (settings crash, closed),
  #38602 (first click registers incorrectly), #38236.

## 8. Accessibility
- **Where:** `Views/*.xaml`, `Controls/*.xaml`; test-only UIA hooks need `AccessibilityView="Raw"`.
- **Evidence:** #42261 (contrast, closed), #42257 (settings button non-interactive),
  #42238 ("Add New Format" not associated with label), #42237 (heading not defined),
  PR #48467 (Raw UIA hook review), PR #45367 (contrast fix).

## Build / infra conventions (cross-cutting, ColorPicker-touching)
- `$(RepoRoot)` over bare relative paths; PlatformToolset unified centrally — PR #44639 (DHowett:
  document + follow-up bug if a local `PlatformToolset` is truly required).
- Keep C# `TreatWarningsAsErrors=true`; don't override in new test csproj — PR #48467/#48842.
- Build-script warning suppression: redirect PowerShell warning stream (`3>&1`) instead of hiding it —
  PR #46729.
- `TreatWarningsAsErrors` / StyleCop (SA1512/SA1515), CA1866/CA1310 (`StartsWith(char)`) — PR #46679.

## Notes on signal
Most raw PRs in this module's history are cross-cutting build/CI/UITest-framework changes
(#48467, #48842, #44304, #41280, #45420, #37651) that merely *touch* ColorPicker project files; they
were **excluded** from the playbooks as non-durable to Color Picker behavior. The durable signal is in
the source code + the bug issues above.
