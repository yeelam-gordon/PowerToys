# New+ PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces. Source root: `src/modules/NewPlus/`.

## General (any New+ PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] No bare relative paths in `.vcxproj`; uses `$(RepoRoot)`; `Microsoft.Cpp.Default.props` import
      order unchanged; deps in `Directory.Packages.props` (#44639).
- [ ] Unrelated build/IDE files (`.vscode/*`, `Directory.Packages.props`) not silently changed (#44639).

## Hide built-in "New" / registry (`NewPlusViewModel.cs`, `new_utilities.h`, `powertoys_module.cpp`)
- [ ] Property setter branches on the **new** `value`, not the current field (#44979).
- [ ] Every `OpenSubKey` result null-checked before `GetValue`/`SetValue`/`DeleteValue` (#44979).
- [ ] `RegSetValueExW` size = `(lstrlenW(str)+1)*sizeof(wchar_t)`; string pointer passed (not `&ptr`) (#44979).
- [ ] Boolean return semantics documented/consistent (helpers return `true` on failure — verify callers).
- [ ] Hide preference re-applied on `enable()` (disable→re-enable path), not only on `init_settings` (#44979).
- [ ] Impact on native File Explorer **Ctrl+Shift+N** considered (#48013).

## Template model / naming (`template_item.cpp`, `template_folder.cpp`, `helpers_filesystem.h`)
- [ ] `remove_starting_digits_from_filename`: numeric-only-stem guard preserved; **all** consecutive
      `.`/space after digits stripped (#45439).
- [ ] Hidden/system entries excluded via `exclude_item` in every scan/rename path (#45439).
- [ ] Copy targets + resolved variables pass through `make_valid_filename` (strip `\ / : * ? " < > |`).
- [ ] `make_unique_path_name` still produces ` (1)`, ` (2)`… on collisions.

## Filename variables (`helpers_variables.h`, `Helpers.cpp`)
- [ ] `$PARENT_FOLDER_NAME` resolved top-down (`resolve_variables_in_path`); copied folders rename leaf-first.
- [ ] Env-var (`%VAR%`) resolution stays case-insensitive; date tokens via `GetDatedFileName`.
- [ ] Note: `GetDatedFileName` is a copied subset of PowerRename — don't assume shared fixes.

## Context menu / registration (`shell_context_menu.cpp`, `*_win10.cpp`, `RuntimeRegistration.h`, `new_utilities.h`)
- [ ] Correct menu surface targeted: Win11 = IExplorerCommand; Win10/classic = IContextMenu (bails on Win11) (#47609).
- [ ] Sparse-package register/unregister idempotent + version-aware (`IsPackageRegisteredWithPowerToysVersion`).
- [ ] `EnumSubCommands`/`QueryContextMenu` rebuild template list from the scanned folder without leaks.
- [ ] Rename-mode still uses the detached-thread + sleep workaround; desktop/multi-monitor path intact (#46797).

## Localization / resources
- [ ] "New+" menu name (`IDS_CONTEXT_MENU_ITEM_NEW`) translations verified per locale (#47225, #46827).

## Build / scripts
- [ ] PowerShell build steps quote path args; warnings not suppressed (module auto-load disabled) (#46729).
- [ ] Win10 project still has the local `Generated Files` include path (#43361, #43461).
- [ ] Shutdown/teardown stays clean (no size-vs-safety regressions like Hybrid CRT, #43484).
