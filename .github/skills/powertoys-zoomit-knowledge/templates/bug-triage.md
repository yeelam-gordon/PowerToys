# ZoomIt Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table. Source root:
`src/modules/ZoomIt/` (unqualified files live in `ZoomIt/`).

## Report
- **Symptom:**
- **Repro / inputs:**
- **Keyboard layout (US / non-US / AltGr):**
- **Monitors / DPI / scaling:**
- **Mode:** zoom / live zoom / draw / break / demotype / record (MP4/GIF) / webcam / snip / OCR / panorama
- **Launched via:** PowerToys / standalone ZoomIt.exe

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Bare key (e.g. `5`) triggers window recording | `RegisterAllHotkeys` `windowMod = g_RecordToggleMod ^ MOD_ALT` | XOR-zero modifier | XOR modifier collision |
| A record variant never registers | 4 registration sites; XOR guards | Missing mirror / XOR | XOR modifier collision |
| "Snip hotkey already in use" after clearing Save field | `OptionsProc`/startup/reload `SNIP_SAVE_HOTKEY` | VK=0 coupled register | Toggle/Save coupling |
| Shortcut hijacks AltGr typing (non-US) | default bindings; `WM_HOTKEY`; `GetKeyMod` | Ctrl+Alt collision | International/AltGr |
| MP4 record crashes / orange frame vanishes | `StartRecordingAsync`, `VideoRecordingSession::Create` | Lifecycle race | Recording lifecycle |
| GIF partial capture stuck | `GifRecordingSession::Create`, teardown | Lifecycle | Recording lifecycle |
| Trim then re-record fails to save | session reset (`g_RecordingSession` null) | Teardown | Recording lifecycle |
| Audio device intermittently not opened | `AudioSampleGenerator::InitializeAsync` (early start) | Audio init race | Recording lifecycle |
| User filename digits stripped / overwrite risk | `GetUniqueRecordingFilename`, `IsDefaultRecordingFilename` | Filename suffix | Filename suffixes |
| Cursor disappears / invisible on 2nd display | DPI + `MonitorFromPoint`; cursor show/hide | Cursor/DPI | Cursor/overlay visibility |
| Webcam overlay mispositioned at <100% scale | `WebcamCapture.cpp`, `WebcamComposite.hlsl` | DPI geometry | Cursor/overlay visibility |
| Options dialog controls mis-sized at high DPI | `ScaleDialogForDpi`, `WM_DPICHANGED` | DPI scaling | Cursor/overlay visibility |
| OCR returns nothing / fails | `OcrFromHBITMAP` (`MaxImageDimension`, OCR language) | OCR | Module Map |
| Panorama popup blocks input | `PanoramaCapture.cpp`; `WM_HOTKEY` panorama guard | Input block | Module Map (#47154) |
| Ctrl+S not editable in settings | `ZoomItViewModel.cs`; `RegSettings[]` | Settings bind | Module Map (#46938) |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. If it's a hotkey issue, check **all four** registration sites, not just the first hit.
3. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
4. Reproduce with the reporter's inputs (layout, monitor/DPI, mode, record format).
5. Validate MP4 + GIF and non-US layout where relevant before fixing.
