---
name: shared-infrastructure-knowledge
description: 'PowerToys cross-module and shared-infrastructure review knowledge. Use when a change touches src/common, Settings.UI.Library, shared WinUI controls, serialization contracts, or the same failure pattern across multiple modules. Covers shared-helper blast radius, non-empty WinUI native titles, ManagedCommon ColorFormatHelper consumers/tests, persisted settings/state migrations, and process/window lifecycle. Keywords: PowerToys shared code, ManagedCommon, ColorFormatHelper, WinUI TitleBar, settings serialization, shared control, cross-module review, blast radius.'
---

# PowerToys Shared Infrastructure Knowledge

Use this skill with the relevant module skill when a change crosses module boundaries, modifies
`src/common/`, or changes a helper/contract consumed by both a module and Settings UI. Read the diff
cold first; then enumerate consumers before applying these playbooks.

## When to Use This Skill

- Editing `src/common/ManagedCommon`, shared WinUI controls, or Settings UI library models.
- Reviewing a helper change whose consumers span `src/modules/` and `src/settings-ui/`.
- Fixing a failure that already occurred in more than one module.
- Changing a shared persisted JSON contract, or a process/window-lifecycle failure pattern already
  demonstrated in more than one module.

## Shared Map

| Contract | Producers / implementations | Representative consumers / verification |
|---|---|---|
| Native WinUI window title | `EnvironmentVariablesXAML/MainWindow.xaml.cs`, `ShortcutGuideXAML/MainWindow.xaml.cs` | Demonstrated custom-title-bar implementations using localized native titles; launch under missing/broken resource maps |
| Color conversion/format strings | `src/common/ManagedCommon/ColorFormatHelper.cs` | Color Picker UI/module services, `Settings.UI.Library/ColorPicker*`, `ColorFormatConversionTest.cs` |
| Settings JSON models | `src/settings-ui/Settings.UI.Library/*Properties.cs` plus module readers | Settings UI round-trip, module hot reload, older `settings.json` without new fields |
| Persistent module state | Module-specific JSON stores such as FancyZones layouts and PowerDisplay profiles | Migration, corruption recovery, atomic write, identity stability |
| Separate UI process lifecycle | Native module interfaces plus WinUI/WPF executables | GPO gate, single instance, launch failure, parent exit, shutdown cleanup |

## Cross-Module Playbooks

### Empty native WinUI title can fault during deferred TitleBar layout
- **Scope:** this pattern is demonstrated in Environment Variables and Shortcut Guide. Apply it to
  another WinUI window only after verifying the same localized native-title/custom-title-bar path.
- **Symptom:** a WinUI process opens empty and terminates, often only under a resource-map failure.
- **Where:** native `Window.Title` / `AppWindow.Title` assignment before or around
  `ExtendsContentIntoTitleBar` and `SetTitleBar`.
- **Mechanism:** `ResourceLoader.GetString` can return `""` without throwing. The WinUI TitleBar can
  read the empty native title during a deferred layout pass and fault.
- **Guardrail:** resolve the localized title, replace null/empty with a non-empty literal, and assign
  the native title before the deferred TitleBar layout can read it. This fallback is implemented in
  both Environment Variables and Shortcut Guide; preserve it regardless of the local ordering of
  `ExtendsContentIntoTitleBar`, `SetTitleBar`, and `Title`.

### Shared helper change has a larger test surface than its declaring project
- **Symptom:** Color Picker works but Settings UI previews/defaults change, or a new format converts
  correctly in one path and serializes/displays incorrectly in another.
- **Where:** `src/common/ManagedCommon/ColorFormatHelper.cs`.
- **Consumers:** Color Picker UI and module services, `Settings.UI.Library.ColorPickerProperties`,
  `ColorPickerSettings`, `ColorFormatModel`, converters, and shared color-name helpers.
- **Guardrail:** enumerate all references before changing public helper behavior. Update
  `ColorFormatConversionTest.cs` for conversion math and representation/default-format tests for
  token/output changes. Validate both runtime Color Picker output and Settings UI preview/defaults.

### Persisted model changes must preserve old files and all readers
- **Symptom:** settings silently reset after upgrade, a module and Settings UI disagree, or a side
  file retains stale identities after migration.
- **Where:** Settings UI library property models, module deserializers/watchers, and module-specific
  state/profile files.
- **Guardrail:** treat this as a compatibility review heuristic, then verify the module's exact
  reader/writer contract. Additive fields default to current behavior when absent; renamed/identity fields
  require migration; source-generated JSON contexts include new serialized types; every reader and
  side file moves in the same change. Test an old-file fixture and a write/read round trip.

### Separate-process modules require paired policy and lifetime checks
- **Symptom:** policy-disabled UI still starts, a second instance races, or shutdown leaves hooks,
  events, windows, or child processes alive.
- **Where:** native module interface plus executable startup/shutdown.
- **Guardrail:** treat this as a lifecycle review heuristic, not a universal architecture mandate.
  Enforce GPO at every entry point required by that module's verified contract
  (some modules check only the native interface; others also check executable startup), make
  single-instance behavior explicit, surface launch failures, and pair every published
  hook/event/window/thread with reverse-order cleanup on disable, parent exit, and shutdown.

## Review Rules

- Search all consumers before modifying shared code; do not infer blast radius from the file's folder.
- Run consumer tests, not only the declaring library's tests.
- Label known live violations explicitly; never present an aspirational guardrail as current behavior.
- Keep module-specific mechanics in module skills and shared contract/blast-radius checks here.
- For persisted data, review compatibility, migration, corruption recovery, and concurrent writes as
  one contract.

## References

- [Evidence ledger](./references/evidence-ledger.md)
- [Cross-module review checklist](./templates/pr-review-checklist.md)
- [Cross-module bug triage](./templates/bug-triage.md)
- [Using the PowerToys knowledge skills](../knowledge-skill-usage.md)
