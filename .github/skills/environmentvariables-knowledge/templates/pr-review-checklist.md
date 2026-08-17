# Environment Variables PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces.

## General (any EnvironmentVariables PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] No bare relative paths in `.vcxproj`/`.csproj`; uses `$(RepoRoot)` (#44639).
- [ ] MSBuild PowerShell `-Command` args quoted; script warnings not blanket-suppressed (#46729).

## Variable read/write (`EnvironmentVariablesHelper.cs`)
- [ ] Reads use registry + `DoNotExpandEnvironmentNames` (no Environment API expansion).
- [ ] Writes go to the registry directly, not `Environment.SetEnvironmentVariable` (avoid per-write notify timeout).
- [ ] Value containing `%` written as `REG_EXPAND_SZ`; otherwise `REG_SZ`.
- [ ] Exactly one `NotifyEnvironmentChange` per logical batch (not per variable).
- [ ] User variable name length `<= 259` enforced before write.

## Profiles (`ProfileVariablesSet.cs`, `MainViewModel.cs`)
- [ ] Profile writes use `SetProfileVariableWithoutNotify`/`UnsetProfileVariableWithoutNotify` (always user-scope) (#48740).
- [ ] Backup created **only** when `GetExisting(backupName) == null` (no overwrite / PATH-nuking).
- [ ] `GetBackupVariableName` format (`NAME_PowerToys_PROFILE`) unchanged (renaming orphans backups).
- [ ] Unapply restores from backup then deletes it.
- [ ] Only one profile enabled at a time; `IsApplicable` validated before `Apply`.
- [ ] Startup drift handled: `IsCorrectlyApplied` false → `EnvironmentState.ChangedOnStartup`, profile disabled.

## Elevation (`ElevationHelper.cs`, `Variable.cs::IsEditable`, `dllmain.cpp`)
- [ ] System edits gated on `IsElevated`; never write `HKLM` without elevation (source contract;
      #45197 concerns elevated-launch user identity).
- [ ] `IsAppliedFromProfile` items remain non-editable.
- [ ] No drag-and-drop reintroduced without an admin-mode fallback (WinUI3 blocks drag as admin) (#40105/#44705).
- [ ] Admin launch path (`runas` verb) and show/show-admin events preserved.

## Editor window / change broadcast (`MainWindow.xaml.cs`)
- [ ] Native window `Title` never empty; non-empty fallback kept (#49069).
- [ ] `WM_SETTINGCHANGE` self-ignore sentinel `0x12345` consistent between broadcast and `WndProc`.
- [ ] Only foreign changes set `EnvironmentState.EnvironmentMessageReceived`.

## Validation / persistence (`Variable.cs::Validate`, `EnvironmentVariablesService.cs`)
- [ ] Name/value validated at model boundary and write boundary (no invalid registry pairs) (#46763).
- [ ] `profiles.json` read/write resilient to missing/corrupt file (returns empty, logs, no crash).

## Build / SDK bumps
- [ ] After WASDK/CppWinRT bump: editor smoke-tested; titlebar/resource loading intact (#45532, #45420).
