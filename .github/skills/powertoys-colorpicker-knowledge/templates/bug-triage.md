# Color Picker Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / inputs:**
- **OS / build / Win10 vs Win11:**
- **Display: HDR on? wide gamut? DPI scale? multi-monitor?:**
- **Runner-launched or standalone? custom activation shortcut?:**
- **Color format in use:**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Picked color wrong, esp. on HDR/wide-gamut | `Mouse/MouseInfoProvider.cs::GetPixelColor` | GDI sRGB capture limit | HDR capture |
| Color slightly off / "not present at pointer" | `MouseInfoProvider.GetPixelColor`; display pipeline | Capture accuracy | HDR capture |
| Zoom view shows the picker's own window/corner | `Helpers/ZoomWindowHelper.cs::SetZoomImage`; `WindowCaptureExclusionHelper` | Capture exclusion | Zoom capture |
| Zoom offset / wrong monitor on mixed DPI | `ZoomWindowHelper.ShowZoomWindow` (`PointFromScreen` loop); `MonitorResolutionHelper` | DPI/positioning | Multi-monitor/DPI |
| Picker opens on wrong monitor | `MonitorResolutionHelper`; `AppStateHandler` show path | Multi-monitor | Multi-monitor/DPI |
| Shortcut doesn't fire (standalone) | `Keyboard/KeyboardMonitor.cs::SetActivationKeys`/`Hook_KeyboardPressed` | Standalone hook | Activation |
| Shortcut doesn't fire (via Runner) | `dllmain.cpp::parse_hotkey`/`on_hotkey`; `MainViewModel` shared-event waiter | Runner hotkey | Activation |
| Fires while only some keys held | `KeyboardMonitor.Hook_KeyboardPressed` (`_activationShortcutPressed`, `ArraysAreSame`) | Latch/match | Activation |
| Settings editor "flashes" the shortcut while held | global low-level hook vs editor; both activation paths | Hook interaction | Activation |
| Modifier keys released when other hook modules active | shared low-level keyboard hook (cross-module) | Hooking | Activation |
| Arrow-key nudge not working | `KeyboardMonitor.CheckMoveNeeded`; `AppStateHandler.MoveCursor` | Nudge | Activation |
| Decimal / format value looks wrong or R/B swapped | `ManagedCommon/ColorFormatHelper.cs` (`%Dv` BGR); `ColorRepresentationHelper` | Format order | Color format |
| Lab / NCol axis wrong | `ColorFormatHelper.GetStringRepresentation`; `ColorFormatConversionTest.cs` | Axis confusion | Color format |
| Color name not localized | `ColorRepresentationHelper.ReplaceName` (`%Na`) | Localization | Review Rules |
| Memory grows over a session | `MouseInfoProvider.GetPixelColor` per-tick GDI; `ZoomWindowHelper` static bmp | GDI/memory | Memory/GDI |
| White rectangle stuck at startup | app init / window show (`AppStateHandler`, `MainWindow`) | Startup race | (Pitfalls) |
| Second instance won't start | `App.xaml.cs` mutex `PowerToys_ColorPicker_InstanceMutex` | Single-instance | Review Rules |
| Contrast / heading / label a11y | `Views/*.xaml`, `Controls/*.xaml` | Accessibility | (Review Rules) |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
3. Reproduce with the reporter's display config (HDR/DPI/monitors) and activation mode.
4. Add/extend a unit test in `ColorPickerUI.UnitTests` (formats) before fixing where applicable.
