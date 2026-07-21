# Environment Variables Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each claim
in source before acting. Symptoms map to `src/modules/EnvironmentVariables/`.

## Key Decisions (context for the playbooks)

- **Registry-direct read/write instead of the Environment API.** `GetVariables` reads from
  `HKCU\Environment` / `HKLM\...\Session Manager\Environment` with `DoNotExpandEnvironmentNames`
  (Environment API auto-expands). Writes use `SetEnvironmentVariableFromRegistryWithoutNotify`, then a
  single `NotifyEnvironmentChange`, because `Environment.SetEnvironmentVariable` carries a ~1s
  `SendNotifyMessage(WM_SETTINGCHANGE)` timeout per write — applying a profile would take
  `num_vars × 1s` (`EnvironmentVariablesHelper.cs`).
- **`%`-containing values stored as `REG_EXPAND_SZ`.** Mirrors the Windows built-in editor so
  `%PATH%`-style references keep expanding (`SetEnvironmentVariableFromRegistryWithoutNotify`).
- **Profiles override User variables only, always user-scope.** Profile apply/unapply route through
  `SetProfileVariableWithoutNotify`/`UnsetProfileVariableWithoutNotify` (always `fromMachine:false`),
  regardless of `ParentType` ([PR #48740](https://github.com/microsoft/PowerToys/pull/48740)).
- **Automatic backup/restore.** When a profile variable shadows an existing User variable, the
  original is renamed to `NAME_PowerToys_PROFILE` (`GetBackupVariableName`) and restored on unapply.
  Backup is created **only if one doesn't already exist**, to avoid overwriting the true original
  (`Variable.cs::Update`, `ProfileVariablesSet.cs::Apply/UnapplyVariable`).
- **One active profile; validated + drift-checked.** `IsApplicable` validates before apply;
  `IsCorrectlyApplied` runs at startup — on drift the profile is disabled and
  `EnvironmentState.ChangedOnStartup` is set (`MainViewModel.cs::LoadProfiles`).
- **Self-ignoring change broadcast.** `NotifyEnvironmentChange` broadcasts `WM_SETTINGCHANGE` with
  sentinel `wParam == 0x12345`; `MainWindow.WndProc` ignores that sentinel so the module doesn't flag
  its own writes as external.
- **Elevation-aware editing + launch.** System edits require `ElevationHelper.IsElevated`
  (`Variable.IsEditable`). The module launches a separate elevated process via `ShellExecuteExW` with
  the `runas` verb and a distinct show-admin event (`dllmain.cpp::launch_process`).
- **Disabled by default, GPO-gated.** `is_enabled_by_default() == false`;
  `gpo_policy_enabled_configuration` gates activation (`dllmain.cpp`; #47144).
- **PATH is merged for display.** `PopulateAppliedVariables` shows System PATH with User PATH appended
  (`;`) as a synthetic `VariablesSetType.Path` row, and flags non-PATH duplicates as `Duplicate`.

## Regression Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| Elevated crash | App terminates on startup in admin mode | `MainWindow.xaml.cs` ctor | Empty `AppWindow.Title` (resource map failed) faults WinUI TitleBar | Non-empty title fallback before `SetTitleBar` | [PR #49069](https://github.com/microsoft/PowerToys/pull/49069), [#48971](https://github.com/microsoft/PowerToys/issues/48971), [#48547](https://github.com/microsoft/PowerToys/issues/48547), [#48519](https://github.com/microsoft/PowerToys/issues/48519), [#41642](https://github.com/microsoft/PowerToys/issues/41642) |
| Elevation gate | System vars read-only / "can't edit" | `Variable.cs::IsEditable`; `ElevationHelper.cs` | `HKLM` needs elevation; profile-applied vars locked | Keep both edit conditions | [#45197](https://github.com/microsoft/PowerToys/issues/45197) |
| Drag-as-admin | Rows not draggable (esp. admin) | `EnvironmentVariablesMainPage.xaml.cs` | WinUI 3 blocks drag when elevated | Reverted feature; require admin fallback before re-adding | [PR #40105](https://github.com/microsoft/PowerToys/pull/40105), [PR #44705](https://github.com/microsoft/PowerToys/pull/44705), [#44631](https://github.com/microsoft/PowerToys/issues/44631) |
| Profile nuke | PATH/User var lost after edit/disable of enabled profile | `Variable.cs::Update`; `ProfileVariablesSet.cs::Apply/UnapplyVariable` | Backup overwritten when it already exists | Backup only if `GetExisting(backupName)==null`; restore-then-delete | `Variable.cs::Update` comment; devdoc backup contract |
| Broadcast | Changes not seen in open cmd/PowerShell | `EnvironmentVariablesHelper.cs::NotifyEnvironmentChange` | `WM_SETTINGCHANGE` only refreshes listeners | Expected; document new-shell requirement | [#47998](https://github.com/microsoft/PowerToys/issues/47998) |
| Validation | Invalid Name/Variable pair in registry | `Variable.cs::Validate`; write guard | Validation gap before write | Validate at model + write boundary; no partial write | [#46763](https://github.com/microsoft/PowerToys/issues/46763) |
| Notify timeout | Profile apply very slow | apply path | Using Environment API (per-write 1s notify) | Registry write + single `NotifyEnvironmentChange` | `EnvironmentVariablesHelper.cs` comments |
| Value kind | `%VAR%` stops expanding | `SetEnvironmentVariableFromRegistryWithoutNotify` | Stored as `REG_SZ` not `REG_EXPAND_SZ` | Write `%`-values as `ExpandString` | source-verified |
| Name length | Long user var silently dropped | `Validate`; `SetEnvironmentVariableFromRegistryWithoutNotify` | 255-char user-name registry limit | Validate `< 255` in UI + guard write | source-verified |
| Build/deps | Build/toolchain breakage | `.vcxproj`/`.csproj`; MSBuild PS invoke | Relative paths; unquoted PS args; suppressed warnings | `$(RepoRoot)`; quote args; disable module auto-load not blanket `SilentlyContinue` | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639), [PR #46729](https://github.com/microsoft/PowerToys/pull/46729) |
| SDK bump | Editor/titlebar issues after WASDK bump | project refs; `MainWindow.xaml.cs` | WASDK 1.8.5 / CppWinRT upgrades, TitleBar workaround removal | Smoke-test editor after bump | [PR #45532](https://github.com/microsoft/PowerToys/pull/45532), [PR #45420](https://github.com/microsoft/PowerToys/pull/45420) |

## Common Practices (enforced in review)

- **Registry is the source of truth**, read unexpanded and written directly; expansion happens only
  for the read-only "applied variables" view (`PopulateAppliedVariables`).
- **Profile lifecycle is transactional-ish:** unset current profile before applying another; back up
  before override; restore before delete. Keep these orderings intact.
- **Elevation is a first-class state:** the app runs both elevated and not; every feature (edit, drag,
  launch) must behave in both. WinUI3 admin limitations (drag) are platform constraints.
- **Persistence:** `profiles.json` lives at `%LOCALAPPDATA%\Microsoft\PowerToys\EnvironmentVariables\`;
  reads tolerate missing/corrupt files (return empty, log, no crash) (`EnvironmentVariablesService.cs`).
- **Default-off + GPO:** don't assume the module is enabled in integration paths (#47144).

## Excluded as noise (not distilled)

- Pure Copilot-bot nitpicks on PR #40105 (extract `;` constant, `.ToArray()` consistency, add
  `AutomationProperties.Name`) — style/maintainability, not durable regressions.
- Cross-module issues surfaced only via the `Product-Environment Variables` area label but unrelated
  to this module (e.g. ZoomIt Ctrl+C #49204, Peek #48665, CmdPal #48369/#39885, LightSwitch #46060,
  Awake #45820, install/Scoop #43371/#39432). Triage to the owning module.
- Localization/translation requests and generic "something went wrong" reports without a repro
  (#48426, #44336, #43301, #43283, #44403 animation polish).

---
*Corpus: 12 merged PRs, 121 review comments, 30 area-labeled issues + source verification against
`src/modules/EnvironmentVariables` and `doc/devdocs/modules/environmentvariables.md`.*
