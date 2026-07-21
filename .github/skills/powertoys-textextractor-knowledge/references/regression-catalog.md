# Text Extractor (PowerOCR) — Regression Catalog

Fuller, progressively-disclosed list behind the SKILL.md playbooks. Every entry is grounded in the
module source and/or the mined issue/PR history for `src/modules/PowerOCR/`. Confirm in source before
acting — issue bodies in the raw dataset were sparse, so several entries are grounded primarily on
titles + source behavior (noted per entry).

## Language / OCR engine

- **Language pack availability (Windows.Media.Ocr).** OCR uses OS-installed language packs, not
  anything PowerToys bundles. `ImageMethods.GetOCRLanguage` shows a message box ("No possible OCR
  languages are installed") when `OcrEngine.AvailableRecognizerLanguages` is empty, and
  `ExtractText`/`GetOcrResultFromImageAsync` call `OcrEngine.TryCreateFromLanguage` (can be null).
  Evidence: [#46030](https://github.com/microsoft/PowerToys/issues/46030) (offline/standalone),
  [#41969](https://github.com/microsoft/PowerToys/issues/41969), [#41517](https://github.com/microsoft/PowerToys/issues/41517).
  Grounding: source + issue titles.

- **Language resolution keys off keyboard input language.** `GetOCRLanguage` seeds from
  `InputLanguageManager.Current.CurrentInputLanguage`, overridden by `UserSettings.PreferredLanguage`
  when set; when no exact `LanguageTag` matches it falls back to `AbbreviatedName` then first
  available. Users expect the OS/system language. Evidence:
  [#42904](https://github.com/microsoft/PowerToys/issues/42904),
  [#47137](https://github.com/microsoft/PowerToys/issues/47137). Grounding: source + titles.

- **CJK space-joining and RTL.** `LanguageHelper.IsLanguageSpaceJoining` returns false for `zh*` and
  `ja`; `OcrExtensions.GetTextFromOcrLine` inserts spaces between space-joining words only, and
  `ImageMethods.ExtractText` reverses word order per line for RTL cultures. Changing text assembly
  without these helpers regresses spacing/order. Grounding: source.

## Multi-monitor / DPI

- **Per-monitor overlay creation.** `WindowUtilities.LaunchOCROverlayOnEveryScreen` iterates
  `Screen.AllScreens`, computing `screen.GetDpi()` (via `WPFExtensionMethods.GetDpi` →
  `GetDpiForMonitor`) and constructing an `OCROverlay(bounds, dpiScale)`. The overlay divides physical
  bounds by `DpiScaleX/Y` for WPF Width/Height.
- **DPI coercion hack.** `OCROverlay.Window_Loaded` calls `MoveWindow` twice (first with +1/-1) — the
  first move lands the window on the target monitor and triggers `WM_DPICHANGED`; the coercion forces
  WPF to update `Top/Left/Width/Height`. Removing it reintroduces misalignment.
- **Selection→capture scaling.** `RegionClickCanvas_MouseUp` multiplies selection coordinates by the
  device transform (`CompositionTarget.TransformToDevice` `M11/M22`) to convert DIP→pixels before
  `CopyFromScreen`. Evidence: [#46852](https://github.com/microsoft/PowerToys/issues/46852),
  [#46088](https://github.com/microsoft/PowerToys/issues/46088),
  [#43024](https://github.com/microsoft/PowerToys/issues/43024),
  [#41930](https://github.com/microsoft/PowerToys/issues/41930). Grounding: source + titles.

## Activation shortcut (two paths)

- **Runner centralized hotkey.** `PowerOCRModuleInterface/dllmain.cpp` exposes the hotkey via
  `get_hotkeys`; `on_hotkey` launches the process (if not running) and sets the shared invoke event.
  `parse_hotkey` defaults to **Win+Shift+T** when no key is configured. `is_enabled_by_default`
  returns true only on Win10 (disabled by default on Win11).
- **Standalone low-level hook.** When started detached, `KeyboardMonitor` installs a
  `GlobalKeyboardHook`; `Hook_KeyboardPressed` builds a sorted list of currently-pressed key names,
  compares to `_activationKeys` (derived from `UserSettings.ActivationShortcut`, default
  **"Win + Shift + O"**), and on match sets `e.Handled = true` and calls
  `LaunchOCROverlayOnEveryScreen`.
- **Regression class:** cleared/empty shortcut still conflicting, custom shortcut not launching,
  editor flashing while held. Fix one path, mirror in the other. Evidence:
  [#44914](https://github.com/microsoft/PowerToys/issues/44914),
  [#44505](https://github.com/microsoft/PowerToys/issues/44505),
  [#48785](https://github.com/microsoft/PowerToys/issues/48785),
  [#43791](https://github.com/microsoft/PowerToys/issues/43791),
  [#43250](https://github.com/microsoft/PowerToys/issues/43250). Grounding: source + titles.

  > Note the two defaults differ in source: Runner `parse_hotkey` fallback is Win+Shift+T while
  > standalone `UserSettings.DefaultActivationShortcut` is Win+Shift+O. The effective shortcut is
  > whatever the Settings UI has persisted to `settings.json`; verify there when triaging.

## Threading / capture / clipboard

- **STA + DWM composition.** OCR and clipboard need the STA UI thread and DWM composition;
  `COMException 0x80263001` = `DWM_E_COMPOSITIONDISABLED`. `Clipboard.SetText` is wrapped in try/catch
  in `RegionClickCanvas_MouseUp`; UI tests read the clipboard only from an explicit STA thread. Evidence:
  [#42784](https://github.com/microsoft/PowerToys/issues/42784),
  [#44069](https://github.com/microsoft/PowerToys/issues/44069). Grounding: source + titles.

## Image handling

- **Small-region padding + GDI+ dispose ordering.** `ImageMethods.PadImage` up-pads any bitmap
  smaller than 64×64 (allocates `max(W+16,minW+16) × max(H+16,minH+16)`, clears with the corner pixel
  `GetPixel(0,0)`, draws the original at offset (8,8)) so tiny selections meet the OCR minimum size.
  [PR #44906](https://github.com/microsoft/PowerToys/pull/44906) refactored it to a
  bool + `out` + `[NotNullWhen(true)]` (`TryPad`) contract: returns `false`/`null` when the image is
  already large enough (no allocation); callers (`GetRegionAsBitmap`, `GetWindowBoundsImage`) dispose
  and replace the original **only** when it returns `true`. Never dispose a `Bitmap` while a `Graphics`
  from it is still scoped. Grounding: source + PR.
- **MaxImageDimension gate.** `ExtractText` and `GetRegionsTextAsTableAsync` only scale the bitmap 1.5×
  when `width * 1.5 <= OcrEngine.MaxImageDimension`; otherwise 1.0×. Don't remove the gate. Grounding: source.

## Conventions surfaced in review (non-regression, still enforced)

- **`$(RepoRoot)` over bare relative paths** in project files
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).
- **Name split:** code namespace/dir `PowerOCR`; product/settings/logs `TextExtractor`
  (`UserSettings.PowerOcrModuleName`, `Logger.InitializeLogger("\\TextExtractor\\Logs")`).

## Excluded as noise (not distilled)

.NET 10 upgrade (#41280), VS 2026 support (#44304), CppWinRT dep bump (#45420), "Remove WPF-UI in
favor of Fluent theming" (#46218), global static `SettingsUtils` (#44064), cmdpal extension (#44006),
PowerShell build-invocation reliability (#46729), spell-check allowlist chatter, test-naming (#40754),
PlatformToolset unification discussion — build/CI/dependency churn with no durable module lesson.
