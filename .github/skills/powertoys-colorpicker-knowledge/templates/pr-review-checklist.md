# Color Picker PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces.

## General (any Color Picker PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] GPO gate honored at entry (`Program.Main` / Runner module).
- [ ] No bare relative paths in `.vcxproj`; uses `$(RepoRoot)`; no per-project `PlatformToolset`
      re-add without a comment + follow-up.
- [ ] C# projects keep `TreatWarningsAsErrors=true` (no local override).

## Pixel capture (`Mouse/MouseInfoProvider.cs`)
- [ ] `GetPixelColor` keeps `Format32bppArgb` + `SourceCopy`; GDI objects stay in `using` blocks.
- [ ] No claim that a capture change "fixes" HDR/wide-gamut accuracy without HDR-hardware verification.
- [ ] Capture timer starts on `AppShown` and stops on `AppClosed/AppHidden` only (no leaked timer).
- [ ] Refresh-rate polling still keys off the primary monitor (`GetMainDisplayRefreshRate`).

## Zoom / magnifier (`Helpers/ZoomWindowHelper.cs`, `Helpers/WindowCaptureExclusionHelper.cs`)
- [ ] `WindowCaptureExclusionHelper.Exclude` is always paired with `Include` in a `finally`.
- [ ] Exclusion unsupported/failed path (pre-Win10-2004) does not crash.
- [ ] `ShowZoomWindow` DPI convergence (`PointFromScreen` loop) preserved.
- [ ] Static `_bmp`/`_graphics` lifetime unchanged unless memory implications reviewed.

## Activation (`Keyboard/KeyboardMonitor.cs`, `dllmain.cpp`, `ViewModels/MainViewModel.cs`)
- [ ] Change mirrored/considered across **both** paths (Runner hotkey vs standalone `GlobalKeyboardHook`).
- [ ] Empty `ActivationShortcut` arms no match (`_activationKeys` empty).
- [ ] `_activationShortcutPressed` latch preserved (held keys fire once).
- [ ] Runner default hotkey fallback (Win+Shift+C) intact in `parse_hotkey`.
- [ ] Lightweight per-session hook (Esc/Space/Enter/arrows) still started/disposed with the session.

## Color formats / editor (`Helpers/ColorRepresentationHelper.cs`, `ManagedCommon/ColorFormatHelper.cs`, `ViewModels/ColorEditorViewModel.cs`)
- [ ] Format-specifier channel order confirmed (Decimal `%Dv` is BGR); test named after asserted axis.
- [ ] Unit test in `ColorFormatConversionTest.cs` added/updated with known colors per axis.
- [ ] Color-name output routed through localization (`ReplaceName` / `%Na`).
- [ ] Shared `ColorFormatHelper` change checked against other consumers.

## UI / accessibility (`Views/*.xaml`, controls)
- [ ] Test-only UIA hooks marked `AutomationProperties.AccessibilityView="Raw"`.
- [ ] Contrast / heading / label-association a11y concerns considered (#42261, #42237, #42238).

## Single-instance / lifecycle (`App.xaml.cs`)
- [ ] Mutex `Local\PowerToys_ColorPicker_InstanceMutex` unchanged; second-instance exit intact.
- [ ] Cursor restored on exit/exception (`CursorManager.RestoreOriginalCursors`).
