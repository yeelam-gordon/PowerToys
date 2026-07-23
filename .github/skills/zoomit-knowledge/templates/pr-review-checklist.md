# ZoomIt PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces. Source root: `src/modules/ZoomIt/`.

## General (any ZoomIt PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] New end-user strings are localizable — Settings-UI strings in `src/settings-ui/Settings.UI/Strings/en-us/Resources.resw`; native ZoomIt strings in the module `.rc` / `resource.h` (ZoomIt is native C++ and has no module-local `Resources.resw`). Sentence casing, no hard-coded literals.
- [ ] New settings round-trip through `ZoomItSettings.h RegSettings[]` + `ZoomItViewModel.cs` /
      `ZoomItSettingsInterop`, and survive `WM_USER_RELOAD_SETTINGS`.

## Hotkeys (`Zoomit.cpp` — `RegisterAllHotkeys` + 3 mirror sites)
- [ ] Change mirrored across **all four** sites: `RegisterAllHotkeys`, `OptionsProc` validation,
      `MainWndProc` startup, `WM_USER_RELOAD_SETTINGS`.
- [ ] Every XOR-derived modifier (`^ MOD_SHIFT`, `^ MOD_ALT`) guarded with `!= 0` before registering (#47388).
- [ ] Derived modifier extracted into a named local (`cropMod`/`windowMod`) with explicit `!= 0`, not
      inlined in both the `if` and the `RegisterHotKey` call.
- [ ] Toggle vs Save hotkeys registered/validated independently; save skipped when VK=0; conflicts
      attributed to the correct hotkey (#49075).
- [ ] Uses the `registerHotkey` logging helper, not raw `RegisterHotKey`.
- [ ] Default bindings avoid Ctrl+Alt (AltGr) collisions; validated on a non-US layout (#48377, #47491).

## Recording (`VideoRecordingSession.cpp`, `GifRecordingSession.cpp`, `AudioSampleGenerator.cpp`, `StartRecordingAsync`)
- [ ] Session create/close ordering explicit; on failure `Close()` + null the shared_ptr (#48685, #46006).
- [ ] Audio `InitializeAsync` joined before first sample use; failure path fully tears down (#48685).
- [ ] MP4 and GIF paths both exercised; record→trim→record-again works (#48368, #47773, #46006).
- [ ] `GetUniqueRecordingFilename`: only default name gets timestamp; custom names keep digits, `(n)`
      only on collision (#43236).

## Webcam / audio filters (`WebcamCapture.cpp`, `BackgroundBlur.cpp`, `NoiseSuppressor.cpp`)
- [ ] Overlay geometry correct at fractional scaling (<100%) and for non-rectangular cutouts (#48508, #48529).
- [ ] Background-blur model (`selfie_segmentation.onnx`) / RNNoise paths guarded when unavailable.

## Zoom / Draw / Live Zoom (`MainWndProc`, Magnification API, `DrawBlurredShape`/`DrawHighlightedShape`)
- [ ] Magnification pointers (`pMagSetWindowSource/Transform`) null-checked before use.
- [ ] Cursor visibility restored on every mode-exit path (#48823, #47736).
- [ ] Pen-width scaling by `g_LiveZoomLevel` preserved for LiveDraw.

## DPI / dialog (`GetDpiForWindowHelper`, `ScaleDialogForDpi`, `WM_DPICHANGED`)
- [ ] New dialog controls participate in DPI scaling; no mis-size at 125%/150% (#48367, #48188).
- [ ] Target monitor resolved via `MonitorFromPoint`; no primary-monitor / 96-DPI assumption.

## OCR / Snip / Panorama (`OcrFromHBITMAP`, `SelectRectangle.cpp`, `PanoramaCapture.cpp`)
- [ ] OCR handles `MaxImageDimension` and missing OCR language gracefully.
- [ ] Panorama capture doesn't block user input on conflict (#47154).
