# ImageResizer Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each
claim in source before acting. Symptoms map to `src/modules/imageresizer/`.

## Key Decisions (context for the playbooks)

- **Migrated to WinUI 3.** [PR #45288](https://github.com/microsoft/PowerToys/pull/45288) rewrote the
  editor from WPF to WinUI 3 (much AI-generated, then hand-reviewed). This is the root of the
  JPEG-quality, PNG-encoder, and settings-JSON regressions below — behaviors that WPF handled were
  re-implemented on the WinRT/WIC APIs. Maintainers standardize on **WinUIEx** for windowing.
- **WIC encode: transcode vs fresh-encode.** `EncodeToStreamAsync` transcodes (re-encode via
  `BitmapTransform`, all metadata preserved) only when output codec == input codec, `RemoveMetadata`
  is false, and `forceFresh` is false. If not, it fresh-encodes and carries over only
  `KnownMetadataProperties`. JPEG resize forces fresh encode so `ImageQuality` applies — a deliberate
  metadata/quality trade-off (`ResizeOperation.cs`).
- **Metadata handling is best-effort + explicit.** Known props (DateTaken, CameraModel/Manufacturer,
  Orientation, ColorSpace, Comment) are re-read/re-written in try/catch; formats that don't support
  `BitmapProperties` (BMP) are ignored. `RemoveMetadata` keeps only orientation + colorspace
  (rendering-critical).
- **Codec set is fixed; unknown decoders fall back.** `CodecHelper` maps JPEG/PNG/BMP/TIFF/GIF/JXR
  only; a decoder with no matching encoder (ICO, HEIF/WebP without Store codec) uses
  `FallbackEncoder` (default JPEG container GUID `19e4a5aa-...`).
- **Settings snapshot per batch.** `ResizeBatch.ProcessAsync` captures `Settings.Default` once before
  `Parallel.ForEachAsync`; mid-batch edits don't apply until the next run — a deliberate
  predictability/perf choice.
- **Live settings reload.** [PR #45266](https://github.com/microsoft/PowerToys/pull/45266) added a
  debounced `FileSystemWatcher` that reloads settings and marshals property updates onto the UI
  dispatcher (`ReloadCore`). Also migrates the legacy settings directory on `Reload`.
- **`%`-token filename format.** `Settings.FileName` (default `"%1 (%2)"`) is converted to a
  composite `{0..5}` format; `%1`=original name, `%2`=size name, `%3/%4`=selected W/H, `%5/%6`=output
  W/H. Output is sanitized for illegal/reserved names.
- **CLI + telemetry.** [PR #46872](https://github.com/microsoft/PowerToys/pull/46872) added CLI
  telemetry; command name must reflect the real op and be logged before returning/terminating the
  process. Migrated toward MTP test tooling ([PR #37651](https://github.com/microsoft/PowerToys/pull/37651), closed stale).

## Regression Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| JPEG quality | Quality % has no effect after WinUI3 | `ResizeOperation.cs` `EncodeToStreamAsync`/`GetEncoderPropertySet` | Transcode path ignores codec options | Force fresh encode for JPEG (`forceFresh`); apply options in `CreateFreshEncoderAsync`; test 2 quality levels | [#47135](https://github.com/microsoft/PowerToys/issues/47135), [#45484](https://github.com/microsoft/PowerToys/issues/45484), [PR #47134](https://github.com/microsoft/PowerToys/pull/47134) |
| Settings round-trip | Sizes/quality/PNG settings revert or read 0 | `Settings.cs`, `ResizeSize.cs`, `AiSize.cs` | `[ObservableProperty]` didn't forward `[JsonPropertyName]`; PNG opts unplumbed | `[property: JsonPropertyName]`; thread PNG opts; round-trip tests incl. `AiSize.Scale` | [#47055](https://github.com/microsoft/PowerToys/issues/47055), [#45484](https://github.com/microsoft/PowerToys/issues/45484), [PR #47056](https://github.com/microsoft/PowerToys/pull/47056), [PR #46695](https://github.com/microsoft/PowerToys/pull/46695) |
| EXIF/metadata | EXIF lost on resize (Remove metadata OFF) | `ResizeOperation.cs` `TranscodeAsync`/`CopyKnownMetadataAsync` | Transcode drops EXIF for large/unusual metadata blocks | Re-set `KnownMetadataProperties` after transcode; best-effort try/catch | [#47693](https://github.com/microsoft/PowerToys/issues/47693) |
| EXIF/metadata | GPS not stripped with Remove metadata ON | `ResizeOperation.cs` `RenderingMetadataProperties`, `FreshEncodeAsync` | Fresh encode keeps only orientation+colorspace; GPS not present but source copy path leaked | Verify only rendering-critical props survive; test GPS removal | [#46317](https://github.com/microsoft/PowerToys/issues/46317) |
| Orientation | Rotated/incorrectly sized portrait/landscape | `ResizeOperation.cs` `CalculateDimensions` (swap), `EncodeFramesAsync` | Missing target W/H swap; double EXIF rotation | Swap when orientation mismatch; keep `IgnoreExifOrientation` on read | (see `Settings` default `IgnoreOrientation=true`) |
| HEIC/WebP | HEIC/WebP won't save or falls back | `CodecHelper.cs` `GetEncoderIdForDecoder`/`CanEncode` | No built-in WIC encoder without Store codec | Extend codec maps together; degrade gracefully | [#47840](https://github.com/microsoft/PowerToys/issues/47840), [#45474](https://github.com/microsoft/PowerToys/issues/45474), [#46665](https://github.com/microsoft/PowerToys/issues/46665) |
| Context menu | Entry missing after update / Win11 | `dll/dllmain.cpp` enable/UpdateRegistration; `RuntimeRegistration.h`; `ContextMenuHandler.cpp` | Sparse-MSIX version-check/registration lifecycle | Idempotent, version-aware register; honor GPO/enabled at ctor | [#45521](https://github.com/microsoft/PowerToys/issues/45521), [#43782](https://github.com/microsoft/PowerToys/issues/43782), [#45458](https://github.com/microsoft/PowerToys/issues/45458) |
| Reserved filename | Bad/duplicate output name | `ResizeOperation.cs` `GetDestinationPath` (`_avoidFilenames`) | User-controlled tokens → reserved/illegal names | Sanitize chars→`_`, append `_` for reserved, de-duplicate `(n)` | (source-verified; [naming a file](https://learn.microsoft.com/windows/win32/fileio/naming-a-file)) |
| UI-thread / a11y | Editor stalls; icon buttons unnamed; hardcoded sizes | `ui/ImageResizerXAML/*`, `ui/ViewModels/*` | Blocking picker call; missing `AutomationProperties.Name`; non-ThemeResource colors | Async view contract; add automation names; ThemeResource | [PR #45288](https://github.com/microsoft/PowerToys/pull/45288), [PR #47752](https://github.com/microsoft/PowerToys/pull/47752) |
| CLI telemetry | Wrong/missing telemetry command | `ImageResizerCLI/Program.cs`; `ui/Cli/*` | Hardcoded `"resize"`; logged after process return/termination | Derive command from parsed options; log before exit | [PR #46872](https://github.com/microsoft/PowerToys/pull/46872) |

## Common Practices (enforced in review)

- **Encoder options only on fresh encode.** `GetEncoderPropertySet` is used solely by
  `CreateFreshEncoderAsync`; never assume the transcode path applies JPEG/PNG/TIFF settings (#47134).
- **Best-effort metadata.** Keep `ReadMetadataAsync`/`WriteMetadataAsync` in try/catch; add preserved
  fields to `KnownMetadataProperties` with tests (#47693, #46317).
- **JSON forwarding for generated props.** Always `[property: JsonPropertyName]` on
  `[ObservableProperty]` fields; add round-trip tests, not just `Name`/`Count` (#47056).
- **Per-batch settings snapshot.** Don't re-read settings inside `Parallel.ForEachAsync`
  (`ResizeBatch.ProcessAsync`).
- **Build hygiene.** On CppWinRT/.NET bumps attach clean-build evidence (x64+ARM64, Debug+Release);
  quote MSBuild PowerShell path args; don't blanket-suppress script warnings (PR #45420, #41280, #46729).
- **Testing.** Regressions ship with tests in `src/modules/imageresizer/tests`
  (`ResizeOperationTests`, `ResizeSizeTests`, `ResizeBatchTests`, `SettingsTests`, `tests/Cli/*`).

## Anti-anchoring note (source-verified)

On [PR #47134](https://github.com/microsoft/PowerToys/pull/47134), Copilot review comments claimed
`forceFresh` drops metadata and that codec options are no longer applied on transcode; maintainer
`moooyo` marked both **"incorrect."** The fresh-encode path is intentional and does carry known
metadata via `CopyKnownMetadataAsync`. Verify claims against source before acting on them.

---
*Corpus: 12 merged/closed PRs, 67 review comments, 76 conversation comments, 30 bug issues +
source verification against `src/modules/imageresizer`.*
