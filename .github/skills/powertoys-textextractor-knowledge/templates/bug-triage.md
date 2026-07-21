# Text Extractor (PowerOCR) — Bug Triage

Map a symptom to the most likely file/function via the Module Map. Treat as a **hypothesis to
confirm in source**, not ground truth (a thin map row can anchor you onto a wrong file).

| Symptom | Start here | Notes |
|---|---|---|
| "No possible OCR languages are installed" / empty result | `ImageMethods.GetOCRLanguage`, `ExtractText`; `OcrEngine.AvailableRecognizerLanguages` | OS language pack missing (offline/standalone). #46030, #41969 |
| Wrong language / doesn't match OS/system language | `ImageMethods.GetOCRLanguage`; `UserSettings.PreferredLanguage` | Uses keyboard **input** language, not OS display language. #42904, #47137 |
| Overlay on wrong monitor / selection offset / "windows pulled to one screen" | `WindowUtilities.LaunchOCROverlayOnEveryScreen`, `OCROverlay` ctor + `Window_Loaded` | Per-monitor DPI + physical↔DIP conversion. #46852, #46088, #43024, #41930 |
| Capture region mis-aligned from cursor | `OCROverlay.RegionClickCanvas_MouseUp` (`TransformToDevice` `m.M11/M22`) | Device-transform scaling of selection rect. |
| Custom shortcut doesn't launch / cleared shortcut still conflicts | `KeyboardMonitor.Hook_KeyboardPressed`, `SetActivationKeys`; `dllmain.cpp` `parse_hotkey`/`on_hotkey` | Two activation paths — check which. #44914, #44505, #48785 |
| Shortcut editor flashes while held | activation combo `e.Handled` handling; settings shortcut UI | #43791 / #43250 |
| Capture blank on some windows / `COMException 0x80263001` | `ImageMethods` `CopyFromScreen`/OCR; STA thread | `DWM_E_COMPOSITIONDISABLED`; STA + composition required. #42784, #44069 |
| Clipboard not populated | `OCROverlay.RegionClickCanvas_MouseUp` `Clipboard.SetText` (try/catch) | Must run STA. |
| Table output wrong / columns mis-grouped | `OcrExtensions.GetRegionsTextAsTableAsync`, `Models/ResultTable.cs` | #42336 (feature). |
| Single-line toggle wrong | `StringHelpers.MakeStringSingleLine` | |
| CJK words spaced wrongly / RTL order | `OcrExtensions.GetTextFromOcrLine`, `LanguageHelper.IsLanguageSpaceJoining`, RTL branch in `ExtractText` | |
| Won't start / GPO disabled | `App.xaml.cs::Application_Startup` (GPO check, mutex) | |
| Intermittent GDI+ exception in image path | `ImageMethods.PadImage`, `GetRegionAsBitmap` | Graphics/Bitmap dispose ordering. PR #44906 |

## Triage steps
1. Reproduce and note: single vs multi-monitor, DPI scaling, launched via Runner or standalone, language selected.
2. Check logs under `%LOCALAPPDATA%\...\TextExtractor\Logs` (name is `TextExtractor`, not PowerOCR).
3. Localize with the table above, then **confirm in source** before concluding.
