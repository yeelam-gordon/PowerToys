# File Locksmith Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

## Role split

`SKILL.md` owns current symptom, root-cause, and guardrail guidance. This catalog preserves the
historical evidence trail, source anchors, reviewer decisions, unresolved reports, and caveats used
to audit or refresh that guidance. Confirm source anchors before relying on them.

## Evidence ledger

| Sequence | Evidence | Source anchors | Recorded outcome / reviewer decision |
|---|---|---|---|
| IPC report and fix | [#46949](https://github.com/microsoft/PowerToys/issues/46949) → [PR #47361](https://github.com/microsoft/PowerToys/pull/47361) | `FileLocksmithLib/IPC.cpp::Writer::start`; `FileLocksmithLibInterop/NativeMethods.cpp::StartAsElevated`; `NativeMethods.cpp::ReadPathsFromFile` | UTF-16 `last-run.log` writers were changed to binary mode. Review found the second writer after the initial patch covered only the first, so both paths are part of the accepted fix. |
| IPC follow-up reports | [#46948](https://github.com/microsoft/PowerToys/issues/46948), [#46951](https://github.com/microsoft/PowerToys/issues/46951) | `IPC.cpp::Writer::start`; `IPC.h::get_read_handle`; context-menu callers | Accepted follow-up checks `m_stream.is_open()` and returns `E_FAIL`; review also identified double `start()` calls and a declaration with no implementation. |
| Icon-crash chronology | [#48693](https://github.com/microsoft/PowerToys/issues/48693) → [PR #48719](https://github.com/microsoft/PowerToys/pull/48719) | `FileLocksmithUI/Converters/PidToIconConverter.cs::Convert`; `App.xaml.cs::App_UnhandledException` | The merged fallback catches extraction failure and returns a placeholder. A `File.Exists` fast path was suggested in review but is not implemented in current source. |
| Handle watchdog design | In-source design note citing the known `NtQueryObject` hang and StackOverflow “Enumerate handles”; related reports [#47200](https://github.com/microsoft/PowerToys/issues/47200), [#46944](https://github.com/microsoft/PowerToys/issues/46944), [#45158](https://github.com/microsoft/PowerToys/issues/45158) | `FileLocksmithLibInterop/NtdllExtensions.cpp::handles`, `file_handle_to_kernel_name` | The detached per-handle worker, 200 ms progress poll, `TerminateThread`, and index resume are documented as an intentional unsafe HACK because no timeout-capable replacement is known. |
| Context-menu reports | [#48951](https://github.com/microsoft/PowerToys/issues/48951), [#45863](https://github.com/microsoft/PowerToys/issues/45863), [#44394](https://github.com/microsoft/PowerToys/issues/44394), [#44374](https://github.com/microsoft/PowerToys/issues/44374) | `FileLocksmithExt/PowerToysModule.cpp::enable`; `FileLocksmithExt/RuntimeRegistration.h`; `ExplorerCommand.cpp`; `FileLocksmithContextMenu/dllmain.cpp` | Windows 11 registers the sparse package and then still runs classic registration management; older systems use the classic path. Both handlers share enabled/extended-menu gating, and #44394 records a duplicate visible entry. |
| Launch failure report | [#46950](https://github.com/microsoft/PowerToys/issues/46950) | `FileLocksmithExt/ExplorerCommand.cpp::LaunchUI` | The report records that `CreateProcessW` failure was ignored while `S_OK` was returned; launch results must be surfaced or traced. |
| Context-menu assets | [#48924](https://github.com/microsoft/PowerToys/issues/48924) → [PR #48925](https://github.com/microsoft/PowerToys/pull/48925) | `FileLocksmithContextMenuPackage.msix` assets | The accepted asset set covers the standard MSIX context-menu logo sizes. |
| Project references | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | File Locksmith project files | Project references use `$(RepoRoot)` rather than bare relative paths. |
| Path matching | Source verification | `FileLocksmith.cpp::kernel_paths_contain`, `path_to_kernel_name`, `find_processes_recursive` | Directory-prefix comparison includes a `\` boundary and maps kernel-name suffixes back to the user path. Process matches include open handles and loaded modules. |
| Elevation/policy | Source verification | `NativeMethods.cpp::SetDebugPrivilege`; `App.xaml.cs::OnLaunched`; `getConfiguredFileLocksmithEnabledValue`; `GPOWrapper.GetConfiguredFileLocksmithEnabledValue` | Elevated scans enable `SE_DEBUG_NAME`; startup exits when disabled by policy. |

## Decision ledger

| Decision | Status | Evidence / anchor |
|---|---|---|
| Every `last-run.log` writer uses binary mode and remains byte-compatible with the UTF-16 reader. | Accepted | [PR #47361](https://github.com/microsoft/PowerToys/pull/47361); `ReadPathsFromFile` |
| Stream-open success is checked explicitly; default `ofstream` exceptions are not assumed. | Accepted | [#46948](https://github.com/microsoft/PowerToys/issues/46948); `IPC.cpp::Writer::start` |
| Per-row icon extraction degrades to a placeholder; avoiding repeated exception control flow remains an unimplemented review suggestion. | Partial: fallback merged, fast path absent | [PR #48719](https://github.com/microsoft/PowerToys/pull/48719) |
| The handle-enumeration watchdog remains despite unsafe termination/leak risk. | Accepted technical debt | `NtdllExtensions.cpp::handles` |
| Sparse-package and classic registration management may coexist on Windows 11; both handlers stay identically gated and must not create duplicate visible entries. | Accepted | `PowerToysModule.cpp::enable`; `RuntimeRegistration.h`; both context-menu handlers |
| Directory matching retains the separator boundary and kernel-name comparison space. | Accepted | `FileLocksmith.cpp::kernel_paths_contain` |
| Elevated visibility requires `SeDebugPrivilege`; module startup remains GPO-gated. | Accepted | `NativeMethods::SetDebugPrivilege`; `App.xaml.cs::OnLaunched` |
| Launch APIs must have their return values checked. | Known current violation: `LaunchUI` ignores `CreateProcessW` result | [#46950](https://github.com/microsoft/PowerToys/issues/46950); current `ExplorerCommand.cpp` |

## Open issues and evidence caveats

- [#46950](https://github.com/microsoft/PowerToys/issues/46950) remains a known current launch-path
  violation: `ExplorerCommand.cpp::LaunchUI` ignores the `CreateProcessW` return value.
- The watchdog evidence is primarily an in-source design comment plus related crash reports; the
  reports [#47200](https://github.com/microsoft/PowerToys/issues/47200),
  [#46944](https://github.com/microsoft/PowerToys/issues/46944), and
  [#45158](https://github.com/microsoft/PowerToys/issues/45158) do not independently prove that each
  incident had the same blocking handle.
- Context-menu reports span registration, settings, install state, and OS version. Confirm which
  handler is active before attributing a missing or duplicate entry.
- Repo-wide changes were excluded unless they established a File Locksmith-specific decision:
  .NET 10 #41280, VS 2026 #44304, CppWinRT #45420, WASDK/title-bar #45532, CLI telemetry #46872,
  PowerShell invocation #46729, signing #44609, and empty-title guard #49069. PR
  [#44639](https://github.com/microsoft/PowerToys/pull/44639) and PR
  [#48925](https://github.com/microsoft/PowerToys/pull/48925) were retained for direct project/asset
  impact.
- Environment/setup reports without module-specific logic evidence were excluded, including .NET
  Desktop Runtime #48432 and settings reset #46165.
