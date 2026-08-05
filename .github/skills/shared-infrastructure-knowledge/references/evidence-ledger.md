# Shared Infrastructure Evidence Ledger

This file records cross-module evidence and decisions. Actionable review rules live in
[`SKILL.md`](../SKILL.md).

| Pattern | Source evidence | Historical evidence / status |
|---|---|---|
| Non-empty native WinUI title | `src/modules/EnvironmentVariables/EnvironmentVariables/EnvironmentVariablesXAML/MainWindow.xaml.cs`; `src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuideXAML/MainWindow.xaml.cs` | PR #49069 and current source; both modules now carry the same fallback pattern. Issue #49131 is ambiguous and is not used as attribution. |
| Shared color-format blast radius | `src/common/ManagedCommon/ColorFormatHelper.cs`; Color Picker UI/module services; `Settings.UI.Library/ColorPicker*`; `ColorFormatConversionTest.cs` | [PR #46679](https://github.com/microsoft/PowerToys/pull/46679) corrected Decimal test expectations and misleading axis names, proving that shared format semantics and their tests affect multiple consumers. |
| Persisted model compatibility | Settings UI `*Properties.cs`, module readers/watchers, module side files | PowerDisplay monitor-ID migration (#47977) and additive setting default (#49002); FancyZones and Awake catalogs contain additional state/round-trip cases |
| Process/policy lifetime | Native module interfaces and separate UI executables | Shortcut Guide and Text Extractor check policy at more than one entry point; GrabAndMove owns hooks/events/windows that require explicit teardown |

## Evidence quality

- The title and ColorFormatHelper rows are verified against current source.
- Persistence and lifecycle rows are cross-module review classes; confirm the exact module contract
  and current source before filing a finding.
- Module evidence catalogs remain authoritative for module-specific PR/issue chronology.
