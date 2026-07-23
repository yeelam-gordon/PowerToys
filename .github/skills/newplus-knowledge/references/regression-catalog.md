# New+ Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each claim
in source before acting. Symptoms map to `src/modules/NewPlus/` (unqualified files under
`NewShellExtensionContextMenu/`).

## Key Decisions (context for the playbooks)

- **Two context-menu surfaces.** Win11 uses the modern **IExplorerCommand** extension
  (`shell_context_menu.cpp`, registered as a **sparse MSIX** via `register_msix_package`); Win10 and
  the Win11 *classic* menu use the legacy **IContextMenu** handler
  (`NewShellExtensionContextMenu.win10/shell_context_menu_win10.cpp`), which **returns `E_FAIL` when
  `IsWin11OrGreater()`**. This split is the root of most "menu missing on Win11 classic" reports.
- **Win10 registration is runtime, registry-based, version-sentineled.** `RuntimeRegistration.h`
  builds a `runtime_shell_ext::Spec` (CLSID `{FF90D477-…5401}`, sentinel
  `Software\Microsoft\PowerToys\NewPlus\ContextMenuRegisteredWin10`) and delegates to
  `common/utils/shell_ext_registration.h`. `powertoys_module.cpp::UpdateRegistration` gates it behind
  `ENABLE_REGISTRATION || NDEBUG`.
- **Template folder = user-owned directory.** `template_folder.cpp::rescan_template_folder` lists the
  folder (dirs first, then files, each sorted), excluding hidden/system entries via `exclude_item`.
  Default location `…\NewPlus\Templates` is auto-created if missing (`settings.cpp
  ::GetTemplateLocationDefaultPath`, `create_folder_if_not_exist`).
- **Create = copy + resolve + rename.** `new_utilities.h::copy_template` copies via
  `SHFileOperation(FO_COPY)`, expands filename variables, makes the name unique, touches
  last-write-time, refreshes the shell, then enters rename mode on a detached thread.
- **Filename variables.** `$PARENT_FOLDER_NAME` (case-sensitive), `%ENV%` (case-insensitive), and
  PowerRename-style date tokens; resolution is top-down over the path so parent names are available
  to children (`resolve_variables_in_path`).
- **`GetDatedFileName` is a copied subset of PowerRename**, deliberately duplicated "to avoid
  cross-module dependencies" (comment in `Helpers.cpp`). Fixes do not auto-propagate between modules.
- **New+ is opt-in.** `is_enabled_by_default() == false`; enabled state lives in general settings,
  module settings in `NewPlus/settings.json`; GPO can force enable/disable and gate features.

