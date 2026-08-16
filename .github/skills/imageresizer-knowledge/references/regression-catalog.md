# ImageResizer Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

Historical evidence for PowerToys **ImageResizer**. Source anchors are under
`src/modules/imageresizer/`.

> **Role split:** `SKILL.md` owns current mechanics, guardrails, and review workflow. This ledger owns
> provenance: chronology, issue/PR evidence, exact source anchors, reviewer decisions, unresolved
> clusters, and caveats. Confirm current behavior in source before applying historical conclusions.

## Chronology and evidence ledger

| Sequence | Evidence | Decision or observed regression | Exact source anchors |
|---|---|---|---|
| 1 | [PR #37651](https://github.com/microsoft/PowerToys/pull/37651) (merged Feb 14, 2026) | Migrated Image Resizer tests toward Microsoft Testing Platform; this is landed test-tooling chronology, not abandoned intent. | ImageResizer test project configuration |
| 2 | [PR #45266](https://github.com/microsoft/PowerToys/pull/45266) | Added debounced live settings reload, dispatcher-marshalled property updates, and legacy settings-directory migration. | `ui/Properties/Settings.cs::InitializeWatcher`, `Reload`, `ReloadCore` |
| 3 | [PR #45288](https://github.com/microsoft/PowerToys/pull/45288) | Migrated the editor from WPF to WinUI 3. Review standardized on WinUIEx, asynchronous picker/view contracts, ThemeResource colors, scalable controls, and accessible names. The migration is the common predecessor of later encoder/settings regressions. | `ui/ImageResizerXAML/*`; `ui/ViewModels/*`; windowing and picker paths |
| 4 | [PR #46695](https://github.com/microsoft/PowerToys/pull/46695), reports [#45484](https://github.com/microsoft/PowerToys/issues/45484) | Restored PNG/encoder-option plumbing missed during migration. | `ui/Models/ResizeOperation.cs::GetEncoderPropertySet`; PNG/TIFF settings path |
| 5 | [PR #46872](https://github.com/microsoft/PowerToys/pull/46872) | Added CLI telemetry. Review required the command to reflect parsed operation and telemetry to run before process return/termination. | `ImageResizerCLI/Program.cs`; `ui/Cli/*` |
| 6 | [PR #47056](https://github.com/microsoft/PowerToys/pull/47056), issue [#47055](https://github.com/microsoft/PowerToys/issues/47055) | Fixed settings fields reverting/reading as zero because source-generated observable properties lacked forwarded JSON names. Round-trip coverage expanded beyond `Name`/`Count`, including numeric/enum fields and `AiSize.Scale`. | `ui/Properties/Settings.cs`; `ui/Models/ResizeSize.cs`; `ui/Models/AiSize.cs`; `[property: JsonPropertyName]`; `SettingsTests` |
| 7 | [PR #47134](https://github.com/microsoft/PowerToys/pull/47134), issues [#47135](https://github.com/microsoft/PowerToys/issues/47135) and [#45484](https://github.com/microsoft/PowerToys/issues/45484) | Fixed JPEG quality being ignored by forcing transformed JPEGs onto fresh encode, where codec options apply. This deliberately trades full transcode metadata preservation for the explicit known-property copy. | `ui/Models/ResizeOperation.cs::EncodeToStreamAsync`, `GetEncoderPropertySet`, `CreateFreshEncoderAsync`; `forceFresh` |
| 8 | [PR #47752](https://github.com/microsoft/PowerToys/pull/47752) | Continued WinUI accessibility cleanup, including names for icon-only controls. | `ui/ImageResizerXAML/*`; `AutomationProperties.Name` |
| 9 | Issues [#47693](https://github.com/microsoft/PowerToys/issues/47693), [#46317](https://github.com/microsoft/PowerToys/issues/46317) | Metadata evidence split into two concerns: unusual/large EXIF could disappear even on transcode, while remove-metadata must strip GPS yet preserve rendering-critical orientation/colorspace. The accepted implementation re-sets known properties best-effort and limits retained properties when stripping. | `ResizeOperation.cs::TranscodeAsync`, `CopyKnownMetadataAsync`, `FreshEncodeAsync`, `ReadMetadataAsync`, `WriteMetadataAsync`; `KnownMetadataProperties`; `RenderingMetadataProperties` |

## Issue evidence by area

| Area | Reports | What the evidence establishes | Exact source anchors |
|---|---|---|---|
| HEIC/WebP | [#47840](https://github.com/microsoft/PowerToys/issues/47840), [#45474](https://github.com/microsoft/PowerToys/issues/45474), [#46665](https://github.com/microsoft/PowerToys/issues/46665) | Repeated failures/fallbacks occur where WIC has a decoder but no matching built-in encoder without Store codec extensions. No accepted evidence here adds HEIC/WebP to the fixed encoder map. | `ui/Utilities/CodecHelper.cs::GetEncoderIdForDecoder`, `CanEncode`, `GetEncoderIdFromLegacyGuid`; `FallbackEncoder` |
| Context-menu registration | [#45521](https://github.com/microsoft/PowerToys/issues/45521), [#43782](https://github.com/microsoft/PowerToys/issues/43782), [#45458](https://github.com/microsoft/PowerToys/issues/45458) | Missing entries after update/under Windows 11 point to sparse-MSIX version and registration lifecycle; classic COM remains separately gated. | `dll/dllmain.cpp::enable`, `disable`, `UpdateRegistration`; `dll/RuntimeRegistration.h`; `dll/ContextMenuHandler.cpp`; `ImageResizerContextMenu/dllmain.cpp` |
| Orientation | Source-verified; no unique PR/issue retained in the corpus | Correct sizing depends on a conditional target-dimension swap and avoiding double EXIF application. | `ResizeOperation.cs::CalculateDimensions`; `EncodeFramesAsync`; `ExifOrientationMode.IgnoreExifOrientation`; `Settings.IgnoreOrientation` |
| Destination names | Source-verified; [Win32 naming rules](https://learn.microsoft.com/windows/win32/fileio/naming-a-file) | User-controlled `%1..%6` tokens can resolve to illegal/reserved or duplicate names. | `ResizeOperation.cs::GetDestinationPath`; `_avoidFilenames`; `Settings.cs::FileNameFormat` |

## Source-backed design decisions

These facts disambiguate the evidence; operating guidance remains in `SKILL.md`.

- **Encode-path split:** `EncodeToStreamAsync` transcodes only when output codec equals input codec,
  `RemoveMetadata` is false, and `forceFresh` is false. Otherwise, it fresh-encodes and copies only
  `KnownMetadataProperties`. JPEG transform sets `forceFresh` so `ImageQuality` applies.
- **Metadata scope:** known preservation covers DateTaken, camera model/manufacturer, orientation,
  color space, and comment. Unsupported `BitmapProperties` operations, including format-specific BMP
  behavior, are intentionally best-effort.
- **Fixed codec map:** JPEG, PNG, BMP, TIFF, GIF, and JXR are mapped. An unmatched decoder uses
  `FallbackEncoder`, whose default is JPEG container GUID `19e4a5aa-...`.
- **Batch consistency:** `ResizeBatch.ProcessAsync` captures `Settings.Default` once before
  `Parallel.ForEachAsync`; mid-batch settings edits apply to the next run.
- **Filename contract:** `%1` original name, `%2` size name, `%3/%4` selected width/height, and
  `%5/%6` output width/height are converted to composite formatting before sanitization and
  de-duplication.

## Reviewer decision ledger

- **Anti-anchoring correction:** on [PR #47134](https://github.com/microsoft/PowerToys/pull/47134),
  Copilot comments claimed `forceFresh` dropped metadata and codec options no longer applied on
  transcode. Maintainer `moooyo` marked both **“incorrect.”** The source shows fresh encode copies
  known metadata, while codec options intentionally apply only to fresh encoders.
- **WinUI review:** [#45288](https://github.com/microsoft/PowerToys/pull/45288) standardized on
  WinUIEx, rejected blocking picker calls/`async void` command paths, required ThemeResource colors
  and scalable sizing, and required accessible names for icon-only buttons.
- **Settings tests:** [#47056](https://github.com/microsoft/PowerToys/pull/47056) established that
  serialization tests must exercise generated numeric/enum properties, not only collection names and
  counts.
- **Build evidence:** CppWinRT/.NET changes were expected to carry clean x64 and ARM64,
  Debug and Release evidence; PowerShell path arguments must be quoted and warnings must not be
  blanket-suppressed ([PR #45420](https://github.com/microsoft/PowerToys/pull/45420),
  [PR #41280](https://github.com/microsoft/PowerToys/pull/41280),
  [PR #46729](https://github.com/microsoft/PowerToys/pull/46729)).
- **Test location:** regression coverage belongs in `src/modules/imageresizer/tests`, including
  `ResizeOperationTests`, `ResizeSizeTests`, `ResizeBatchTests`, `SettingsTests`, and `tests/Cli/*`.

## Open clusters and caveats

- HEIC/WebP reports establish a compatibility cluster, not an accepted new encoder implementation.
  Availability still depends on the installed WIC codec set.
- Context-menu reports establish registration-lifecycle risk, but the corpus contains no single
  confirmed root cause shared by all three issues.
- Orientation and reserved-name entries are source-verified decisions without unique regression PRs
  in the retained corpus; do not manufacture historical attribution for them.
- The WPF-to-WinUI migration was substantially AI-generated and then hand-reviewed. Treat migration
  parity claims as requiring source and test verification.

---

*Corpus: 12 merged/closed PRs, 67 review comments, 76 conversation comments, 30 bug issues, plus
source verification against `src/modules/imageresizer`.*
