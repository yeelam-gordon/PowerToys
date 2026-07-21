---
name: powertoys-imageresizer-knowledge
description: 'PowerToys ImageResizer module knowledge: feature->file/function map, recurring regression playbooks (WIC transcode-vs-fresh-encode & JPEG quality, EXIF/metadata & orientation preservation, size presets & Fit/Fill/percent math, %-token filename format & reserved-name sanitizing, Win11 sparse-MSIX + Win10 COM context-menu registration, settings JSON round-trip), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/imageresizer — resize/encode, WIC codecs, metadata, presets, CLI, context menu, settings, WinUI editor. Keywords: ImageResizer, resize, WIC, BitmapEncoder, transcode, JPEG quality, EXIF metadata, orientation, PNG interlace, TIFF, HEIC, sparse MSIX, context menu, WinUI3, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys ImageResizer Knowledge

Grounded engineering knowledge for the PowerToys **ImageResizer** module — a bulk image
resizer that plugs into the Windows Explorer context menu and a CLI. It decodes with Windows
Imaging Component (WIC), scales/crops per a chosen size preset (Fit/Fill/Stretch; px/cm/inch/%),
re-encodes (JPEG/PNG/BMP/TIFF/GIF/JXR), preserves or strips metadata, and writes a new file
using a `%`-token name format. Use this to localize code fast, avoid known regression traps, and
enforce conventions the maintainers already established.

The C# UI/model layer is **forked from Brice Lambson's ImageResizer** (MIT) — many `ui/Models/`
files carry that header; keep that lineage in mind when reasoning about design.

## When to Use This Skill

- Planning or implementing a change under `src/modules/imageresizer/` and needing prior art.
- Fixing/triaging an ImageResizer bug: wrong output size, EXIF/metadata lost, orientation wrong,
  JPEG quality ignored, HEIC/WebP not saved, context menu missing, settings not applied.
