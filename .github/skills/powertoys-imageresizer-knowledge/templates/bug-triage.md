# ImageResizer Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / inputs (format, size preset, Fit mode):**
- **OS / build / Win10 vs Win11:**
- **Entry point (context menu vs CLI vs UI editor):**
- **Settings relevant (JPEG quality, Remove metadata, Ignore orientation, Overwrite):**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| JPEG quality % has no effect | `ResizeOperation.cs` `EncodeToStreamAsync`/`GetEncoderPropertySet` | Wrong encode path | JPEG quality |
| PNG interlace / TIFF compression ignored | `ResizeOperation.cs` `GetEncoderPropertySet`, `MapTiffCompression` | Options on transcode | JPEG quality / Settings |
| Saved settings revert / sizes read 0 | `Settings.cs`, `ResizeSize.cs`, `AiSize.cs` (`[property: JsonPropertyName]`) | JSON attr forwarding | Settings round-trip |
| EXIF/metadata lost after resize | `ResizeOperation.cs` `TranscodeAsync`/`CopyKnownMetadataAsync`, `KnownMetadataProperties` | Metadata drop | EXIF/metadata |
| GPS not stripped with Remove metadata | `ResizeOperation.cs` `RenderingMetadataProperties`, `FreshEncodeAsync` | Over-preservation | EXIF/metadata |
| Output rotated / wrong portrait-landscape | `ResizeOperation.cs` `CalculateDimensions` (swap), `EncodeFramesAsync` | Orientation | Orientation |
| HEIC / WebP fails or falls back to JPEG | `CodecHelper.cs` `GetEncoderIdForDecoder`/`CanEncode` | Missing encoder | HEIC/WebP |
| Wrong output size (Fit/Fill/%, DPI, ShrinkOnly) | `ResizeOperation.cs` `CalculateDimensions`; `ResizeSize.cs` `ConvertToPixels` | Dimension math | (Module Map) |
| Bad/duplicate output filename, reserved name | `ResizeOperation.cs` `GetDestinationPath` (`_avoidFilenames`) | Filename sanitize | Reserved filename |
| Overwrite loses original / no backup | `ResizeOperation.cs` `ExecuteAsync` (`File.Replace`), `GetBackupPath` | Replace path | (Module Map) |
| Context menu missing after update / Win11 | `dll/dllmain.cpp` enable/UpdateRegistration; `RuntimeRegistration.h`; `ContextMenuHandler.cpp` | Registration lifecycle | Context menu |
| Editor stalls / hangs opening picker | `ui/ImageResizerXAML/MainWindow.xaml.cs`; ViewModels | UI-thread block / async void | Review Rules |
| Settings change while editor open ignored | `Settings.cs` `InitializeWatcher`/`Reload` | Watcher/dispatcher | Review Rules |
| CLI telemetry wrong / missing | `ImageResizerCLI/Program.cs`; `ui/Cli/*` | Telemetry placement | Review Rules |
| Files skipped in batch | `ResizeBatch.cs` `IsValidImagePath`, `ValidImageExtensions` | Input validation | (Module Map) |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
3. Reproduce with the reporter's inputs (note format, size preset, and relevant settings).
4. Add/extend a unit test in `src/modules/imageresizer/tests` before fixing.
