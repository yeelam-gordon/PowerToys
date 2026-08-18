---
name: powertoys-textextractor-knowledge
description: 'PowerToys Text Extractor (module dir/namespace PowerOCR, product name "TextExtractor") knowledge: feature->file/function map, recurring regression playbooks (Windows.Media.Ocr language-pack availability, input-vs-OS language resolution, multi-monitor/per-monitor-DPI overlay misalignment, dual activation paths — Runner centralized hotkey vs standalone GlobalKeyboardHook, clipboard/STA capture, GDI bitmap disposal), maintainer review rules, and gotchas. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/PowerOCR — screen-capture OCR overlay, language selection, table/single-line output, clipboard copy, activation shortcut, settings, GPO. Keywords: Text Extractor, PowerOCR, OCR, Windows.Media.Ocr, OcrEngine, language pack, screen capture overlay, multi-monitor, DPI, clipboard, global keyboard hook, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Text Extractor (PowerOCR) Knowledge

Grounded engineering knowledge for the PowerToys **Text Extractor** module — a screen-region
OCR utility. On the activation shortcut it draws a dimming overlay across every monitor, lets the
user drag-select a region (or click a single word), runs Windows' built-in `Windows.Media.Ocr`
engine, and copies the recognized text to the clipboard. The module directory and namespace are
**PowerOCR**; the user-facing product / settings / logs name is **TextExtractor**. Use this to
localize code fast, avoid known regression traps, and enforce the conventions maintainers already
established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/PowerOCR/` and needing prior art.
- Fixing/triaging a Text Extractor bug: no text captured, overlay on wrong monitor / misaligned,
  "No possible OCR languages are installed", wrong/default language, activation shortcut not firing
  or conflicting, capture blank on certain windows, clipboard not populated, crash on capture.
- Reviewing a Text Extractor PR against maintainer conventions and regression traps.
- Touching the OCR pipeline, the multi-monitor capture overlay, language selection, table/
  single-line output, or the activation-shortcut plumbing.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Runner-side module registration, centralized hotkey, GPO gate, process launch/terminate | `PowerOCRModuleInterface/dllmain.cpp` `parse_hotkey`, `on_hotkey`, `launch_process`, `get_hotkeys`, `is_enabled_by_default` (**disabled by default on Win11, enabled on Win10**) |
| Shared-event names (invoke / terminate) | `PowerOCRModuleInterface/PowerOcrConstants.h`; consumed via `PowerToys.Interop` `Constants.ShowPowerOCRSharedEvent()` / `TerminatePowerOCRSharedEvent()` |
| App bootstrap, single-instance mutex, GPO check, UI-culture load, Runner-vs-standalone branch | `PowerOCR/App.xaml.cs` `Application_Startup` (mutex `Local\PowerToys_PowerOCR_InstanceMutex`) |
| Activation when launched by Runner (waits on shared event) | `PowerOCR/Keyboard/EventMonitor.cs` `StartOCRSession` |
| Activation standalone (own low-level keyboard hook, parses `ActivationShortcut` string) | `PowerOCR/Keyboard/KeyboardMonitor.cs` `Hook_KeyboardPressed`, `SetActivationKeys`; `Keyboard/GlobalKeyboardHook.cs` |
| Create overlay window per monitor, per-screen DPI, foreground activation, close-all | `PowerOCR/Helpers/WindowUtilities.cs` `LaunchOCROverlayOnEveryScreen`, `ActivateWindow`, `CloseAllOCROverlays` |
| Overlay window: drag/click selection, DPI MoveWindow coercion, region math, clipboard write | `PowerOCR/OCROverlay.xaml.cs` `Window_Loaded`, `RegionClickCanvas_Mouse*`, `Clipboard.SetText` |
| Screen bitmap capture, padding, uniform scaling | `PowerOCR/Helpers/ImageMethods.cs` `GetWindowBoundsImage`, `GetRegionAsBitmap`, `PadImage`, `ScaleBitmapUniform` |
| OCR engine call (Windows built-in) | `ImageMethods.ExtractText` / `Helpers/OcrExtensions.cs` `GetOcrResultFromImageAsync` (`OcrEngine.TryCreateFromLanguage`, `RecognizeAsync`, `OcrEngine.MaxImageDimension`) |
| Language selection & availability | `ImageMethods.GetOCRLanguage` (`OcrEngine.AvailableRecognizerLanguages`, `InputLanguageManager.Current.CurrentInputLanguage`); `Helpers/LanguageHelper.cs` `IsLanguageSpaceJoining` |
| Line/word assembly, CJK space-joining, RTL reversal | `OcrExtensions.GetTextFromOcrLine`; RTL branch in `ImageMethods.ExtractText` |
| Table-mode output | `OcrExtensions.GetRegionsTextAsTableAsync`; `Models/ResultTable.cs`, `Models/WordBorder.cs` |
| Single-line output toggle | `Helpers/StringHelpers.cs` `MakeStringSingleLine` |
| Per-monitor DPI + absolute window position | `Helpers/WPFExtensionMethods.cs` `GetDpi`, `GetAbsolutePosition`; `Helpers/OSInterop.cs` |
| Settings (default `ActivationShortcut = "Win + Shift + O"`, `PreferredLanguage`), file watcher, module name `TextExtractor` | `PowerOCR/Settings/UserSettings.cs`, `Settings/IUserSettings.cs`, ``Settings/SettingItem`1.cs`` |
| Cursor clipping during drag-select | `Helpers/CursorClipper.cs` |
| Telemetry (invoked / capture / cancelled) | `PowerOCR/Telemetry/PowerOCR*Event.cs` |
| UI tests | `PowerOCR-UITests/PowerOCRTests.cs` |

