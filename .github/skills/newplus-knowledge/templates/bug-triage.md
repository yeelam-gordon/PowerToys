# New+ Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.
Source root: `src/modules/NewPlus/`; unqualified files are under `NewShellExtensionContextMenu/`.

## Report
- **Symptom:**
- **Repro / inputs (template name, target folder):**
- **OS / build / Win10 vs Win11 / classic vs modern context menu:**
- **Settings: hide extension? hide digits? replace variables? hide built-in New? GPO?:**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Playbook |
|---|---|---|
| "New+" menu missing on Win11 with classic menu | `shell_context_menu_win10.cpp::QueryContextMenu` (returns E_FAIL on Win11); `register_msix_package` | Context menu |
| "New+" missing after upgrade / in file-picker dialog | `powertoys_module.cpp::enable/UpdateRegistration`; `new_utilities.h::register_msix_package` | Context menu |
| Templates missing from submenu | `template_folder.cpp::rescan_template_folder`; `exclude_item` | Context menu / exclusion |
| Hidden/system files show as templates | `helpers_variables.h::exclude_item` | Exclusion |
| Created file has wrong / empty name; digits not stripped | `template_item.cpp::remove_starting_digits_from_filename` | Digit stripping |
| Numeric-only name (`001231`) mangled | `template_item.cpp::remove_starting_digits_from_filename` (numeric-only guard) | Digit stripping |
| `$PARENT_FOLDER_NAME` / `%ENV%` / date not resolved | `helpers_variables.h::resolve_variables_in_path/_in_filename`; `Helpers.cpp::GetDatedFileName` | Variables |
| Files inside copied folder not renamed | `helpers_variables.h::resolve_variables_in_filename_and_rename_files` | Variables |
| Hide built-in New does nothing / inverts | `NewPlusViewModel.cs` HideBuiltInNew setter (checks new value?) | Hide built-in New |
| NullReferenceException hiding/restoring New | `NewPlusViewModel.cs` `OpenSubKey` null check | Hide built-in New |
| Only part of registry sentinel written | `new_utilities.h::disable_built_in_new_via_registry` (REG_SZ byte size) | Hide built-in New |
| Ctrl+Shift+N broken after hiding built-in New | `disable_built_in_new_via_registry`; built-in `New` handler | Hide built-in New |
| Rename mode not entered after create | `template_item.cpp::enter_rename_mode`; `new_utilities.h::explorer_enter_rename_mode` | Pitfalls |
| New file appears on wrong monitor (multi-mon) | `explorer_enter_rename_mode` desktop `SelectAndPositionItems` path | Pitfalls |
| Wrong translation of "New+" menu | resource `IDS_CONTEXT_MENU_ITEM_NEW`; module `.resx` | Localization |
| Wrong icon / theme icon | `new_utilities.h::get_new_icon_resource_filepath`, `get_explorer_icon_handle` | Module Map |
| Setting not persisting | `settings.cpp::Save/Load/ParseJson`; `constants.h` keys | Module Map |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Determine the menu surface (Win11 IExplorerCommand vs Win10/classic IContextMenu).
3. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
4. Reproduce with the reporter's template name + settings before fixing.
