# Environment Variables Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

## Role split

`SKILL.md` owns current symptom, root-cause, and guardrail guidance. This catalog preserves the
historical evidence trail, source anchors, reviewer decisions, unresolved reports, and caveats used
to audit or refresh that guidance. Confirm source anchors before relying on them.

## Evidence ledger

| Sequence | Evidence | Source anchors | Recorded outcome / reviewer decision |
|---|---|---|---|
| Profile semantics | [PR #48740](https://github.com/microsoft/PowerToys/pull/48740) | `EnvironmentVariablesHelper.cs::SetProfileVariableWithoutNotify`, `UnsetProfileVariableWithoutNotify`; `ProfileVariablesSet.cs::Apply/UnapplyVariable` | Profile apply/unapply was kept user-scoped (`fromMachine:false`) regardless of `ParentType`. |
| Backup contract | Desired contract from source comments/devdocs; known current violation | `EnvironmentVariablesHelper.cs::GetBackupVariableName`; `Variable.cs::Update`; `ProfileVariablesSet.cs::Apply/UnapplyVariable`; `doc/devdocs/modules/environmentvariables.md` | Preserve `NAME_PowerToys_PROFILE`; create a backup only when none exists, then restore before deleting it. Current `Apply` can overwrite the backup and `UnapplyVariable` deletes it before restoration; the `Variable.cs::Update` comment records the intended PATH-loss-prevention order. |
| Registry I/O design | Source verification | `EnvironmentVariablesHelper.cs::GetVariables`, `SetEnvironmentVariableFromRegistryWithoutNotify`, `NotifyEnvironmentChange` | Reads use `DoNotExpandEnvironmentNames`; writes are batched directly to the registry and followed by one notification. Values containing `%` use `REG_EXPAND_SZ`; user names are guarded below 255 characters. |
| Change notification | [#47998](https://github.com/microsoft/PowerToys/issues/47998) plus source verification | `EnvironmentVariablesHelper.cs::NotifyEnvironmentChange`; `MainWindow.xaml.cs::WndProc` | The broadcast remains `WM_SETTINGCHANGE` with self-ignore sentinel `wParam == 0x12345`. The report records that already-running shells may not refresh. |
| Validation report | [#46763](https://github.com/microsoft/PowerToys/issues/46763) | `Variable.cs::Validate`; `SetEnvironmentVariableFromRegistryWithoutNotify` | Validation is retained at both the model and registry-write boundaries. |
| Edit-gating contract | Source verification | `Variable.cs::IsEditable`; `ElevationHelper.cs::IsElevated` | System variables require elevation and profile-applied variables remain locked. Issue [#45197](https://github.com/microsoft/PowerToys/issues/45197) concerns elevated-launch user identity, not this gate. |
| Drag feature chronology | [PR #40105](https://github.com/microsoft/PowerToys/pull/40105) → [#44631](https://github.com/microsoft/PowerToys/issues/44631) → revert [PR #44705](https://github.com/microsoft/PowerToys/pull/44705) | `EnvironmentVariablesMainPage.xaml.cs` | Drag-and-drop was introduced, reported unusable under elevation, and reverted. Reintroduction requires an admin-mode fallback. |
| Elevated startup chronology | Reports [#48547](https://github.com/microsoft/PowerToys/issues/48547), [#48971](https://github.com/microsoft/PowerToys/issues/48971) → [PR #49069](https://github.com/microsoft/PowerToys/pull/49069) | `MainWindow.xaml.cs` constructor | The accepted fix retained a non-empty `AppWindow.Title` fallback before deferred TitleBar layout consumes the title; it does not require assignment before `SetTitleBar`. |
| Build-path decision | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | `.vcxproj`, `.csproj` | Project references use `$(RepoRoot)` rather than bare relative paths. |
| Build-script review | [PR #46729](https://github.com/microsoft/PowerToys/pull/46729) | MSBuild PowerShell invocation | Reviewer outcome: quote path arguments and disable module auto-loading rather than hiding all warnings with `SilentlyContinue`. |
| SDK/toolchain chronology | [PR #45420](https://github.com/microsoft/PowerToys/pull/45420) → [PR #45532](https://github.com/microsoft/PowerToys/pull/45532) | Project references; `MainWindow.xaml.cs` | CppWinRT/WASDK changes touched title-bar assumptions; editor and elevated-launch smoke coverage remains relevant after future bumps. |
| Startup/profile state | Source verification | `ProfileVariablesSet.cs::IsApplicable`, `IsCorrectlyApplied`; `MainViewModel.cs::LoadProfiles`; `EnvironmentState.ChangedOnStartup` | One profile is active at a time; applicability is checked before apply and startup drift disables the profile and records changed state. |
| Applied view | Source verification | `MainViewModel.cs::PopulateAppliedVariables`; `VariablesSetType.Path`, `Duplicate` | System PATH is displayed with User PATH appended; other duplicate names are marked as duplicates. |
| Persistence and launch | Source verification | `EnvironmentVariablesService.cs`; `dllmain.cpp::launch_process`, `gpo_policy_enabled_configuration`, `is_enabled_by_default` | `profiles.json` is under `%LOCALAPPDATA%\Microsoft\PowerToys\EnvironmentVariables\`; missing/corrupt reads are tolerated. Elevated launch uses `ShellExecuteExW`/`runas`; the module is default-off and GPO-gated ([PR #47144](https://github.com/microsoft/PowerToys/pull/47144)). |

## Decision ledger

| Decision | Status | Evidence / anchor |
|---|---|---|
| Registry is the persistence source of truth; source values are read unexpanded. | Accepted | `EnvironmentVariablesHelper.cs::GetVariables` |
| Batch profile writes, then send one change notification. | Accepted | `SetEnvironmentVariableFromRegistryWithoutNotify`; `NotifyEnvironmentChange` |
| Store `%`-containing values as expandable strings. | Accepted | `SetEnvironmentVariableFromRegistryWithoutNotify` |
| Profile overrides are user-scope and use a stable backup name. | Accepted | [PR #48740](https://github.com/microsoft/PowerToys/pull/48740); `GetBackupVariableName` |
| Preserve the notification sentinel in both sender and receiver. | Accepted | `NotifyEnvironmentChange`; `MainWindow.WndProc` |
| Keep elevated and non-elevated launch/edit paths distinct. | Accepted | `Variable.IsEditable`; `dllmain.cpp::launch_process` |
| Do not restore drag ordering without an elevated-mode design. | Reverted / pending redesign | [PR #40105](https://github.com/microsoft/PowerToys/pull/40105), [PR #44705](https://github.com/microsoft/PowerToys/pull/44705) |

## Open issues and evidence caveats

- [#41642](https://github.com/microsoft/PowerToys/issues/41642) is a scrolling crash and
  [#48519](https://github.com/microsoft/PowerToys/issues/48519) was attributed to corrupt
  installation files; neither is direct evidence for PR #49069's title fallback.

- [#47998](https://github.com/microsoft/PowerToys/issues/47998) describes propagation expectations;
  it does not establish that existing processes can be safely mutated.
- [#45197](https://github.com/microsoft/PowerToys/issues/45197) concerns elevated launch switching
  execution to the administrator account; it does not establish the `IsEditable` conditions.
- Area-label searches included unrelated reports: ZoomIt [#49204](https://github.com/microsoft/PowerToys/issues/49204),
  Peek [#48665](https://github.com/microsoft/PowerToys/issues/48665), CmdPal
  [#48369](https://github.com/microsoft/PowerToys/issues/48369)/[#39885](https://github.com/microsoft/PowerToys/issues/39885),
  LightSwitch [#46060](https://github.com/microsoft/PowerToys/issues/46060), Awake
  [#45820](https://github.com/microsoft/PowerToys/issues/45820), and install/Scoop
  [#43371](https://github.com/microsoft/PowerToys/issues/43371)/[#39432](https://github.com/microsoft/PowerToys/issues/39432).
- Localization, animation, or generic reports without a module repro were not distilled:
  [#48426](https://github.com/microsoft/PowerToys/issues/48426),
  [#44336](https://github.com/microsoft/PowerToys/issues/44336),
  [#43301](https://github.com/microsoft/PowerToys/issues/43301),
  [#43283](https://github.com/microsoft/PowerToys/issues/43283), and
  [#44403](https://github.com/microsoft/PowerToys/issues/44403).
- Pure automated style comments on PR #40105 were excluded because they did not establish durable
  behavior.

---
*Corpus: 12 merged PRs, 121 review comments, 30 area-labeled issues, plus source verification against
`src/modules/EnvironmentVariables` and `doc/devdocs/modules/environmentvariables.md`.*
