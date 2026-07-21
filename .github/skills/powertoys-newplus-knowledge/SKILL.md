---
name: powertoys-newplus-knowledge
description: 'PowerToys New+ (NewPlus) module knowledge: feature->file/function map, regression playbooks (template-folder scan/exclude, leading-digit stripping, filename-variable resolution, hide-built-in-New registry edits, Win10 IContextMenu vs Win11 IExplorerCommand + sparse-MSIX registration, localization of the "New+" menu name), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/NewPlus — context-menu registration, template creation/copy, filename variables ($PARENT_FOLDER_NAME/env/date), hide built-in New, GPO, settings, localization. Keywords: New+, NewPlus, context menu, template folder, shell extension, IExplorerCommand, IContextMenu, sparse MSIX, Ctrl+Shift+N, GPO, localization, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys New+ (NewPlus) Knowledge

Grounded engineering knowledge for the PowerToys **New+** module — a Windows Explorer shell
extension that adds a "New+" context-menu entry letting users create files/folders from a
personal **template folder** (with optional filename-variable expansion, digit stripping, and
hiding of the built-in Windows "New" menu). Use it to localize code fast, avoid known regression
traps, and enforce the conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/NewPlus/` and needing prior art.
- Fixing/triaging a New+ bug: menu missing/duplicated, templates not listed, files created with
  wrong name, leading digits not stripped, variables not resolved, built-in New not hidden/restored,
  Ctrl+Shift+N broken, rename-mode not entered.
- Reviewing a New+ PR against maintainer conventions and regression traps.
- Touching context-menu registration (Win10 classic verb vs Win11 sparse-MSIX / IExplorerCommand),
  template-folder scanning, the copy/create path, filename-variable resolution, or localization.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see
anti-anchoring below). Root: `src/modules/NewPlus/`. Unqualified files live under
`NewShellExtensionContextMenu/`.

| Sub-feature | Implementation (file · function) |
|---|---|
| Win11 context-menu entry (IExplorerCommand) | `shell_context_menu.cpp` `shell_context_menu::GetTitle/GetIcon/GetState/GetFlags/EnumSubCommands`; COM class `dll_main.cpp` `CoCreatableClass(shell_context_menu)` |
| Win11 submenu enumeration (templates + separator + "Open templates") | `shell_context_sub_menu.cpp` ctor + `::Next` |
| Win11 per-item command (title/icon/invoke) | `shell_context_sub_menu_item.cpp` |
| Win10 classic context menu (IContextMenu) | `NewShellExtensionContextMenu.win10/shell_context_menu_win10.cpp` `QueryContextMenu`, `InvokeCommand` |
| Win10 runtime registration (registry + version sentinel) | `RuntimeRegistration.h` `NewPlusRuntimeRegistration::EnsureRegisteredWin10/Unregister` → `common/utils/shell_ext_registration.h` |
| Win11 sparse-package registration | `new_utilities.h` `register_msix_package` (`package::RegisterSparsePackage`, `IsPackageRegisteredWithPowerToysVersion`) |
| Module lifecycle, enable/disable, GPO gate | `powertoys_module.cpp` `NewModule::enable/disable/init_settings/UpdateRegistration/gpo_policy_enabled_configuration` |
| Template-folder scan / sort (dirs first) / hidden-system exclude | `template_folder.cpp` `rescan_template_folder`; exclude via `helpers_variables.h` `exclude_item` |
| Template item model: menu title, target filename, **leading-digit stripping** | `template_item.cpp` `get_menu_title`, `get_target_filename`, `remove_starting_digits_from_filename` |
| Copy template → create file/folder, unique name, telemetry, rename | `new_utilities.h` `copy_template`; `template_item.cpp` `copy_object_to` (`SHFileOperation` FO_COPY) |
| Unique-name generation ` (1)`, ` (2)`… | `helpers_filesystem.h` `make_unique_path_name` |
| Valid-filename sanitize (strip `\ / : * ? " < > |`) | `helpers_filesystem.h` `make_valid_filename` |
| Filename variables: `$PARENT_FOLDER_NAME`, `%ENV%`, date/time tokens | `helpers_variables.h` `resolve_variables_in_filename`, `resolve_variables_in_path`, `resolve_environment_variables`, `resolve_parent_folder`, `resolve_date_time_variables` |
| Date/time token engine (`$YYYY $MM $DD $hh $mm $ss $fff $TT`…) | `Helpers.cpp` `GetDatedFileName` (copied subset of PowerRename) |
| Recursive variable resolution + rename inside copied folders (leaf-first) | `helpers_variables.h` `resolve_variables_in_filename_and_rename_files` |
| Enter-Explorer-rename-mode workaround (detached thread + 50 ms sleep) | `template_item.cpp` `enter_rename_mode`/`rename_on_other_thread_workaround`; `new_utilities.h` `explorer_enter_rename_mode` (incl. desktop/multi-monitor repositioning) |
| Hide/restore built-in Windows "New" (registry) | `new_utilities.h` `disable_built_in_new_via_registry`/`enable_built_in_new_via_registry`; Settings UI `src/settings-ui/Settings.UI/ViewModels/NewPlusViewModel.cs` |
| Settings JSON load/save, GPO reads, default template location | `settings.cpp` `NewSettings::*`, `GetTemplateLocationDefaultPath` (`…\NewPlus\Templates`) |
| Settings keys / package names / icon paths / `$PARENT_FOLDER_NAME` literal | `constants.h` |
| Theme icons (light/dark), Explorer icon extraction | `new_utilities.h` `get_new_icon_resource_filepath`, `get_explorer_icon_handle` |
| Template-count telemetry global (HACK) | `new_utilities.cpp` `get/set_saved_number_of_templates` |
| Telemetry events | `trace.cpp` (`EventShowTemplateItems`, `EventCopyTemplate`, `EventOpenTemplates`, `EventToggleOnOff`) |