**Activation has two independent code paths** (confirm which one a bug hits): when Runner launches
PowerOCR it passes the Runner PID and uses the **centralized Runner hotkey** (`dllmain.cpp::on_hotkey`
→ sets the shared invoke event → `EventMonitor`); when started detached it installs its **own**
`GlobalKeyboardHook` via `KeyboardMonitor`. `App.xaml.cs::Application_Startup` chooses the path by
whether a Runner PID arg is present.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### OCR language pack not installed (offline / standalone)
- **Symptom:** "No possible OCR languages are installed" message box, or empty result; happens on
  fresh/offline machines and non-English SKUs.
- **Where:** `ImageMethods.GetOCRLanguage` / `ExtractText`; `OcrEngine.AvailableRecognizerLanguages`,
  `OcrEngine.TryCreateFromLanguage`.
- **Root cause:** OCR relies on **Windows' installed OCR language packs**
  ([`Windows.Media.Ocr`](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine)) —
  PowerToys ships none. No pack → `AvailableRecognizerLanguages` is empty and `TryCreateFromLanguage`
  can return `null`.
- **Guardrail:** never assume a language is installed; null-check `TryCreateFromLanguage`, handle the
  empty-list case without crashing, and surface an actionable "install a language pack" hint. Evidence:
  issues [#46030](https://github.com/microsoft/PowerToys/issues/46030),
  [#41969](https://github.com/microsoft/PowerToys/issues/41969).

### Wrong / non-default recognition language
- **Symptom:** Text Extractor doesn't default to the OS/system language; only the native language is
  offered even on an English OS.
- **Where:** `ImageMethods.GetOCRLanguage` (keys off `InputLanguageManager.Current.CurrentInputLanguage`
  = the current **keyboard input** language), overridden by `UserSettings.PreferredLanguage` when set.
- **Root cause:** input language ≠ OS display language ≠ user `PreferredLanguage`; the three are
  conflated. When no exact tag matches, it falls back to abbreviated-name match then first available.
- **Guardrail:** resolve language as `PreferredLanguage` (if set) → matched installed language →
  first available; don't silently pick a language whose tag doesn't match the request. Evidence:
  issues [#42904](https://github.com/microsoft/PowerToys/issues/42904),
  [#47137](https://github.com/microsoft/PowerToys/issues/47137).

### Multi-monitor / per-monitor-DPI overlay misalignment
- **Symptom:** overlay on the wrong screen, selection offset from the cursor, "all windows pulled to
  one screen", increasing latency with multiple displays, misalignment after a screen shift.
- **Where:** `WindowUtilities.LaunchOCROverlayOnEveryScreen` (one `OCROverlay` per `Screen.AllScreens`
  with `screen.GetDpi()`), `OCROverlay` ctor (Width/Height = `bounds / dpiScale`), `OCROverlay.Window_Loaded`
  (double `MoveWindow` +1/-1), region math in `RegionClickCanvas_MouseUp` (`TransformToDevice` `m.M11/M22`).
- **Root cause:** WPF window coordinates are DIPs while `Screen.Bounds` and captured bitmaps are
  physical pixels; per-monitor DPI must be applied per screen, and WPF needs a nudge to pick up the
  new monitor's DPI.
- **Guardrail:** always divide physical bounds by the screen's `DpiScale` for WPF sizing and multiply
  selection coordinates back by the device transform for capture; **do not remove** the deliberate
  double `MoveWindow` (the +1/-1 forces `WM_DPICHANGED` so WPF updates Top/Left/Width/Height). Test on
  a mixed-DPI multi-monitor setup. Evidence: issues
  [#46852](https://github.com/microsoft/PowerToys/issues/46852),
  [#46088](https://github.com/microsoft/PowerToys/issues/46088),
  [#43024](https://github.com/microsoft/PowerToys/issues/43024),
  [#41930](https://github.com/microsoft/PowerToys/issues/41930).

### Activation shortcut not firing / conflicts / cleared shortcut still conflicts
- **Symptom:** custom shortcut doesn't launch; a cleared shortcut still causes a system conflict;
  activation works only for some focused windows; shortcut editor "flashes" while held.
- **Where:** **two paths** — Runner centralized hotkey `dllmain.cpp::get_hotkeys`/`on_hotkey`
  (`parse_hotkey` falls back to Win+Shift+**T** when no key set), and standalone
  `KeyboardMonitor.Hook_KeyboardPressed` (matches sorted key-name lists, sets `e.Handled = true` to
  swallow the combo).
- **Root cause:** the two activation mechanisms are maintained separately; the standalone low-level
  hook matches on string key names and must be re-derived when the setting changes; an empty/cleared
  shortcut must fully disarm the hook.
- **Guardrail:** keep both paths behaviorally in sync; when `ActivationShortcut` is empty, install
  **no** activation match; verify `e.Handled` swallows only the exact combo. Evidence: issues
  [#44914](https://github.com/microsoft/PowerToys/issues/44914),
  [#44505](https://github.com/microsoft/PowerToys/issues/44505),
  [#48785](https://github.com/microsoft/PowerToys/issues/48785),
  [#43791](https://github.com/microsoft/PowerToys/issues/43791) /
  [#43250](https://github.com/microsoft/PowerToys/issues/43250).

### Capture blank / crash depending on focused window (STA + composition)
- **Symptom:** works on some windows but not others; `COMException (0x80263001)` on capture;
  clipboard occasionally not set.
- **Where:** `ImageMethods` `CopyFromScreen`/OCR calls, `OCROverlay.RegionClickCanvas_MouseUp`
  `Clipboard.SetText` (wrapped in try/catch).
- **Root cause:** `0x80263001` is `DWM_E_COMPOSITIONDISABLED`; OCR and clipboard require an STA thread
  and DWM composition. UI tests capture clipboard only via an explicit STA thread helper.
- **Guardrail:** run OCR/clipboard on the STA UI thread; never let a capture/clipboard exception crash
  the overlay (catch + log). Evidence: issues
  [#42784](https://github.com/microsoft/PowerToys/issues/42784),
  [#44069](https://github.com/microsoft/PowerToys/issues/44069).

### Small-region OCR preprocessing (PadImage) + GDI+ bitmap disposal ordering
- **Symptom:** OCR of a very small selection (a single short word / tiny region) returns nothing or is
  unreliable; and/or intermittent GDI+ `ArgumentException`/`ExternalException` in the image path.
- **Where:** `ImageMethods.PadImage` (`internal static bool PadImage(Bitmap image,
  [NotNullWhen(true)] out Bitmap? paddedBitmap, int minW = 64, int minH = 64)`), called by
  `ImageMethods.GetRegionAsBitmap` and `GetWindowBoundsImage`.
- **Root cause / mechanism:** the OCR engine needs a minimum image size, so `PadImage` up-pads any
  bitmap smaller than 64×64 — it allocates a `max(W+16, minW+16) × max(H+16, minH+16)` bitmap, clears
  it with the source's corner pixel color (`image.GetPixel(0,0)`) and draws the original unscaled at
  offset (8,8). [PR #44906](https://github.com/microsoft/PowerToys/pull/44906) refactored it from
  *always returning a new Bitmap* to a **bool + `out` + `[NotNullWhen(true)]`** contract (a `TryPad`
  shape): it returns `false` (and `paddedBitmap = null`) when the image is already big enough, so no
  bitmap is allocated. Callers only replace **and dispose** the original when `PadImage` returns
  `true` — fixing the dispose-ordering leak where a `Graphics` created from a `Bitmap` was disposed
  after its backing bitmap.
- **Guardrail:** preserve the `bool`/`out`/`[NotNullWhen(true)]` contract — treat `PadImage` like
  `TryPad`; the caller disposes the original bitmap **only** when it returns `true` and swaps in the
  padded one. Don't reintroduce an always-allocate return. Scope any `Graphics` strictly inside its
  backing `Bitmap`'s lifetime. Evidence: [PR #44906](https://github.com/microsoft/PowerToys/pull/44906).

## Review Rules

Enforce these when reviewing or authoring Text Extractor changes:

- **Never assume an OCR language is installed.** Guard `OcrEngine.AvailableRecognizerLanguages`
  (empty) and `OcrEngine.TryCreateFromLanguage` (null) — offline machines have no packs
  ([Windows.Media.Ocr](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine); #46030).
- **Respect `OcrEngine.MaxImageDimension`.** Upscaling is gated to 1.5× only when the result stays
  under the cap (`ImageMethods.ExtractText`, `OcrExtensions.GetRegionsTextAsTableAsync`); don't remove the gate.
- **Handle per-monitor DPI at the boundary.** Convert physical↔DIP with the screen `DpiScale` /
  device transform; preserve the `MoveWindow` coercion in `OCROverlay.Window_Loaded` (#46852, #43024).
- **Keep the two activation paths in sync.** A change to the standalone `GlobalKeyboardHook`
  (`KeyboardMonitor`) or the Runner centralized hotkey (`dllmain.cpp`) must be mirrored/considered in
  the other (#44914, #48785).
- **Clipboard/OCR on STA, never crash on failure.** Wrap `Clipboard.SetText` and capture in try/catch
  and keep them on the STA UI thread ([STA/MTA](https://learn.microsoft.com/en-us/windows/win32/com/single-threaded-apartments); #42784).
- **Route text assembly through the language-aware helpers.** Use `OcrExtensions.GetTextFromOcrLine`
  + `LanguageHelper.IsLanguageSpaceJoining` for CJK spacing and the RTL reversal path; don't naively
  `string.Join` words.
- **Honor the GPO gate in both entry points.** `GetConfiguredTextExtractorEnabledValue() == Disabled`
  must exit in `App.xaml.cs`, and the module interface must respect enablement.
- **Scope `Graphics` within its backing `Bitmap`.** Avoid dispose-ordering GDI+ faults
  ([PR #44906](https://github.com/microsoft/PowerToys/pull/44906)).
- **No bare relative paths in project files.** Use `$(RepoRoot)`, not `..\..\..\`
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).
- **Mind the name split.** Directory/namespace/class are `PowerOCR`; settings folder, logs, and
  product name are `TextExtractor` (`UserSettings.PowerOcrModuleName = "TextExtractor"`,
  `Logger.InitializeLogger("\\TextExtractor\\Logs")`). Keep both consistent.

## Gotchas

- **Never** assume OCR languages exist — `Windows.Media.Ocr` uses OS language packs, not anything
  PowerToys ships; an offline PC yields "No possible OCR languages are installed" (#46030, #41969).
- **Input language ≠ OS display language ≠ `PreferredLanguage`.** `GetOCRLanguage` keys off the
  current **keyboard input** language; this surprises users (#42904, #47137).
- **The overlay is one `Window` per `Screen.AllScreens`** — each needs its own `DpiScale`; mixing DPI
  without it misaligns selection or throws the overlay onto the wrong monitor (#46088, #43024).
- **Do not remove the double `MoveWindow` (+1/-1) in `Window_Loaded`** — it deliberately triggers
  `WM_DPICHANGED` so WPF updates `Top/Left/Width/Height`. It looks redundant; it isn't.
- **Two activation code paths** (Runner centralized hotkey via shared event + standalone
  `GlobalKeyboardHook`). Fixing one and forgetting the other is the classic shortcut-bug regression.
- **OCR + clipboard require STA** and DWM composition; `COMException 0x80263001` is
  `DWM_E_COMPOSITIONDISABLED`, not a PowerToys bug per se (#42784).
- **`GC.Collect()` calls are intentional** throughout `ImageMethods` to release large bitmaps
  promptly — don't "clean them up" without understanding the memory pressure they address.
- **Single-instance mutex** `Local\PowerToys_PowerOCR_InstanceMutex` — a second launch exits silently;
  don't break or rename it.

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim these playbooks and then hunt the diff for their themes —
that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

When localizing a bug, if the symptom doesn't map cleanly to a row above, reason from the symptom and
verify in source — a thin/absent map entry can anchor you onto a confident, wrong file.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + notes.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Text Extractor PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/PowerOCR/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/PowerOCR)
- [Windows.Media.Ocr.OcrEngine](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr.ocrengine) ·
  [STA/MTA](https://learn.microsoft.com/en-us/windows/win32/com/single-threaded-apartments) ·
  [Per-Monitor DPI](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
