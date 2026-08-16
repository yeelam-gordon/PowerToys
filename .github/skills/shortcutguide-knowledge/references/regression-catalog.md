# Shortcut Guide Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This file is the historical evidence store behind `SKILL.md`.

**Role split:** `SKILL.md` owns the current engineering and manifest-authoring playbooks, review
rules, and actionable guardrails. This ledger retains provenance: source locations, issue/PR
evidence, chronology, maintainer decisions, unresolved clusters, and evidence caveats. Do not
duplicate the playbook mechanics here; confirm observations against `src/modules/ShortcutGuide/`
and the current manifest specification before acting.

## Evidence ledger

| ID | Evidence / observation | Source location | History / provenance | Caveat |
|---|---|---|---|---|
| SG-E01 | The v0.100 architecture replaced the Win-key-hold overlay with a C++ hotkey/process module plus a single-instance WinUI 3 manifest interpreter. The default hotkey is Win+Shift+/; activation toggles the UI process. | `ShortcutGuideModuleInterface/dllmain.cpp`; `ShortcutGuide.Ui/Program.cs` | Source verification of the v0.100 rewrite | Legacy hold-timing constants remain but are not the current activation path. |
| SG-E02 | Each launch copies bundled manifests to `%LocalAppData%\Microsoft\WinGet\KeyboardShortcuts`, invokes `IndexYmlGenerator.exe`, and injects enabled PowerToys hotkeys into the marked region of `Microsoft.PowerToys.en-US.yml`. | `Program.cs` · `CopyAndIndexGenerationThread`; `PowerToysShortcutsPopulator.Populate` | Source verification | Launch-path work and external index generation are relevant to latency and empty-index failures. |
| SG-E03 | Application matching compares the foreground process and eligible background processes with manifest `WindowFilter`; index entries are grouped by `(WindowFilter, BackgroundProcess)`. | `ManifestInterpreter.GetAllCurrentApplicationIds`; `ShortcutGuide.IndexYmlGenerator/IndexYmlGenerator.cs` | Source verification | Process access can fail and is intentionally caught. |
| SG-E04 | The removed dual-window architecture could enter a reentrant activation/positioning path in which `App.TaskBarWindow.AppWindow` was null and an exception closed initialization. Current source replaces both native windows with `OverlayWindow` hosting `MainPaneControl` and `TaskbarPaneControl`. | Historical `MainWindow`/`TaskbarWindow`; current `ShortcutGuideXAML/OverlayWindow.xaml.cs` and `Controls/*PaneControl.*` | Reports [#48441](https://github.com/microsoft/PowerToys/issues/48441), [#48448](https://github.com/microsoft/PowerToys/issues/48448), [#48522](https://github.com/microsoft/PowerToys/issues/48522) → fix [PR #48481](https://github.com/microsoft/PowerToys/pull/48481) | Retain as historical causality; current reviews should preserve the single-overlay replacement rather than demand null checks for removed windows. |
| SG-E05 | `ResourceLoader.GetString` can return an empty string without throwing; an empty native title on the custom-title-bar window was associated with deferred WinUI failure. | `ShortcutGuideXAML/OverlayWindow.xaml.cs` title assignment; WinUI title-bar path | Current-source verification and fix [PR #49069](https://github.com/microsoft/PowerToys/pull/49069) | The generalized WinUI class-of-bug conclusion comes from source/fix analysis; no cited issue is used as direct attribution. |
| SG-E06 | Bare numeric YAML keys are interpreted as virtual-key codes; literal digits use `<N>` and are rendered without brackets. | `ShortcutDescriptionToKeysConverter.cs`; `Controls/KeyVisual.xaml.cs`; manifest schema | Convention surfaced in #48461 → data sweep [PR #48757](https://github.com/microsoft/PowerToys/pull/48757), changing 91 keys across 14 manifests | The schema is authoritative; PowerToys rendering behavior should still be checked when token handling changes. |
| SG-E07 | Maintainer review repeatedly required spec tokens for special keys, rejected modifier-only shortcuts, required `+` on both filename and `PackageName` when no WinGet package exists, and required PR section counts to match the manifest. | Bundled manifests; `doc/specs/WinGet Manifest Keyboard Shortcuts schema.md` | Review push-back on PRs [#48652](https://github.com/microsoft/PowerToys/pull/48652), [#48821](https://github.com/microsoft/PowerToys/pull/48821), [#48959](https://github.com/microsoft/PowerToys/pull/48959), [#48960](https://github.com/microsoft/PowerToys/pull/48960) | These are review decisions, not merely parser capabilities. |
| SG-E08 | PowerToys bundles `<PackageId>.<locale>.yml`, while the specification's canonical extension is `.KBSC.yaml`; names without a WinGet package use a leading `+`, and `+WindowsNT*` is reserved for OS/shell manifests. | Bundled `Assets/ShortcutGuide/Manifests`; manifest schema | Source/spec comparison | Do not normalize one convention into the other without checking the ingestion path. |
| SG-E09 | Manifest keys support exact executable names or `*`; special section names such as `<TASKBAR1-9>` drive interpreter-specific displays; `index.yml` uses `+WindowsNT.Shell` as the default shell. | Manifest schema; `ManifestInterpreter`; `IndexYmlGenerator.cs`; taskbar window code | Source/spec verification | Interpreters that do not understand a special section may omit it. |
| SG-E10 | Manifest records include `PackageName`, `WindowFilter`, optional `BackgroundProcess`, and `Shortcuts` containing `SectionName` plus properties such as `Name`, optional descriptive fields, recommendation state, and one or more shortcut chords. `Name` and `SectionName` use sentence case; a label such as `1 - 8` is free-form text rather than a key token. | `doc/specs/WinGet Manifest Keyboard Shortcuts schema.md`; bundled manifests | Source/spec verification | Field availability and canonical spelling follow the current schema; re-check it before adding data. |
| SG-E11 | Win+Q and Win+S had duplicate “Open search” labels; Copilot+ hardware changes Win+Q semantics to Click to Do. | `+WindowsNT.Shell.en-US.yml` | Issue [#48427](https://github.com/microsoft/PowerToys/issues/48427) → fix [PR #48439](https://github.com/microsoft/PowerToys/pull/48439) | Static OS shortcut text can vary by SKU, hardware, or release. |
| SG-E12 | The resx-to-rc build step failed when PowerShell profile/module-autoload warnings reached stderr and when unquoted `$(MSBuildThisFileDirectory)` contained spaces. | `ShortcutGuideModuleInterface.vcxproj` and repo-wide conversion invocation | Issue [#46618](https://github.com/microsoft/PowerToys/issues/46618) → fix [PR #46729](https://github.com/microsoft/PowerToys/pull/46729) | This was a repo-wide build reliability change retained because it touched the module interface. |
| SG-E13 | The UI process force-exits, GPO is checked in both native and managed entry paths, index caching uses `index.yml` last-write time, and dynamic PowerToys content is bounded by populate markers. | `Program.cs`; `dllmain.cpp`; `ManifestInterpreter.GetCachedIndexYamlFile`; `PowerToysShortcutsPopulator` | Source verification | These are current architecture decisions and may change only with coordinated lifecycle/cache work. |
| SG-E14 | Key conversion has focused unit coverage for VK codes and `<N>`/special tokens. | `ShortcutGuide.UnitTests/ConvertersTests/ShortcutDescriptionToKeysConverterTests.cs` | Source verification | Coverage is focused on conversion and does not validate every manifest or overlay behavior. |
| SG-E15 | `CopyAndIndexGenerationThread` logs a bundled-manifest copy exception but continues into index generation, so mixed old/new files can feed a stale or partial index. The generator deletes the existing `index.yml` before parsing, so malformed input can leave no prior index to reuse. | `ShortcutGuide.Ui/Program.cs`; `ShortcutGuide.IndexYmlGenerator/IndexYmlGenerator.cs`; `ManifestInterpreter.GetCachedIndexYamlFile` | Known current-source violation | Require atomic replacement or preservation/restoration of the previous valid index; do not attribute unrelated navigation/positioning reports to manifest corruption. |

## Decision ledger

| ID | Decision / review outcome | Basis | Status |
|---|---|---|---|
| SG-D01 | Preserve the single `OverlayWindow` host; do not reintroduce the removed dual-native-window activation path. | Historical #48441/#48448/#48522 → PR #48481; current overlay source | Historical fix superseded by current architecture |
| SG-D02 | Native custom-title-bar windows require a non-empty fallback title. | Current source and PR #49069 | Accepted and implemented |
| SG-D03 | Literal digits use `<N>`; special keys use schema tokens; modifier-only entries are rejected. | #48461 → PR #48757; PR reviews #48652/#48821/#48959/#48960 | Maintainer authoring decision |
| SG-D04 | No-WinGet-package manifests prefix both filename and `PackageName` with `+`; `+WindowsNT` remains OS-reserved. | Manifest schema and PR reviews #48821/#48959 | Maintainer authoring decision |
| SG-D05 | Conditional or hardware-dependent shell shortcuts need disambiguating text rather than duplicate static labels. | #48427 → PR #48439 | Accepted content decision |
| SG-D06 | Resource conversion invokes PowerShell without profiles, avoids module autoload, and quotes path arguments. | #46618 → PR #46729 | Accepted build decision |
| SG-D07 | Keep toggle activation, single-instance UI, forced process exit, and the two GPO gates coherent. | Native/managed lifecycle source | Current architecture decision |
| SG-D08 | Treat the populate-marker region as generated content, not hand-authored manifest data. | `PowerToysShortcutsPopulator.Populate` | Established review decision |

## Evidence clusters (lifecycle noted)

- **Resolved overlay-rewrite cluster:** startup latency [#49200](https://github.com/microsoft/PowerToys/issues/49200),
  Windows 10 crash [#48773](https://github.com/microsoft/PowerToys/issues/48773), and moved-taskbar
  behavior [#48435](https://github.com/microsoft/PowerToys/issues/48435) were closed completed by
  [PR #48683](https://github.com/microsoft/PowerToys/pull/48683) on July 27, 2026.
- **Settings crash:** [#49173](https://github.com/microsoft/PowerToys/issues/49173) remains distinct
  from the #48481, #49069, and #48683 paths until reproduced.
- **Ambiguous empty-close report:** [#49131](https://github.com/microsoft/PowerToys/issues/49131)
  was closed as a duplicate of #48773 without diagnostic evidence tying it to either #48481 or
  #49069. Keep it unresolved rather than using it as fix attribution.
- **Home-screen labels and conflicts:** [#49311](https://github.com/microsoft/PowerToys/issues/49311),
  [#44830](https://github.com/microsoft/PowerToys/issues/44830), and
  [#44141](https://github.com/microsoft/PowerToys/issues/44141) form an unresolved ambiguity/conflict
  cluster around the “Shortcuts” widget.
- **Hard-coded Windows shortcut remaps:** [#47950](https://github.com/microsoft/PowerToys/issues/47950)
  records the inability to display some remaps such as Win+C.
- **Taskbar geometry:** [#48435](https://github.com/microsoft/PowerToys/issues/48435) records moved
  or vertical taskbar behavior and was closed completed by PR #48683.

## Evidence caveats

- [#48170](https://github.com/microsoft/PowerToys/issues/48170) concerns missing manifests, while
  [#48638](https://github.com/microsoft/PowerToys/issues/48638) followed the overlay-rewrite
  crash path resolved by PR #48683. Neither is direct evidence for PR #49069's title fallback.
- The manifest schema is authoritative for authoring semantics; bundled `.yml` naming is a
  PowerToys integration convention and intentionally differs from the canonical `.KBSC.yaml`
  extension.
- Several issue reports share “opens empty/closes/crashes” wording. Preserve chronology and avoid
  collapsing them into one cause without logs or reproduction.
- Source locations describe the inspected v0.100-era implementation and may move.
- The ledger intentionally omits repeated symptom → root cause → guardrail instructions already
  maintained in `SKILL.md`.