- Reviewing an ImageResizer PR against maintainer conventions and regression traps.
- Adding an encoder option, a size preset, a CLI option, or touching the WIC encode/decode core.
- Working on context-menu registration (Win10 COM verb, Win11 sparse MSIX, GPO gating).

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Resize/encode core (decode → transform → encode → post) | `ui/Models/ResizeOperation.cs` `ExecuteAsync` |
| Transcode vs fresh-encode decision | `ResizeOperation.cs` `EncodeToStreamAsync` (`canTranscode` = same codec, keep-metadata, no `forceFresh`) |
| Transcode path (preserves all metadata via `CreateForTranscodingAsync`) | `ResizeOperation.cs` `TranscodeAsync` + `CopyKnownMetadataAsync` |
| Fresh-encode path (applies codec options / strips metadata) | `ResizeOperation.cs` `FreshEncodeAsync`, `EncodeFramesAsync` |
| Dimension math (Fit/Fill/Stretch, %, DPI, crop, ShrinkOnly, orientation swap) | `ResizeOperation.cs` `CalculateDimensions` |
| Unit → pixel conversion, `HasAuto`, PositiveInfinity for Fit-auto | `ui/Models/ResizeSize.cs` `ConvertToPixels`, `GetPixelWidth/Height` |
| Size preset model + `$small$/$medium$/$large$/$phone$` name tokens | `ResizeSize.cs` `_tokenKeys`, `ReplaceTokens`; defaults in `ui/Properties/Settings.cs` ctor |
| Encoder options (JPEG `ImageQuality`, PNG `InterlaceOption`, TIFF `TiffCompressionMethod`) | `ResizeOperation.cs` `GetEncoderPropertySet`, `MapTiffCompression` |
| Metadata preservation set (DateTaken/Camera/Orientation/ColorSpace/Comment) | `ResizeOperation.cs` `KnownMetadataProperties`, `RenderingMetadataProperties` |
| Metadata read/write best-effort (format may not support) | `ResizeOperation.cs` `ReadMetadataAsync`, `WriteMetadataAsync` |
| Destination filename (`%1..%6` tokens, reserved-name + illegal-char sanitize, de-duplication) | `ResizeOperation.cs` `GetDestinationPath`; format built in `Settings.cs` `FileNameFormat` |
| Overwrite-in-place (`Replace`) with `.bak` + recycle-bin backup | `ResizeOperation.cs` `ExecuteAsync` (`File.Replace`), `GetBackupPath` |
| Keep-date-modified | `ResizeOperation.cs` `ExecuteAsync` (`SetLastWriteTimeUtc`) |
| Codec ↔ extension mapping, legacy container-GUID → encoder ID, fallback encoder | `ui/Utilities/CodecHelper.cs` |
| Batch orchestration (parallel, per-file errors, stdin/named-pipe/CLI intake) | `ui/Models/ResizeBatch.cs` `ProcessAsync`, `FromCliOptions`, `IsValidImagePath` |
| AI super-resolution size (`AiSize`, Win AI upscale) | `ui/Models/AiSize.cs`; `ui/Services/*AiSuperResolutionService.cs`; `ResizeOperation.cs` `ExecuteAiAsync` |
| Settings model, JSON round-trip, `%`→`{}` format, defaults | `ui/Properties/Settings.cs`; `SettingsWrapper.cs`, `WrappedJsonValueConverter.cs` |
| Live settings reload (debounced FileSystemWatcher → UI dispatcher) | `Settings.cs` `InitializeWatcher`, `Reload`, `ReloadCore` (PR #45266) |
| Settings legacy-dir migration | `Settings.cs` `Reload` (old `LocalAppData\...\ImageResizer` → new dir) |
| CLI (System.CommandLine options, telemetry) | `ui/Cli/*`, `ImageResizerCLI/Program.cs` |
| WinUI 3 editor window + pages/VMs | `ui/ImageResizerXAML/*`, `ui/ViewModels/*` (Input/Progress/Results/MainViewModel) |
| Module interface: enable/disable, GPO gate, sparse-package register | `dll/dllmain.cpp` `ImageResizerModule::enable/disable`, `UpdateRegistration`, `gpo_policy_enabled_configuration` |
| Win10 classic COM context-menu handler | `dll/ContextMenuHandler.cpp` `CContextMenuHandler` |
| Win11 sparse-MSIX runtime (un)registration | `dll/RuntimeRegistration.h` `ImageResizerRuntimeRegistration::EnsureRegistered/Unregister`; `ImageResizerContextMenu/dllmain.cpp` |

**Encode-path invariant (critical):** in `EncodeToStreamAsync`, ImageResizer **transcodes**
(re-encodes via `BitmapTransform`, preserving all metadata) only when the output codec equals the
input codec, `RemoveMetadata` is false, and `forceFresh` is false. If not, it takes the
**fresh-encode** path, which manually carries over only `KnownMetadataProperties`. JPEG resize
forces fresh encode (`forceFresh = JpegEncoderId && transform needed`) so the quality setting
applies — a deliberate metadata/quality trade-off, not a bug (PR #47134).

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### JPEG quality ignored after WinUI3 migration
- **Symptom:** changing JPEG quality % produces identical output; setting had no effect.
- **Where:** `ResizeOperation.cs` `EncodeToStreamAsync`/`GetEncoderPropertySet`.
- **Root cause:** the transcode path (`CreateForTranscodingAsync`) accepts only a `BitmapTransform`;
  codec options like `ImageQuality` never apply there. After the WinUI3 rewrite JPEG resize stayed
  on transcode, so quality was dropped.
- **Guardrail:** force the fresh-encode path for JPEG when a transform is needed (`forceFresh`), and
  apply `GetEncoderPropertySet` only in `CreateFreshEncoderAsync`. Any change to encoder-option
  application must ship a test resizing the same JPEG at two quality levels and asserting different
  output. Evidence: issue [#47135](https://github.com/microsoft/PowerToys/issues/47135),
  [#45484](https://github.com/microsoft/PowerToys/issues/45484); fix
  [PR #47134](https://github.com/microsoft/PowerToys/pull/47134).

### Settings JSON fields silently revert to defaults / all-zero
- **Symptom:** saved size/quality/PNG-encoder settings don't persist; sizes read back as 0; PNG
  interlace/TIFF options lost.
- **Where:** `Settings.cs`, `ResizeSize.cs`, `AiSize.cs` `[ObservableProperty]` fields; PNG option
  plumbing in `ResizeOperation.cs`.
- **Root cause:** the CommunityToolkit MVVM `[ObservableProperty]` source generator did not forward
  `[JsonPropertyName]` onto the generated public property, so System.Text.Json used the wrong names;
  separately, PNG/TIFF encoder settings weren't threaded through after WinUI3.
- **Guardrail:** annotate generated properties with `[property: JsonPropertyName(...)]`; add
  round-trip tests asserting numeric/enum fields (incl. `AiSize.Scale`) survive serialize→deserialize
  — the old `SystemTextJsonDeserializesCorrectly` test only checked `Name`/`Count` and missed this.
  Evidence: issue [#47055](https://github.com/microsoft/PowerToys/issues/47055),
  [#45484](https://github.com/microsoft/PowerToys/issues/45484); fixes
  [PR #47056](https://github.com/microsoft/PowerToys/pull/47056),
  [PR #46695](https://github.com/microsoft/PowerToys/pull/46695).

### EXIF / metadata dropped (even with "Remove metadata" off)
- **Symptom:** resized image loses EXIF; GPS not stripped when it should be; DateTaken/camera gone.
- **Where:** `ResizeOperation.cs` `TranscodeAsync`/`CopyKnownMetadataAsync`,
  `FreshEncodeAsync`/`ReadMetadataAsync`/`WriteMetadataAsync`; property lists `KnownMetadataProperties`.
- **Root cause:** transcode of JPEGs with large/unusual metadata blocks (e.g. big embedded
  thumbnails) can silently drop EXIF; the fresh-encode path only carries the explicit known-property
  set, so anything not listed is lost. `RemoveMetadata` keeps only rendering-critical props
  (`System.Photo.Orientation`, `System.Image.ColorSpace`).
- **Guardrail:** treat metadata copy as best-effort but **re-set critical props explicitly** after
  transcode (`CopyKnownMetadataAsync`); when adding a preservable field, add it to
  `KnownMetadataProperties` AND cover it in tests. Reads/writes must stay in try/catch — some formats
  (BMP) reject property queries. Evidence: issues
  [#47693](https://github.com/microsoft/PowerToys/issues/47693),
  [#46317](https://github.com/microsoft/PowerToys/issues/46317) (GPS not stripped).
  See [WIC metadata](https://learn.microsoft.com/windows/win32/wic/-wic-about-metadata),
  [Photo metadata policies](https://learn.microsoft.com/windows/win32/wic/photo-metadata-policies).

### Orientation wrong / dimensions not swapped
- **Symptom:** portrait/landscape mismatch; EXIF-rotated photos come out sideways or wrongly sized.
- **Where:** `ResizeOperation.cs` `CalculateDimensions` (`canSwapDimensions`),
  `EncodeFramesAsync` (`ExifOrientationMode.IgnoreExifOrientation`).
- **Root cause:** the "Ignore the orientation of pictures" setting must swap target W/H when input
  and target orientation disagree; the frame decode must not double-apply EXIF orientation.
- **Guardrail:** swap only when `IgnoreOrientation && !HasAuto && Unit != Percent`; keep
  `IgnoreExifOrientation` on the pixel read so the encoder doesn't rotate twice. Default
  `IgnoreOrientation = true` (`Settings.cs`). See
  [System.Photo.Orientation](https://learn.microsoft.com/windows/win32/wic/-wic-about-metadata).

### HEIC / WebP output missing or resize fails
- **Symptom:** HEIC/WebP inputs fail to save or "no longer work"; output falls back unexpectedly.
- **Where:** `CodecHelper.cs` `GetEncoderIdForDecoder`/`CanEncode`/`GetEncoderIdFromLegacyGuid`;
  `ResizeOperation.cs` `ExecuteAsync` encoder selection.
- **Root cause:** WIC has no built-in HEIF/WebP *encoder* without the Store codec extensions;
  ImageResizer only maps a fixed encoder set (JPEG/PNG/BMP/TIFF/GIF/JXR). When the decoder has no
  matching encoder (`GetEncoderIdForDecoder` → null) it falls back to `FallbackEncoder` (default JPEG).
- **Guardrail:** when adding a container format, extend `DecoderIdToEncoderId`,
  `EncoderExtensions`, and `LegacyGuidToEncoderId` together and degrade gracefully when the Store
  codec is absent. Evidence: issues
  [#47840](https://github.com/microsoft/PowerToys/issues/47840) (HEIC),
  [#45474](https://github.com/microsoft/PowerToys/issues/45474) (HEIC),
  [#46665](https://github.com/microsoft/PowerToys/issues/46665) (WebP).
  See [HEIF codec](https://learn.microsoft.com/windows/win32/wic/native-wic-codecs).

### Context menu missing after update
- **Symptom:** "Resize with Image Resizer" absent from Explorer after upgrade / on Win11.
- **Where:** `dll/dllmain.cpp` `enable/disable/UpdateRegistration`; `dll/RuntimeRegistration.h`
  (Win11 sparse MSIX); `dll/ContextMenuHandler.cpp` (Win10 verb, gated on `GetEnabled()`).
- **Root cause:** Win11 entry is a sparse MSIX registered at `enable()` only if not already
  registered for the current PowerToys version; registration/version-check lifecycle can leave the
  package unregistered after an update; `UpdateRegistration` is compiled out unless
  `ENABLE_REGISTRATION`/`NDEBUG`.
- **Guardrail:** keep register/unregister idempotent and version-aware
  (`IsPackageRegisteredWithPowerToysVersion`); honor GPO/enabled state at construction. Evidence:
  issues [#45521](https://github.com/microsoft/PowerToys/issues/45521),
  [#43782](https://github.com/microsoft/PowerToys/issues/43782),
  [#45458](https://github.com/microsoft/PowerToys/issues/45458).

### Reserved / invalid destination filename
- **Symptom:** crash or bad output when the name format resolves to a Win32-reserved or
  illegal-character name.
- **Where:** `ResizeOperation.cs` `GetDestinationPath` (`_avoidFilenames`, char replacement).
- **Root cause:** user-controlled `%`-token format + size name can produce `CON`/`PRN`/… or
  `:*?"<>|`.
- **Guardrail:** sanitize illegal chars to `_`, append `_` for reserved names, and de-duplicate with
  ` (n)` when the target exists. Any new token or size-name source must run through this sanitizer.
  See [naming files](https://learn.microsoft.com/windows/win32/fileio/naming-a-file).

## Review Rules

Enforce these when reviewing or authoring ImageResizer changes:

- **Know which encode path you're on.** Codec options (JPEG quality, PNG interlace, TIFF compression)
  apply **only** on the fresh-encode path (`GetEncoderPropertySet` → `CreateFreshEncoderAsync`); the
  transcode path takes a `BitmapTransform` only. Changing this trade-off requires tests at two
  quality/option values (#47134).
- **Metadata copy is best-effort — keep it in try/catch and re-set critical props.** Never assume a
  format supports `BitmapProperties`; BMP and others throw. Add new preserved fields to
  `KnownMetadataProperties` and test them (#47693, #46317).
- **Forward `[JsonPropertyName]` on generated observable properties.** `[ObservableProperty]` does not
  auto-forward JSON attributes; use `[property: JsonPropertyName]` or settings silently revert
  (#47056). Add a round-trip test, not just a `Name`/`Count` assertion.
- **Settings are captured per batch.** `ResizeBatch.ProcessAsync` captures `Settings.Default` once
  before `Parallel.ForEachAsync`; on-disk edits mid-batch are intentionally ignored. Don't add
  per-file settings re-reads inside the parallel loop.
- **Keep the settings watcher debounced and marshalled to the UI thread.** `Reload` must apply via
  the dispatcher queue (`ReloadCore`); direct cross-thread writes to observable properties break the
  WinUI editor (PR #45266).
- **Reject `async void`** in view/VM command paths; blocking the UI thread on WinRT pickers
  (`.AsTask().GetAwaiter().GetResult()`) can stall/deadlock — use an async view contract and `await`
  ([async best practices](https://learn.microsoft.com/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming#avoid-async-void), PR #45288).
- **Any color in XAML must be a `ThemeResource`; no hardcoded control sizes.** Test with large
  Windows text size / high scale; icon-only buttons need `AutomationProperties.Name` (WCAG 1.1.1 /
  4.1.2) (PR #45288, #47752).
- **Keep the WinUIEx dependency** for windowing — maintainers standardize on it across utilities;
  don't strip it for "standard WinUI" (PR #45288, niels9001).
- **CLI telemetry must reflect the real operation and run before process exit.** Don't hardcode the
  command name or log after returning/terminating the process (PR #46872).
- **Ship a test with every fix.** Suites live in `src/modules/imageresizer/tests`
  (`ResizeOperationTests`, `ResizeSizeTests`, `ResizeBatchTests`, `SettingsTests`, CLI tests).

## Pitfalls

- **Never** assume the transcode path preserves EXIF for every JPEG — large/unusual metadata blocks
  drop silently; `CopyKnownMetadataAsync` re-sets known props as a safety net (#47693).
- **Never** expect codec options on the transcode path — JPEG quality/PNG interlace/TIFF compression
  are applied only when a fresh encoder is created (#47134).
- **Never** add `[JsonPropertyName]` to the backing field of `[ObservableProperty]` and expect it to
  work — it must be `[property: JsonPropertyName]` to land on the generated property (#47056).
- **`RemoveMetadata` still keeps orientation & colorspace** (`RenderingMetadataProperties`) — those
  are rendering-critical, not privacy metadata; don't "fix" GPS stripping by dropping them (#46317).
- **HEIC/WebP have no built-in WIC encoder** without Store codec extensions; unmatched decoders fall
  back to `FallbackEncoder` (default JPEG) (#47840, #46665).
- **Fit-auto returns `double.PositiveInfinity`** from `ConvertToPixels` when width/height is 0/NaN in
  Fit mode — downstream math must handle it; Fill/Stretch return the original dimension instead.
- **Settings are captured once per batch** — a mid-batch settings change won't take effect until the
  next run (by design, `ResizeBatch.ProcessAsync`).
- **The C# models are forked from Brice Lambson's ImageResizer (MIT)** — preserve the license header
  and be wary that upstream behavior/tests informed the current design.

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim this file's playbooks and then hunt the diff for those
themes — that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

Note: several Copilot-authored review comments on PR #47134 (claiming `forceFresh` drops metadata /
codec options) were marked **"incorrect"** by maintainer `moooyo` — the fresh-encode path is
deliberate and does carry known metadata. When localizing a bug, if the symptom doesn't map cleanly
to a row above, reason from the symptom and verify in source; a thin/absent map entry can anchor you
onto a confident, wrong file.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to an ImageResizer PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/imageresizer/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/imageresizer)
- [WIC codecs](https://learn.microsoft.com/windows/win32/wic/native-wic-codecs) · [WIC metadata](https://learn.microsoft.com/windows/win32/wic/-wic-about-metadata) · [naming a file](https://learn.microsoft.com/windows/win32/fileio/naming-a-file) · [avoid async void](https://learn.microsoft.com/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming#avoid-async-void)
