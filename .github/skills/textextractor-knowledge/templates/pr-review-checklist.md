# Text Extractor (PowerOCR) — PR Review Checklist

Apply **after** reading the diff cold (see anti-anchoring in SKILL.md). Only check rows whose code
paths the diff actually touches.

## OCR pipeline (`ImageMethods.cs`, `OcrExtensions.cs`)
- [ ] `OcrEngine.TryCreateFromLanguage` result null-checked; empty `AvailableRecognizerLanguages` handled without crash (#46030, #41969).
- [ ] Upscaling still gated by `OcrEngine.MaxImageDimension` (1.5× only under the cap).
- [ ] Text assembly goes through `GetTextFromOcrLine` + `IsLanguageSpaceJoining`; RTL reversal path preserved.
- [ ] **Known current violation checked:** `Graphics` is scoped within its backing `Bitmap`'s
      lifetime; PR #44906 established the padding try-pattern, not this disposal ordering.
- [ ] Bitmap disposal / `GC.Collect()` intent preserved for large captures.

## Language selection
- [ ] Resolution order = `PreferredLanguage` (if set) → matched installed language → first available.
- [ ] Not silently conflating keyboard **input** language, OS display language, and setting (#42904, #47137).

## Multi-monitor / DPI (`WindowUtilities.cs`, `OCROverlay.xaml.cs`, `WPFExtensionMethods.cs`)
- [ ] One overlay per `Screen.AllScreens`, each with its own `screen.GetDpi()`.
- [ ] WPF sizing converts physical bounds ↔ DIP via `DpiScale`; selection coords multiplied by device transform for capture.
- [ ] Double `MoveWindow` (+1/-1) in `Window_Loaded` untouched (forces `WM_DPICHANGED`).
- [ ] Verified on a mixed-DPI, multi-monitor layout (#46852, #46088, #43024, #41930).

## Activation shortcut (two paths)
- [ ] Runner centralized hotkey (`dllmain.cpp` `get_hotkeys`/`on_hotkey`/`parse_hotkey`) and standalone `KeyboardMonitor`/`GlobalKeyboardHook` kept in sync (#44914, #48785).
- [ ] Empty/cleared `ActivationShortcut` installs no match / disarms the hook.
- [ ] `e.Handled = true` swallows only the exact activation combo.

## Threading / clipboard / lifecycle
- [ ] OCR + `Clipboard.SetText` on the STA UI thread; both wrapped in try/catch + log (#42784).
- [ ] GPO gate honored in `App.xaml.cs` and module interface.
- [ ] Single-instance mutex `Local\PowerToys_PowerOCR_InstanceMutex` intact.
- [ ] Name split respected: `PowerOCR` (code) vs `TextExtractor` (settings folder, logs, product).

## Build hygiene
- [ ] Project files use `$(RepoRoot)`, not bare `..\..\..\` (PR #44639).
- [ ] Language-aware / capture changes covered by a test in `PowerOCR-UITests` where feasible.
