---
name: powertoys-environmentvariables-knowledge
description: 'PowerToys Environment Variables module knowledge: feature->file/function map, recurring regression playbooks (elevation/admin crashes & drag-as-admin, profile apply/unapply + backup restore, registry-direct writes vs Environment API, WM_SETTINGCHANGE broadcast/self-ignore, REG_EXPAND_SZ vs REG_SZ, empty-titlebar startup fault), maintainer review rules, and Pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/EnvironmentVariables — user/system/profile variable handling, elevation, applying/broadcasting env changes, backup/restore, profiles.json, PATH merge, WinUI editor. Keywords: Environment Variables, env var, PATH, profile, elevation, admin, runas, registry, WM_SETTINGCHANGE, REG_EXPAND_SZ, backup, WinUI3, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Environment Variables Knowledge

Grounded engineering knowledge for the PowerToys **Environment Variables** module — a WinUI 3
desktop app that views and edits Windows **User** and **System** environment variables and manages
**profiles** (named sets of variables that override User variables, with automatic backup/restore).
Use it to localize code fast, avoid known regression traps (elevation, profile backup/restore,
registry write semantics, change broadcasting), and enforce conventions maintainers established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/EnvironmentVariables/` and needing prior art.
- Fixing/triaging an EnvironmentVariables bug: crash when run as admin, System variables not
  editable, profile apply/unapply corrupting or "nuking" PATH, changes not propagating to open
  shells, invalid name/value written to registry, drag-and-drop not working.
- Reviewing an EnvironmentVariables PR against maintainer conventions and regression traps.
- Touching variable read/write, the profile apply/backup engine, elevation gating, or the
  `WM_SETTINGCHANGE` broadcast/receive path.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).
Root: `src/modules/EnvironmentVariables/`.

| Sub-feature | Implementation (file · function) |
|---|---|
| Read variables from registry (no expansion) | `EnvironmentVariablesUILib/Helpers/EnvironmentVariablesHelper.cs::GetVariables` (reads with `RegistryValueOptions.DoNotExpandEnvironmentNames`) |
| Registry key resolution (User vs Machine) | `EnvironmentVariablesHelper.cs::OpenEnvironmentKeyIfExists` (`HKCU\Environment` vs `HKLM\...\Session Manager\Environment`) |
| Write variable to registry (no notify) | `EnvironmentVariablesHelper.cs::SetEnvironmentVariableFromRegistryWithoutNotify` (REG_SZ vs REG_EXPAND_SZ; 255-char user-name guard) |
| Broadcast env change to system | `EnvironmentVariablesHelper.cs::NotifyEnvironmentChange` (`SendNotifyMessage(HWND_BROADCAST, WM_SETTINGCHANGE, 0x12345, "Environment")`) |
| Set/Unset a single User/System var | `EnvironmentVariablesHelper.cs::SetVariable` / `UnsetVariable` (dispatch by `ParentType`; then notify) |
| Set/Unset a **profile** var (always user-scope) | `EnvironmentVariablesHelper.cs::SetProfileVariableWithoutNotify` / `UnsetProfileVariableWithoutNotify` (always `fromMachine:false`) |
| Backup-variable naming | `EnvironmentVariablesHelper.cs::GetBackupVariableName` → `NAME_PowerToys_PROFILE` |
| Look up existing var (User then System) | `EnvironmentVariablesHelper.cs::GetExisting` |
| Elevation detection | `EnvironmentVariablesUILib/Helpers/ElevationHelper.cs::IsElevated` (`WindowsPrincipal.IsInRole(Administrator)`) |
| Profile apply / unapply / restore | `EnvironmentVariablesUILib/Models/ProfileVariablesSet.cs::Apply` / `UnApply` / `UnapplyVariable` |
| Profile applicability + drift checks | `ProfileVariablesSet.cs::IsApplicable` / `IsCorrectlyApplied` |
| Orchestration (load, edit, add/remove, enable) | `EnvironmentVariablesUILib/ViewModels/MainViewModel.cs` |
| Applied-variables view + PATH merge + duplicate detection | `MainViewModel.cs::PopulateAppliedVariables` |
| Startup profile drift → state | `MainViewModel.cs::LoadProfiles` (sets `EnvironmentState.ChangedOnStartup`) |
| Variable model, validation, edit propagation, backup-on-rename | `EnvironmentVariablesUILib/Models/Variable.cs::Validate` / `Update`; `IsEditable` (elevation gate) |
| Precedence types | `EnvironmentVariablesUILib/Models/VariablesSetType.cs` (`Path, Duplicate, Profile, User, System`) |
| Env-changed banner states | `EnvironmentVariablesUILib/Models/EnvironmentState.cs` |
| profiles.json persistence | `EnvironmentVariablesUILib/Helpers/EnvironmentVariablesService.cs` (`%LOCALAPPDATA%\Microsoft\PowerToys\EnvironmentVariables\profiles.json`) |
| Editor window, titlebar, external-change receive | `EnvironmentVariables/EnvironmentVariablesXAML/MainWindow.xaml.cs` (`WndProc` WM_SETTINGSCHANGED; empty-title fallback) |
| Main page / list UI / drag handlers | `EnvironmentVariablesUILib/EnvironmentVariablesMainPage.xaml(.cs)` |
| Process launch (normal + elevated) | `EnvironmentVariablesModuleInterface/dllmain.cpp::launch_process` (ShellExecuteExW, `runas` verb; separate show/show-admin events) |
| GPO gate, enable/disable, default-off | `dllmain.cpp` `gpo_policy_enabled_configuration`, `enable/disable`, `is_enabled_by_default()==false` |

**Precedence (documented):** active **Profile** var > **User** var > **System** var. Profiles
override **User** variables only; every profile write targets the current-user registry regardless
of a variable's `ParentType`. See `doc/devdocs/modules/environmentvariables.md`.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Crash immediately when launched as Administrator
- **Symptom:** the app terminates on startup in elevated mode (admin), while non-elevated works.
- **Where:** `MainWindow.xaml.cs` constructor — admin path uses the `WindowAdminTitle` resource, and
  the WinUI TitleBar reads `AppWindow.Title` during a deferred layout pass.
- **Root cause:** `ResourceLoader.GetString` can return an **empty string** when the resource map
  fails to resolve at runtime; an empty native window title faults the windowing layer and kills the
  process. Elevated launches are more exposed to resource-resolution differences.
- **Guardrail:** never leave the native window `Title` empty — fall back to a non-empty product name
  before `SetTitleBar`. Evidence: [PR #49069](https://github.com/microsoft/PowerToys/pull/49069);
  reports [#48971](https://github.com/microsoft/PowerToys/issues/48971),
  [#48547](https://github.com/microsoft/PowerToys/issues/48547),
  [#48519](https://github.com/microsoft/PowerToys/issues/48519),
  [#41642](https://github.com/microsoft/PowerToys/issues/41642).

### System variables not editable / User vars can't be edited
- **Symptom:** System variables are read-only; user reports being unable to edit.
- **Where:** `Variable.cs::IsEditable` → `(ParentType != System || IsElevated) && !IsAppliedFromProfile`.
- **Root cause:** editing `HKLM` requires elevation; a variable shown as "applied from profile" is
  intentionally locked. Both correctly disable editing but surface as "can't edit".
- **Guardrail:** when changing edit-gating, preserve both conditions — elevation for System, and the
  `IsAppliedFromProfile` lock. Evidence: [#45197](https://github.com/microsoft/PowerToys/issues/45197).

### Drag-and-drop reordering breaks under elevation
- **Symptom:** variable/value rows can't be dragged, especially when running as admin.
- **Where:** `EnvironmentVariablesMainPage.xaml(.cs)` drag handlers.
- **Root cause:** **WinUI 3 does not support drag operations when running as administrator** — a
  platform limitation, not an app bug. The drag-and-drop feature was reverted for this reason.
- **Guardrail:** do not re-add drag-and-drop without an admin-mode fallback; the app routinely runs
  elevated. Evidence: [PR #40105](https://github.com/microsoft/PowerToys/pull/40105) reverted by
  [PR #44705](https://github.com/microsoft/PowerToys/pull/44705); report
  [#44631](https://github.com/microsoft/PowerToys/issues/44631).

### Profile apply/unapply corrupts or "nukes" a User variable (e.g. PATH)
- **Symptom:** enabling a profile that shadows an existing User var, then editing/disabling it, loses
  the original value (PATH wiped).
- **Where:** `ProfileVariablesSet.cs::Apply`/`UnApply`/`UnapplyVariable`; `Variable.cs::Update`
  (backup-on-rename block).
- **Root cause:** the backup is keyed by `GetBackupVariableName` (`NAME_PowerToys_PROFILE`); creating
  a backup when one already exists overwrites the true original with the profile value.
- **Guardrail:** only create a backup when `GetExisting(backupName) == null`; on unapply, restore from
  the backup then delete it. Keep backup naming stable — renaming the scheme orphans existing backups.
  Evidence: `Variable.cs::Update` comment ("solves Path nuking errors … after editing path on an
  enabled profile"); precedence/backup contract in `doc/devdocs/modules/environmentvariables.md`.

### Changes don't take effect in already-open shells (cmd/PowerShell)
- **Symptom:** after applying a profile/var, an already-running `cmd`/console doesn't see the new value.
- **Where:** `EnvironmentVariablesHelper.cs::NotifyEnvironmentChange` (`WM_SETTINGCHANGE` broadcast).
- **Root cause:** `WM_SETTINGCHANGE` only refreshes processes that **listen** for it (Explorer, new
  shells). Console hosts and already-launched processes do not re-read the environment; this is
  expected Windows behavior, not a module bug.
- **Guardrail:** don't "fix" this by force-injecting into running processes; document that a new shell
  is required. Preserve the broadcast + the self-ignore sentinel. Evidence:
  [#47998](https://github.com/microsoft/PowerToys/issues/47998).

### Invalid name/value written to registry
- **Symptom:** an invalid Name/Variable pair ends up in the registry.
- **Where:** `Variable.cs::Validate` (empty name; user name length `< 255`) and
  `SetEnvironmentVariableFromRegistryWithoutNotify` (silently returns on over-length user names).
- **Root cause:** validation gaps let malformed pairs through before a registry write.
- **Guardrail:** validate at the model boundary (`Validate`) **and** guard at the write boundary; a
  failing write must not partially apply. Evidence: [#46763](https://github.com/microsoft/PowerToys/issues/46763).

### Self-triggered "environment changed" banner loop
- **Symptom:** the module shows its own writes as an external change.
- **Where:** `MainWindow.xaml.cs::WndProc` (WM_SETTINGSCHANGED) vs `NotifyEnvironmentChange`.
- **Root cause:** the module both broadcasts and receives `WM_SETTINGCHANGE`.
- **Guardrail:** keep the `wParam == 0x12345` sentinel in sync between broadcaster and receiver so the
  module ignores its own broadcasts; only foreign changes set `EnvironmentState.EnvironmentMessageReceived`.

## Review Rules

Enforce these when reviewing or authoring Environment Variables changes:

- **Write to the registry directly, not via `Environment.SetEnvironmentVariable`.** The Environment
  API expands values on read and imposes a ~1s `SendNotifyMessage` timeout per write (num_vars × 1s
  when applying a profile). Reads must use `DoNotExpandEnvironmentNames`; writes use the registry +
  one explicit `NotifyEnvironmentChange`. Don't reintroduce the Environment API on these paths
  (`EnvironmentVariablesHelper.cs`).
- **Preserve `%VAR%` values as `REG_EXPAND_SZ`.** A value containing `%` must be written with
  `RegistryValueKind.ExpandString`; otherwise `%PATH%`-style references are stored literally and stop
  expanding — matches the Windows editor. Keep the `%` branch in
  `SetEnvironmentVariableFromRegistryWithoutNotify`.
- **Profile writes are always user-scope.** Route profile apply/unapply through
  `SetProfileVariableWithoutNotify` / `UnsetProfileVariableWithoutNotify` (both `fromMachine:false`),
  not the generic `SetVariable`, regardless of `ParentType`
  ([PR #48740](https://github.com/microsoft/PowerToys/pull/48740)).
- **Gate System edits on elevation.** Any new edit/delete UI path must respect `Variable.IsEditable`
  (System requires `ElevationHelper.IsElevated`); never write `HKLM` without elevation (#45197).
- **Never overwrite an existing backup.** Backup only when `GetExisting(backupName) == null`; restore
  then delete on unapply. Keep `GetBackupVariableName` format stable (renaming orphans backups).
- **Keep the `WM_SETTINGCHANGE` self-ignore sentinel (`0x12345`) consistent** across
  `NotifyEnvironmentChange` and `MainWindow.WndProc`.
- **Never leave the native window title empty.** WinUI TitleBar faults on an empty `AppWindow.Title`;
  keep the non-empty fallback ([PR #49069](https://github.com/microsoft/PowerToys/pull/49069)).
- **Only one profile active at a time**, validated before apply. Preserve `IsApplicable` (pre-apply
  validation incl. backup-name length) and `IsCorrectlyApplied` (startup drift → `ChangedOnStartup`).
- **No bare relative paths in project files.** Use `$(RepoRoot)`, not `..\..\..\`
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).
- **Quote path args in MSBuild PowerShell invocations** and prefer disabling module auto-loading over
  blanket `$WarningPreference='SilentlyContinue'` (which hides real resource/localization warnings)
  ([PR #46729](https://github.com/microsoft/PowerToys/pull/46729)).

## Pitfalls

- **Never** run drag-and-drop assuming it works elevated — WinUI 3 blocks drag under admin; the app
  frequently runs as admin (#40105 → reverted #44705).
- **Never** create a profile backup variable without checking one doesn't already exist — you'll
  overwrite the user's real value with the profile value and lose it on restore (PATH-nuking, `Variable.cs::Update`).
- **Never** call `Environment.SetEnvironmentVariable` on the apply path — the per-write notify timeout
  makes profile application take seconds per variable; write registry + one `NotifyEnvironmentChange`.
- **Never** read env vars with expansion when displaying source values — use
  `DoNotExpandEnvironmentNames` so `%VAR%` round-trips; the app expands only for the read-only
  "applied variables" view (`PopulateAppliedVariables`).
- **Don't** expect `WM_SETTINGCHANGE` to update already-open consoles — it only refreshes listeners;
  a new shell is required (#47998).
- **User** environment variable **names are limited to 255 chars**; over-length names are silently
  dropped at the write boundary — validate in the UI (`Variable.cs::Validate`).
- **This module is disabled by default** (`is_enabled_by_default() == false`) and GPO-gated; don't
  assume it's active in integration flows (#47144).
- **The elevated window uses a different title resource** (`WindowAdminTitle`); resource-map failures
  there are the historical root of admin-mode startup crashes (PR #49069).

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim this file's playbooks and then hunt the diff for those
themes — that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

When localizing a bug, if the symptom doesn't map cleanly to a row above, reason from the symptom and
verify in source — a thin/absent map entry can anchor you onto a confident, wrong file.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to an EnvironmentVariables PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/EnvironmentVariables/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/EnvironmentVariables)
- Dev docs: `doc/devdocs/modules/environmentvariables.md` · [Public docs](https://learn.microsoft.com/en-us/windows/powertoys/environment-variables)
- [WM_SETTINGCHANGE](https://learn.microsoft.com/en-us/windows/win32/winmsg/wm-settingchange) · [Registry env vars](https://learn.microsoft.com/en-us/windows/win32/procthread/environment-variables)