**Menu title is localized** via `GET_RESOURCE_STRING_FALLBACK(IDS_CONTEXT_MENU_ITEM_NEW, L"New+")`
in `shell_context_menu.cpp` and `powertoys_module.cpp::get_name`.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Leading-digit stripping in template names
- **Symptom:** template file/folder named `01. Name` shows/creates with wrong name; folders or files
  whose stem is **only digits** (`001231`, `001231.txt`) get mangled; `01..Name` / `01 . Name`
  keep a stray leading `.`/space.
- **Where:** `template_item.cpp::remove_starting_digits_from_filename` (used by `get_menu_title` and
  `get_target_filename`).
- **Root cause:** the digit/separator stripper had edge cases — numeric-only stems, and stripping
  only a single separator char after the digit run.
- **Guardrail:** keep the numeric-only-stem guard (files always kept; folders stripped only if they
  look like they have an extension) **and** skip **all** consecutive `.`/space after the digit run.
  Evidence: [PR #45439](https://github.com/microsoft/PowerToys/pull/45439) (Copilot flagged the
  single-char-strip regression), issue [#46871](https://github.com/microsoft/PowerToys/issues/46871).

### Hide built-in "New" — registry edits & toggle logic
- **Symptom:** toggling "Hide built-in New" does nothing / inverts; `NullReferenceException` in
  Settings; only part of the sentinel value written to the registry; built-in New not hidden after
  disable→re-enable of New+; hiding it breaks File Explorer **Ctrl+Shift+N**.
- **Where:** `NewPlusViewModel.cs` `HideBuiltInNew` setter; `new_utilities.h`
  `disable_built_in_new_via_registry`/`enable_built_in_new_via_registry`;
  `powertoys_module.cpp::init_settings`.
- **Root cause (all flagged in review):** setter branched on the **old** field instead of the new
  `value`; `OpenSubKey` result used without a null check; `RegSetValueExW` size passed as character
  count (must be `(lstrlenW(str)+1)*sizeof(wchar_t)` bytes) and `reinterpret_cast<const BYTE*>(&ptr)`
  passed the address of the pointer, not the string; functions return `true` on failure.
- **Guardrail:** branch on the new `value`; null-check every `OpenSubKey`; compute REG_SZ byte size
  incl. terminator and pass the pointer directly; re-apply the hide preference on `enable()`; be
  aware hiding the built-in New affects the native Ctrl+Shift+N shortcut. Evidence:
  [PR #44979](https://github.com/microsoft/PowerToys/pull/44979) review comments; issues
  [#48013](https://github.com/microsoft/PowerToys/issues/48013),
  [#46026](https://github.com/microsoft/PowerToys/issues/46026).

### Context menu missing on Win11 (classic menu) / after upgrade / in dialogs
- **Symptom:** "New+" entry absent on Win11 when the classic context menu is enabled, missing after
  an upgrade, missing in file-picker/dialog Explorer windows, or templates missing from the submenu.
- **Where:** Win11 path is `shell_context_menu.cpp` (IExplorerCommand) + sparse-MSIX registration
  `new_utilities.h::register_msix_package` / `powertoys_module.cpp::enable/UpdateRegistration`;
  Win10 path is `shell_context_menu_win10.cpp::QueryContextMenu`, which **returns `E_FAIL` when
  `IsWin11OrGreater()`** — so the classic (Win10-style) menu on Win11 gets no New+ entry.
- **Root cause:** registration lifecycle + the modern IExplorerCommand extension not surfacing in the
  Win11 *classic* menu; template list is rebuilt each invocation from the scanned folder.
- **Guardrail:** keep sparse-package register/unregister idempotent and version-aware; confirm which
  menu surface (modern vs classic) the report targets before "fixing" the wrong handler. Evidence:
  issues [#47609](https://github.com/microsoft/PowerToys/issues/47609),
  [#47066](https://github.com/microsoft/PowerToys/issues/47066),
  [#49316](https://github.com/microsoft/PowerToys/issues/49316),
  [#48125](https://github.com/microsoft/PowerToys/issues/48125),
  [#49068](https://github.com/microsoft/PowerToys/issues/49068).

### Hidden/system files leaking into (or missing from) the template list
- **Symptom:** hidden/system files (e.g. `desktop.ini`, `Thumbs.db`) appear as templates.
- **Where:** `template_folder.cpp::rescan_template_folder` → `helpers_variables.h::exclude_item`
  (checks `FILE_ATTRIBUTE_HIDDEN | FILE_ATTRIBUTE_SYSTEM`).
- **Guardrail:** route every template enumeration and every copy-time recursive rename through
  `exclude_item`; don't add ad-hoc filters. Evidence:
  [PR #45439](https://github.com/microsoft/PowerToys/pull/45439) (hidden/system exclusion added).

### Localization of the "New+" name and template names
- **Symptom:** wrong translation of the product/menu name (French rendered "Nouveauté+" instead of
  "Nouveau+"; Japanese "新規" should be "新規作成").
- **Where:** resource string `IDS_CONTEXT_MENU_ITEM_NEW` (menu title in `shell_context_menu.cpp`,
  `shell_context_menu_win10.cpp`, `powertoys_module.cpp::get_name`); translations under
  `src/settings-ui`/module `.resx`.
- **Guardrail:** "New+" is a product name — verify locale-specific guidance before changing resx;
  template **file names** are literal on disk and are localized only by the user's template folder,
  not by resources. Evidence: [PR #47225](https://github.com/microsoft/PowerToys/pull/47225),
  issue [#46827](https://github.com/microsoft/PowerToys/issues/46827).

### PowerShell build-step reliability (resx → rc)
- **Symptom:** the `convert-resx-to-rc.ps1` build step fails when the repo path contains spaces, or
  masks real warnings.
- **Where:** MSBuild `-Command` invocation (e.g. `runner.vcxproj`) shared by module resource builds.
- **Root cause:** unquoted `$(MSBuildThisFileDirectory)` argument splitting; `$WarningPreference =
  'SilentlyContinue'` hid script warnings.
- **Guardrail:** quote path arguments; disable PowerShell **module auto-loading** rather than
  suppressing warnings. Evidence: [PR #46729](https://github.com/microsoft/PowerToys/pull/46729).

## Review Rules

Enforce these when reviewing or authoring New+ changes:

- **Property setters must branch on the new `value`, not the current field** — the HideBuiltInNew
  toggle inverted because it read `_disableBuiltInNew`
  ([PR #44979](https://github.com/microsoft/PowerToys/pull/44979)).
- **Null-check every `RegistryKey.OpenSubKey`** before `GetValue`/`SetValue`/`DeleteValue` — missing
  keys throw `NullReferenceException` ([.NET Registry docs](https://learn.microsoft.com/en-us/dotnet/api/microsoft.win32.registrykey.opensubkey);
  pattern in `KeyboardManagerViewModel.cs`; PR #44979).
- **REG_SZ sizes are bytes incl. the terminator.** Pass `(lstrlenW(str)+1)*sizeof(wchar_t)` to
  `RegSetValueExW`, and pass the string pointer — not `&pointer`
  ([RegSetValueExW](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regsetvalueexw); PR #44979).
- **Preserve the digit-stripping edge cases** (numeric-only guard + strip *all* separators) whenever
  touching `remove_starting_digits_from_filename` ([PR #45439](https://github.com/microsoft/PowerToys/pull/45439)).
- **Keep hidden/system files excluded** from the template list via `exclude_item` (PR #45439).
- **Route new filename inputs through `make_valid_filename`** — copy targets and resolved variables
  must strip `\ / : * ? " < > |` before hitting `SHFileOperation`.
- **Know the two menu surfaces.** Win11 = IExplorerCommand (`shell_context_menu.cpp`); Win10 /
  classic = IContextMenu (`shell_context_menu_win10.cpp`, which bails on Win11). Fix the surface the
  bug actually targets.
- **Quote path args in PowerShell build steps; don't suppress warnings**
  ([PR #46729](https://github.com/microsoft/PowerToys/pull/46729)).
- **Don't reorder `Microsoft.Cpp.Default.props` imports; use `$(RepoRoot)` not relative paths**
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639) — import order is "very sensitive";
  central deps in `Directory.Packages.props`).
- **"New+" is a localized product name** — treat resx changes with locale care (PRs #47225, #46827).

## Pitfalls

- **`saved_number_of_templates` is a global HACK** initialized to `(size_t)-1`
  (`new_utilities.cpp`); `copy_template`/`open_template_folder` gate telemetry on `>= 0`, which is
  effectively always true for `size_t`. Don't rely on it as a "was the menu shown?" flag.
- **Explorer rename-mode can't run on the main thread.** `enter_rename_mode` **detaches a thread**
  and sleeps ~50 ms so the icon is drawn first; desktop/multi-monitor creation needs the special
  `SelectAndPositionItems` path (`explorer_enter_rename_mode`). Don't "simplify" it to a synchronous
  call. Related: issue [#46797](https://github.com/microsoft/PowerToys/issues/46797) (multi-monitor).
- **`SHFILEOPSTRUCT` paths must be double-null-terminated.** `copy_object_to` uses `MAX_PATH + 1`
  buffers and writes a second `\0`; long paths risk truncation.
- **New+ is disabled by default** (`is_enabled_by_default() == false`).
- **Hiding built-in New requires the runner running and the module loaded** (applied in
  `init_settings`) — invoking the context menu alone won't apply the preference.
- **Variables resolve top-down for `$PARENT_FOLDER_NAME`; copied folders rename leaf-first** to avoid
  re-scans (`resolve_variables_in_path`, `resolve_variables_in_filename_and_rename_files`).
- **The date/time token engine is a *copied subset* of PowerRename's `GetDatedFileName`** — it is not
  shared code; fixes in PowerRename don't propagate here automatically.
- **The default template folder is auto-created if missing** (`create_folder_if_not_exist`) at
  `…\NewPlus\Templates`.

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim these playbooks and then hunt the diff for their themes —
that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

When localizing a bug, if the symptom doesn't map cleanly to a row above, reason from the symptom and
verify in source — a thin/absent map entry can anchor you onto a confident, wrong file.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a New+ PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/NewPlus/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/NewPlus)
- [Sparse packages](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/grant-identity-to-nonpackaged-apps) · [IExplorerCommand](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-iexplorercommand) · [RegSetValueExW](https://learn.microsoft.com/en-us/windows/win32/api/winreg/nf-winreg-regsetvalueexw)
