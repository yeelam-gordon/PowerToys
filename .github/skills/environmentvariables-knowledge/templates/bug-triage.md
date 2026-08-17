# Environment Variables Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / inputs:**
- **OS / PowerToys build:**
- **Elevated (admin) or not?:**
- **Profile enabled? which variable(s)?:**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Crashes immediately when run as admin | `MainWindow.xaml.cs` ctor (empty-title fallback / `WindowAdminTitle`) | Startup fault | Elevated crash |
| Crash on launch (non-admin too) | `MainWindow.xaml.cs`; resource loading | Startup fault | Elevated crash |
| System variables can't be edited | `Variable.cs::IsEditable`; `ElevationHelper.cs` | Elevation gate | System editable |
| Variable shown but locked | `Variable.cs::IsEditable` (`IsAppliedFromProfile`) | Profile lock | System editable |
| Can't drag/reorder rows | `EnvironmentVariablesMainPage.xaml.cs` drag handlers | WinUI3 admin drag | Drag-as-admin |
| PATH wiped after editing on enabled profile | `Variable.cs::Update`; `ProfileVariablesSet.cs::Apply` | Backup overwrite | Profile nuke |
| Original value lost after disabling profile | `ProfileVariablesSet.cs::UnapplyVariable` (restore) | Backup restore | Profile nuke |
| Changes not visible in open cmd/PowerShell | `EnvironmentVariablesHelper.cs::NotifyEnvironmentChange` | Broadcast semantics | cmd not refreshed |
| `%VAR%` stored literally / stops expanding | `SetEnvironmentVariableFromRegistryWithoutNotify` (REG_EXPAND_SZ branch) | Registry value kind | Review Rules |
| Invalid name/value in registry | `Variable.cs::Validate`; write guard | Validation gap | Invalid registry write |
| App shows its own change as external | `MainWindow.xaml.cs::WndProc`; `NotifyEnvironmentChange` (`0x12345`) | Self-ignore sentinel | Self-trigger loop |
| Applying a profile is very slow | apply path using Environment API instead of registry | Notify timeout | Review Rules |
| Profile won't enable | `ProfileVariablesSet.cs::IsApplicable` (validation) | Pre-apply validation | Profile playbooks |
| Profile disabled unexpectedly on startup | `MainViewModel.cs::LoadProfiles` (`IsCorrectlyApplied`) | Startup drift | Profile playbooks |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Note elevation state and whether a profile is enabled (both change behavior).
3. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
4. Reproduce with the reporter's inputs (User vs System vs Profile variable; admin vs not).
