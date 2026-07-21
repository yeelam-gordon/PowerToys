# ImageResizer PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
links to the Regression Playbook / Review Rule it enforces.

## General (any ImageResizer PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] A unit test accompanies each behavior change (`src/modules/imageresizer/tests`).
- [ ] MIT "forked from Brice Lambson" header preserved on touched `ui/Models/` files.

## Resize / encode core (`ResizeOperation.cs`)
- [ ] Correct encode path chosen: transcode only when same codec + keep-metadata + not `forceFresh`.
- [ ] Codec options (JPEG `ImageQuality`, PNG `InterlaceOption`, TIFF compression) applied on the
      **fresh-encode** path only; test asserts two distinct quality/option values differ.
- [ ] `EncodeFramesAsync` keeps `ExifOrientationMode.IgnoreExifOrientation` (no double rotation).
- [ ] `CalculateDimensions` orientation swap gated on `IgnoreOrientation && !HasAuto && Unit != Percent`.
- [ ] Fit-auto `PositiveInfinity` handled; ShrinkOnly early-returns respected.

## Metadata (`ReadMetadataAsync`/`WriteMetadataAsync`/`CopyKnownMetadataAsync`)
- [ ] Metadata read/write stays best-effort in try/catch (BMP etc. throw).
- [ ] New preservable field added to `KnownMetadataProperties` AND covered by a test.
- [ ] `RemoveMetadata` still keeps orientation + colorspace (`RenderingMetadataProperties`).
- [ ] Transcode path re-sets critical EXIF via `CopyKnownMetadataAsync`.

## Codecs / extensions (`CodecHelper.cs`)
- [ ] New container format extends `DecoderIdToEncoderId`, `EncoderExtensions`, `LegacyGuidToEncoderId` together.
- [ ] Graceful fallback to `FallbackEncoder` when decoder has no encoder (HEIC/WebP/ICO).

## Filename / output (`GetDestinationPath`)
- [ ] `%1..%6` tokens still map via `Settings.FileNameFormat` (`%`→`{}`).
- [ ] Illegal chars sanitized to `_`; reserved names (`CON`, `PRN`, …) get trailing `_`; uniquifier applied.
- [ ] `Replace` (overwrite) path keeps `.bak` backup + recycle-bin delete.

## Settings (`Settings.cs`, `SettingsWrapper.cs`, `ResizeSize.cs`, `AiSize.cs`)
- [ ] Generated observable properties use `[property: JsonPropertyName]`; round-trip test added.
- [ ] Settings snapshot captured once per batch; no per-file re-read inside `Parallel.ForEachAsync`.
- [ ] Watcher reload debounced and marshalled to UI dispatcher (`ReloadCore`).

## CLI (`ui/Cli/*`, `ImageResizerCLI/Program.cs`)
- [ ] Telemetry command name reflects real op (help/config/resize), logged before process exit/return.
- [ ] Input validation via `ResizeBatch.IsValidImagePath` (extension + existence).

## WinUI editor / a11y (`ui/ImageResizerXAML/*`, `ui/ViewModels/*`)
- [ ] No `async void` command paths; no blocking `.GetAwaiter().GetResult()` on pickers.
- [ ] Colors use `ThemeResource`; no hardcoded sizes; tested at large text size/scale.
- [ ] Icon-only buttons have `AutomationProperties.Name`.
- [ ] WinUIEx dependency retained for windowing.

## Context menu / registration (`dll/dllmain.cpp`, `RuntimeRegistration.h`, `ContextMenuHandler.cpp`)
- [ ] register/unregister idempotent and version-aware (`IsPackageRegisteredWithPowerToysVersion`).
- [ ] GPO/enabled state honored at construction; Win10 verb and Win11 sparse-MSIX both considered.

## Build / dependency bumps
- [ ] After CppWinRT/.NET/WinAppSDK bump: attach clean-build evidence (x64 + ARM64, Debug + Release).
- [ ] MSBuild PowerShell invocations quote path args; don't silently suppress script warnings.
