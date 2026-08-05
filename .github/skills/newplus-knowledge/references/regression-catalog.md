# New+ — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This catalog is the progressive-disclosure evidence record for `SKILL.md`. Source paths are under
`src/modules/NewPlus/`; unqualified native files are under `NewShellExtensionContextMenu/`.

> **Role split:** `SKILL.md` owns actionable symptom → root-cause → guardrail guidance. This file
> owns provenance: exact source anchors, historical decisions, reviewer rationale, unresolved issue
> clusters, chronology, and confidence caveats. Do not duplicate the playbook prose here.

## Verified source-anchor ledger

| Area | Exact source anchors | Evidence retained |
|---|---|---|
| Win11 menu surface | `shell_context_menu.cpp`; `new_utilities.h::register_msix_package` | Modern `IExplorerCommand` extension registered through a sparse MSIX. |
| Win10/classic surface | `NewShellExtensionContextMenu.win10/shell_context_menu_win10.cpp::QueryContextMenu` | Legacy `IContextMenu` handler returns `E_FAIL` on `IsWin11OrGreater()`. |
| Win10 registration | `RuntimeRegistration.h`; `common/utils/shell_ext_registration.h`; `powertoys_module.cpp::UpdateRegistration` | CLSID `{FF90D477-…5401}` and sentinel `Software\Microsoft\PowerToys\NewPlus\ContextMenuRegisteredWin10`; build gate is `ENABLE_REGISTRATION \|\| NDEBUG`. |
| Template enumeration | `template_folder.cpp::rescan_template_folder`; `helpers_variables.h::exclude_item` | Directories then files, sorted within each group; hidden/system entries excluded. |
| Default folder | `settings.cpp::GetTemplateLocationDefaultPath`; `create_folder_if_not_exist` | Default `…\NewPlus\Templates` directory is created when absent. |
| Create/rename pipeline | `new_utilities.h::copy_template`; `template_item.cpp::enter_rename_mode`; `new_utilities.h::explorer_enter_rename_mode` | Copy, resolve variables, uniquify, update write time, refresh shell, then request rename on a detached thread; desktop uses `SelectAndPositionItems`. |
| Variable resolution | `helpers_variables.h::resolve_variables_in_path`; `Helpers.cpp::GetDatedFileName` | `$PARENT_FOLDER_NAME` is case-sensitive; `%ENV%` is case-insensitive; path traversal is top-down. Date-token code is intentionally copied from PowerRename to avoid cross-module dependencies. |
| Built-in New toggle | `NewPlusViewModel.cs::HideBuiltInNew`; `new_utilities.h::disable_built_in_new_via_registry`; `powertoys_module.cpp::enable`, `init_settings` | UI setter, registry contract, and module re-enable are distinct lifecycle points. |
| Settings/policy/telemetry | `constants.h`; `NewSettings::Save`, `ParseJson`; `settings.cpp` getters; `trace.cpp` | New+ is disabled by default; GPO reads occur in getters. Telemetry includes `EventShowTemplateItems`, `EventCopyTemplate`, `EventOpenTemplates`, and `EventToggleOnOff`. |

## Decision chronology

Ordered by the repository history represented in this corpus.

| Change | Evidence | Decision / reviewer record |
|---|---|---|
| Win10 generated-header build repair | [PR #43361](https://github.com/microsoft/PowerToys/pull/43361), [PR #43461](https://github.com/microsoft/PowerToys/pull/43461) | Add the local `Generated Files` include path required by the Win10 project. |
| CRT servicing rollback | [PR #43484](https://github.com/microsoft/PowerToys/pull/43484) | Hybrid CRT size optimization was reverted because reliable PowerToys teardown/quit behavior outranked binary-size savings. |
| Project-path/import normalization | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | Use `$(RepoRoot)` and central dependencies; reviewer record says `Microsoft.Cpp.Default.props` import order is sensitive and must not be reordered. |
| Built-in New toggle/registry review | [PR #44979](https://github.com/microsoft/PowerToys/pull/44979) | Reviewer decisions: branch on setter `value`; null-check `OpenSubKey`; write `REG_SZ` as bytes including terminator and pass the string pointer, not its address; reapply the preference on module enable. |
| Template-name and exclusion fixes | [PR #45439](https://github.com/microsoft/PowerToys/pull/45439) | Preserve numeric-only edge handling, remove every consecutive leading dot/space after the digit prefix, and centralize hidden/system exclusion through `exclude_item`. |
| Resource-build PowerShell reliability | [PR #46729](https://github.com/microsoft/PowerToys/pull/46729) | Quote paths containing `$(MSBuildThisFileDirectory)`; disable module auto-loading rather than suppressing warnings. |
| Locale-specific product naming | [PR #47225](https://github.com/microsoft/PowerToys/pull/47225) | “New+” is a localized product name; translation decisions require locale-specific review rather than literal substitution. |

## Open symptom-cluster ledger

These are issue signals, not established root causes. Confirm current registration state, Windows
surface, settings, and source behavior before changing code.

| Cluster | Reports | Current evidence boundary |
|---|---|---|
| Digit-prefixed template names | [#46871](https://github.com/microsoft/PowerToys/issues/46871) (closed completed June 2, 2026) | Historical source anchor is `template_item.cpp::remove_starting_digits_from_filename`; the issue does not supersede the edge-case decisions in PR #45439. |
| Native Ctrl+Shift+N interaction | [#48013](https://github.com/microsoft/PowerToys/issues/48013), [#46026](https://github.com/microsoft/PowerToys/issues/46026) | Disabling the built-in Windows handler can remove the native shortcut. This is a product trade-off, not evidence that New+ can independently preserve the shortcut. |
| Missing/unstable context menu | [#47609](https://github.com/microsoft/PowerToys/issues/47609), [#47066](https://github.com/microsoft/PowerToys/issues/47066), [#49316](https://github.com/microsoft/PowerToys/issues/49316), [#48125](https://github.com/microsoft/PowerToys/issues/48125), [#49068](https://github.com/microsoft/PowerToys/issues/49068) | Reports span Win11 classic menu, upgrades, dialogs, and submenu population. Modern/classic surface and registration lifecycle must be distinguished before grouping causes. |
| Rename-mode placement | [#46797](https://github.com/microsoft/PowerToys/issues/46797) (closed completed April 18, 2026) | Historical multi-monitor/desktop behavior points to the detached rename request and desktop positioning path; timing remains environment-sensitive. |
| Locale naming | [#46827](https://github.com/microsoft/PowerToys/issues/46827) | Locale-specific wording report; template filenames remain user-owned disk names rather than resource-localized strings. |

## Caveats and exclusions

- `saved_number_of_templates` is a `size_t` global initialized to `(size_t)-1`; its telemetry gate
  must not be interpreted as reliable proof that the menu was previously shown.
- `SHFILEOPSTRUCT` paths and Explorer rename timing retain platform constraints; this ledger records
  the historical anchors but does not establish long-path or deterministic-timing guarantees.
- The date-token implementation is duplicated intentionally. A PowerRename fix is not evidence that
  New+ changed unless `Helpers.cpp::GetDatedFileName` changed too.
- “Missing menu” reports are not interchangeable: Win11 modern, Win11 classic, Win10 classic,
  dialogs, package registration, and template enumeration are separate evidence paths.
- Corpus scope: 12 merged PRs, 116 review comments, 30 candidate issues, plus source verification.
  Keyboard Manager, Command Palette, Shortcut Guide, Workspaces, and other keyword-only “new”
  matches were excluded as noise.