## Regression / Review Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| Digit stripping | `01. Name` wrong; numeric-only stem mangled; stray leading `.`/space | `template_item.cpp::remove_starting_digits_from_filename` | Numeric-only edge cases; only one separator stripped | Keep numeric-only guard; strip all consecutive `.`/space | [PR #45439](https://github.com/microsoft/PowerToys/pull/45439), [#46871](https://github.com/microsoft/PowerToys/issues/46871) |
| Exclusion | Hidden/system files listed as templates | `template_folder.cpp::rescan_template_folder`; `helpers_variables.h::exclude_item` | No hidden/system filter | Exclude `FILE_ATTRIBUTE_HIDDEN|SYSTEM` in all scans | [PR #45439](https://github.com/microsoft/PowerToys/pull/45439) |
| Hide built-in New | Toggle inverts / no-op | `NewPlusViewModel.cs` HideBuiltInNew setter | Branches on old field, not new `value` | Branch on new `value` | [PR #44979](https://github.com/microsoft/PowerToys/pull/44979) |
| Hide built-in New | `NullReferenceException` | `NewPlusViewModel.cs` `OpenSubKey` calls | Null key used without check | Null-check `OpenSubKey` | [PR #44979](https://github.com/microsoft/PowerToys/pull/44979) |
| Hide built-in New | Partial registry value written | `new_utilities.h::disable_built_in_new_via_registry` | `RegSetValueExW` size = char count; `&ptr` passed | Bytes incl. terminator: `(len+1)*sizeof(wchar_t)`; pass pointer | [PR #44979](https://github.com/microsoft/PowerToys/pull/44979) |
| Hide built-in New | Not hidden after disable→re-enable | `powertoys_module.cpp::enable` vs `init_settings` | Preference only applied in `init_settings` | Re-apply hide preference on `enable()` | [PR #44979](https://github.com/microsoft/PowerToys/pull/44979) |
| Native shortcut | Hiding built-in New breaks Ctrl+Shift+N | built-in `New` ContextMenuHandlers registry | Disabling the built-in handler removes the shortcut | Weigh trade-off; document | [#48013](https://github.com/microsoft/PowerToys/issues/48013), [#46026](https://github.com/microsoft/PowerToys/issues/46026) |
| Context menu | Missing on Win11 classic menu / after upgrade / in dialogs | `shell_context_menu_win10.cpp::QueryContextMenu` (E_FAIL on Win11); `register_msix_package` | IExplorerCommand not in classic menu; registration lifecycle | Target correct surface; idempotent version-aware registration | [#47609](https://github.com/microsoft/PowerToys/issues/47609), [#47066](https://github.com/microsoft/PowerToys/issues/47066), [#49316](https://github.com/microsoft/PowerToys/issues/49316), [#48125](https://github.com/microsoft/PowerToys/issues/48125), [#49068](https://github.com/microsoft/PowerToys/issues/49068) |
| Rename mode | Rename not entered / wrong monitor | `template_item.cpp::enter_rename_mode`; `new_utilities.h::explorer_enter_rename_mode` | Must run off main thread; desktop needs reposition path | Keep detached thread + sleep; desktop `SelectAndPositionItems` | [#46797](https://github.com/microsoft/PowerToys/issues/46797) |
| Localization | Wrong "New+" translation | resource `IDS_CONTEXT_MENU_ITEM_NEW`; module `.resx` | Locale-specific naming | Verify per-locale guidance | [PR #47225](https://github.com/microsoft/PowerToys/pull/47225), [#46827](https://github.com/microsoft/PowerToys/issues/46827) |
| Build script | resx→rc PS step fails on spaced path / hides warnings | MSBuild `-Command` (e.g. `runner.vcxproj`) | Unquoted `$(MSBuildThisFileDirectory)`; `WarningPreference` | Quote args; disable PS module auto-load | [PR #46729](https://github.com/microsoft/PowerToys/pull/46729) |
| Build | Win10 New+ build fails (missing generated header) | Win10 `.vcxproj` include paths | Missing local `Generated Files` include path | Add local include path | [PR #43361](https://github.com/microsoft/PowerToys/pull/43361), [PR #43461](https://github.com/microsoft/PowerToys/pull/43461) |
| Packaging | Project paths / prop import order | `*.vcxproj`, `Cpp.Build.props`, `Directory.Packages.props` | Relative paths; reordered `Microsoft.Cpp.Default.props` | Use `$(RepoRoot)`; never reorder import; central deps | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) |
| Servicing | PowerToys couldn't quit safely | repo-wide CRT config | Hybrid CRT for size | Reverted — teardown outranks size | [PR #43484](https://github.com/microsoft/PowerToys/pull/43484) |

## Common Practices (enforced in review)

- **Sanitize before filesystem ops.** Copy targets and resolved variables pass through
  `make_valid_filename` (strips `\ / : * ? " < > |`) and `make_unique_path_name` before
  `SHFileOperation`.
- **Route enumeration + copy-time rename through `exclude_item`** so hidden/system files never leak.
- **Registry writes follow the Win32 REG_SZ contract** (byte size incl. terminator, pointer not
  address-of); prefer the pattern in `common/utils/shell_ext_registration.h`.
- **Settings round-trip through `constants.h` keys** and `NewSettings::Save/ParseJson`; GPO reads live
  in `settings.cpp` getters — don't bypass them.
- **Telemetry** flows through `trace.cpp` (`EventShowTemplateItems`, `EventCopyTemplate`,
  `EventOpenTemplates`, `EventToggleOnOff`); the `saved_number_of_templates` global is a HACK, not a
  reliable gate.

---
*Corpus: 12 merged PRs, 116 review comments, 30 candidate issues (New+-specific subset) + source
verification against `src/modules/NewPlus`. Non-New+ issues that matched the keyword "new"
(Keyboard Manager "new editor", CmdPal, Shortcut Guide, Workspaces) were excluded as noise.*
