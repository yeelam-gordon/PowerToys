---
name: powertoys-filelocksmith-knowledge
description: 'PowerToys File Locksmith module knowledge: feature->file/function map, recurring regression playbooks (NtQuerySystemInformation handle enumeration + NtQueryObject hang watchdog, path->kernel-name matching, process kill/EndTask, elevation + SeDebugPrivilege + runas, last-run.log IPC binary vs text mode Unicode corruption, per-row icon extraction crash, Win10 classic vs Win11 sparse-MSIX context menu, extended-menu gating), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/FileLocksmith — handle enumeration, locking-process discovery, kill process, elevation, context-menu registration, IPC. Keywords: File Locksmith, FileLocksmith, locked file, which process locks, handle, NtQuerySystemInformation, NtQueryObject, SystemExtendedHandleInformation, kernel name, EndTask, kill process, SeDebugPrivilege, elevated, runas, context menu, sparse package, MSIX, shell extension, IExplorerCommand, IPC, last-run.log, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys File Locksmith Knowledge

Grounded engineering knowledge for the PowerToys **File Locksmith** module — a Windows utility that
finds which running processes hold a handle to (lock) the selected files/folders, and lets the user
end those processes. It is invoked from the Explorer context menu, enumerates system handles via
undocumented `Nt*` APIs, matches them against the selected paths by kernel object name, and can
restart elevated to see handles held by other users / system processes.

Use this file to localize code fast and avoid known regression traps (handle-enumeration hangs, IPC
Unicode corruption, per-row icon crashes, Win10-vs-Win11 context-menu registration, elevation gating).

## When to Use This Skill

- Planning or implementing a change under `src/modules/FileLocksmith/` and needing prior art.
- Fixing/triaging a File Locksmith bug: UI crashes after a few seconds or when scanning an
  executable, crash `0xc000027b` on a listed process, non-ASCII / Unicode paths corrupted when
  restarting elevated, the context-menu entry missing / duplicated / not appearing after install,
  scan hangs, "End task" fails, elevated scan shows no system processes.
- Reviewing a File Locksmith PR against maintainer conventions and regression traps.
- Touching handle enumeration, the path→process matching, process kill, elevation/`runas`, the
  `last-run.log` IPC, or the classic (Win10) / sparse-MSIX (Win11) shell-extension registration.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring
below). Root: `src/modules/FileLocksmith/`.

| Sub-feature | Implementation (file · symbol) |
|---|---|
| Enumerate all system handles | `FileLocksmithLibInterop/NtdllExtensions.cpp::handles` (`NtQuerySystemInformation(SystemExtendedHandleInformation)`) |
| Grow buffer until it fits | `NtdllExtensions.cpp::NtQuerySystemInformationMemoryLoop` (doubles on `STATUS_INFO_LENGTH_MISMATCH`, capped at `MaxResultBufferSize`) |
| Resolve a handle's file path (kernel name) | `NtdllExtensions.cpp::file_handle_to_kernel_name` (`NtQueryObject(ObjectNameInformation)`; `GetFileType == FILE_TYPE_DISK` guard) |
| Hang watchdog around `NtQueryObject`/`GetFileType` | `NtdllExtensions.cpp::handles` — offload `std::thread` + `Sleep(200)` progress poll + `TerminateThread` |
| Duplicate a foreign process's handle | `handles` (`OpenProcess(PROCESS_DUP_HANDLE)` + `DuplicateHandle`) |
| Enumerate processes + their modules | `NtdllExtensions.cpp::processes` (`SystemProcessInformation`), `process_modules` (`EnumProcessModules`) |
| Resolve owning user of a PID | `NtdllExtensions.cpp::pid_to_user` (`OpenProcessToken`/`GetTokenInformation`/`LookupAccountSidW`) |
| Path→kernel-name convert (open with backup semantics) | `FileLocksmith.cpp` / `NtdllExtensions.cpp::path_to_kernel_name` (`CreateFileW` `FILE_FLAG_BACKUP_SEMANTICS`) |
| Core: match locked handles/modules to selected paths | `FileLocksmithLibInterop/FileLocksmith.cpp::find_processes_recursive` |
| Directory-prefix matching (avoid false positives) | `FileLocksmith.cpp` lambda `kernel_paths_contain` + `starts_with` (appends `\` boundary) |
| PID → full image path | `FileLocksmith.cpp::pid_to_full_path` (`GetModuleFileNameExW`) |
| WinRT projection to C# | `FileLocksmithLibInterop/NativeMethods.cpp` (`FindProcessesRecursive`, `PidToFullPath`, `ReadPathsFromFile`, `StartAsElevated`, `SetDebugPrivilege`, `IsProcessElevated`) |
| Read selected paths from `last-run.log` | `NativeMethods.cpp::ReadPathsFromFile` (reads UTF-16 2 bytes at a time; blank line terminates) |
| Restart elevated (write paths + `runas`) | `NativeMethods.cpp::StartAsElevated` (`std::ios::binary`, `ShellExecuteExW` verb `runas`, `--elevated`) |
| Enable SeDebugPrivilege (see system handles) | `NativeMethods.cpp::SetDebugPrivilege` (`AdjustTokenPrivileges`, `SE_DEBUG_NAME`) |
| Context-menu IPC writer (`last-run.log`) | `FileLocksmithLib/IPC.cpp::Writer` (`start`/`add_path`/`finish`; `std::ios::binary`) |
| Constants (paths, package name, UI exe) | `FileLocksmithLib/Constants.h` (`PowerToyName`, `LastRunPath`, `ContextMenuPackageName`, `FileNameUIExe`) |
| Settings (enabled / extended-menu-only) | `FileLocksmithLib/Settings.h` (`GetEnabled`, `GetShowInExtendedContextMenu`) |
| Classic (Win10) shell ext: menu + launch UI | `FileLocksmithExt/ExplorerCommand.cpp` (`QueryContextMenu`, `GetState`, `InvokeCommand`, `LaunchUI`) |
| Classic runtime registry registration | `FileLocksmithExt/RuntimeRegistration.h` (`AllFileSystemObjects`, `Drive` ContextMenuHandlers) |
| Module interface enable/disable + Win11 sparse pkg | `FileLocksmithExt/PowerToysModule.cpp::enable` (`package::IsWin11OrGreater` → `RegisterSparsePackage`) |
| Win11 MSIX context-menu handler | `FileLocksmithContextMenu/dllmain.cpp` (`IExplorerCommand`+`IObjectWithSite`, `RunNonElevatedEx`, `GetState`) |
| UI: load / watch / end processes | `FileLocksmithUI/ViewModels/MainViewModel.cs` (`LoadProcessesAsync`, `WatchProcess`, `EndTask`, `RestartElevated`) |
| UI: per-row process icon | `FileLocksmithUI/Converters/PidToIconConverter.cs` (`Icon.ExtractAssociatedIcon`) |
| UI: startup GPO gate + elevation + SeDebug | `FileLocksmithUI/FileLocksmithXAML/App.xaml.cs::OnLaunched` |
| CLI variant | `FileLocksmithCLI/CLILogic.cpp` |

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Non-ASCII / Unicode paths corrupted (esp. when restarting elevated)
- **Symptom:** paths whose UTF-16 bytes contain `0x0A` in a low byte get mangled through the
  `last-run.log` hand-off; elevated relaunch scans the wrong path.
- **Where:** `FileLocksmithLib/IPC.cpp::Writer::start` **and**
  `FileLocksmithLibInterop/NativeMethods.cpp::StartAsElevated` — both write UTF-16 to `last-run.log`.
- **Root cause:** `std::ofstream` in **default text mode** performs newline translation, corrupting
  UTF-16 bytes equal to `0x0A`.
- **Guardrail:** open **both** writers with `std::ios::binary`. Fixing only the context-menu writer
  leaves the elevated-relaunch path corrupting. Evidence:
  [#46949](https://github.com/microsoft/PowerToys/issues/46949) →
  [PR #47361](https://github.com/microsoft/PowerToys/pull/47361).

### `Writer::start()` returns S_OK on open failure / opens the file twice
- **Symptom:** IPC silently produces an empty/garbled `last-run.log`; the UI opens with no paths.
- **Where:** `IPC.cpp::Writer` — the ctor calls `start()`, and callers
  (`ExplorerCommand::InvokeCommand`, `FileLocksmithContextMenu/dllmain.cpp::Invoke`) call
  `writer.start()` **again**, double-opening; `std::ofstream` does **not** throw on open failure.
- **Root cause:** relying on `try/catch` around `std::ofstream` is inert (no exceptions by default),
  so a failed open still returned `S_OK`.
- **Guardrail:** check `m_stream.is_open()` after construction and return `E_FAIL` on failure; don't
  double-`start()`. Evidence:
  [#46948](https://github.com/microsoft/PowerToys/issues/46948),
  [#46951](https://github.com/microsoft/PowerToys/issues/46951) (dead `get_read_handle` decl),
  fixed alongside [PR #47361](https://github.com/microsoft/PowerToys/pull/47361).

### Crash `0xc000027b` on a listed process whose image no longer exists
- **Symptom:** the app fast-fails (unhandled exception) when a listed process's image file was
  deleted on disk (e.g. self-updating software that removes its old versioned folder).
- **Where:** `FileLocksmithUI/Converters/PidToIconConverter.cs` — `Icon.ExtractAssociatedIcon(y)`
  runs **per row during ListView virtualization**; an unhandled throw reaches
  `App.xaml.cs::App_UnhandledException` and fast-fails.
- **Root cause:** the image path is non-empty but the file is gone, so `ExtractAssociatedIcon` throws.
- **Guardrail:** wrap the extraction in `try/catch`, log a warning, and fall back to a placeholder
  `BitmapImage`. Because the converter re-runs during virtualization, prefer a `File.Exists`
  fast-path so the common case doesn't rely on (expensive, log-flooding) exceptions. Evidence:
  [#48693](https://github.com/microsoft/PowerToys/issues/48693) →
  [PR #48719](https://github.com/microsoft/PowerToys/pull/48719).

### Scan hangs on a specific handle
- **Symptom:** enumeration stops making progress; the UI never finishes loading.
- **Where:** `NtdllExtensions.cpp::handles` — `NtQueryObject`/`GetFileType` were reported to hang on
  some handles, with **no timeout-capable alternative API**.
- **Root cause:** a single bad handle blocks the whole enumeration on the calling thread.
- **Guardrail:** keep the existing design — run the per-handle loop on a detached `std::thread`, poll
  progress every `Sleep(200)`, and `TerminateThread` + resume (`i++`) when it stalls. Do **not**
  "simplify" this into a straight-line loop; the watchdog is load-bearing. (Enumeration itself already
  runs off the UI thread via `MainViewModel.FindProcesses` → `Task.Run`.)

### Context-menu entry missing, duplicated, or absent after install
- **Symptom:** "Unlock with File Locksmith" is missing, appears twice, or never shows after install.
- **Where:** Win11 sparse MSIX path `PowerToysModule.cpp::enable`
  (`package::IsWin11OrGreater` → `RegisterSparsePackage(FileLocksmithContextMenuPackage.msix)`)
  handled by `FileLocksmithContextMenu/dllmain.cpp`; Win10 classic path
  `RuntimeRegistration.h` (registry `ContextMenuHandlers`) handled by `ExplorerCommand.cpp`.
- **Root cause:** two independent registration mechanisms gated on OS version + settings; a mismatch
  (both registered → **double entry**; neither → **missing**) breaks the menu. Both handlers also
  gate on `GetEnabled()` and `GetShowInExtendedContextMenu()` — the classic one returns `E_FAIL`
  unless `CMF_EXTENDEDVERBS` when extended-only; the MSIX one sets `ECS_HIDDEN`.
- **Guardrail:** keep Win11=sparse-package and Win10=classic mutually exclusive; keep the
  enabled/extended-only checks identical in both handlers. Evidence:
  [#48951](https://github.com/microsoft/PowerToys/issues/48951),
  [#45863](https://github.com/microsoft/PowerToys/issues/45863),
  [#44394](https://github.com/microsoft/PowerToys/issues/44394) (double entry),
  [#44374](https://github.com/microsoft/PowerToys/issues/44374).

### "Launch UI" failure is swallowed
- **Symptom:** context-menu click appears to do nothing; no UI, no error.
- **Where:** `ExplorerCommand.cpp::LaunchUI` calls `CreateProcessW` but ignores its return value and
  still returns `S_OK`, then writes paths to a UI that never started.
- **Root cause:** unchecked `CreateProcessW` result.
- **Guardrail:** check the return and surface failure. Evidence:
  [#46950](https://github.com/microsoft/PowerToys/issues/46950).

### Selected path matches an unintended sibling directory
- **Symptom:** a process locking `C:\foobar` is wrongly reported for a selection of `C:\foo`.
- **Where:** `FileLocksmith.cpp::kernel_paths_contain` directory-prefix check.
- **Root cause:** a naive prefix compare treats `C:\foo` as a prefix of `C:\foobar`.
- **Guardrail:** the prefix match appends a `\` boundary before `starts_with`; preserve that boundary
  when touching path matching, and re-map the kernel name back to the user path via the substring
  offset (`dir_path + kernel_name.substr(dir_kernel_name.size())`).

## Review Rules

Enforce these when reviewing or authoring File Locksmith changes:

- **Open `last-run.log` in binary on every writer.** Both `IPC.cpp::Writer::start` and
  `NativeMethods.cpp::StartAsElevated` must use `std::ios::binary`; text mode corrupts UTF-16 paths
  (#46949 / PR #47361). See [MSVC fopen text/binary mode](https://learn.microsoft.com/cpp/c-runtime-library/text-and-binary-mode-file-i-o).
- **Check stream/handle state after opening.** `std::ofstream` does not throw on failure — verify
  `is_open()` and return `E_FAIL`; don't wrap it in a no-op `try/catch` (#46948).
- **Guard `Icon.ExtractAssociatedIcon`.** It runs per-row during virtualization; an unhandled throw
  fast-fails via `App_UnhandledException`. Keep the `try/catch` + placeholder + `File.Exists`
  fast-path (#48693 / PR #48719).
- **Never remove the handle-enumeration hang watchdog.** `NtQueryObject`/`GetFileType` can hang and
  have no timeout API; keep the offload-thread + `TerminateThread` progress loop in `handles`.
- **Keep the directory-prefix `\` boundary** in `kernel_paths_contain`; a bare prefix match reports
  false positives on sibling directories.
- **Keep Win10-classic and Win11-sparse-MSIX registration mutually exclusive** and gated identically
  on `GetEnabled()` / `GetShowInExtendedContextMenu()` in both handlers (#44394 double entry,
  #48951/#44374 missing).
- **Elevated scans require `SetDebugPrivilege`.** To see handles held by other users / system
  processes the elevated UI enables `SE_DEBUG_NAME` in `App.xaml.cs::OnLaunched`; the module is also
  GPO-gated (`GetConfiguredFileLocksmithEnabledValue`) — respect both.
- **Check `CreateProcessW`/`ShellExecuteExW` return values** in the launch/elevation paths; failures
  are currently swallowed in `LaunchUI` (#46950).
- **Use `$(RepoRoot)`, not bare relative paths, in project files**
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).

## Pitfalls

- **Never** open `last-run.log` in default text mode — UTF-16 bytes equal to `0x0A` are corrupted;
  the bug hides on ASCII-only paths and only surfaces for Unicode paths / elevated relaunch (#46949).
- **`std::ofstream` never throws on a failed open by default** — a `try/catch` around it is inert;
  `start()` returned `S_OK` on failure until an explicit `is_open()` check was added (#46948).
- **`PidToIconConverter` re-runs many times per row** during ListView virtualization; an exception
  there does not just drop an icon — it reaches the app's unhandled handler and crashes the whole app
  (`0xc000027b`, #48693).
- **The handle loop can hang on a single handle** and there is no timeout-capable replacement for
  `NtQueryObject`/`GetFileType`; the `TerminateThread` watchdog is deliberate and may leak by design
  (documented HACK). Don't "clean it up" into a plain loop.
- **Two context-menu implementations coexist:** Win11 registers a **sparse MSIX package**
  (`FileLocksmithContextMenuPackage.msix`) in `enable()`; Win10 uses **classic registry**
  registration. Changing one without the other yields missing or duplicated entries (#44394/#48951).
- **`find_processes_recursive` also scans loaded modules**, not just open file handles — a process is
  reported if it has the file as a handle **or** as a loaded module (DLL/EXE), so running executables
  and their DLLs match.
- **Directory matching normalizes to kernel names** (`\Device\HarddiskVolumeN\...`) via
  `path_to_kernel_name`, not drive letters; comparisons happen in kernel-name space, so don't compare
  Win32 paths directly.
- **`ReadPathsFromFile` reads UTF-16 two bytes at a time** and treats an empty line as end-of-input;
  it must stay byte-compatible with what the writers emit (a trailing blank line terminates).

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**; then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you on recurring
themes and measurably lowers your catch rate on the PR's actual issues. If a symptom doesn't map to
a row, reason from the source, not the map. Best for planning / triage; a targeted checklist (not a
script) for review.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a File Locksmith PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/FileLocksmith/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/FileLocksmith)
- [Public docs](https://learn.microsoft.com/windows/powertoys/file-locksmith) ·
  [NtQuerySystemInformation](https://learn.microsoft.com/windows/win32/api/winternl/nf-winternl-ntquerysysteminformation) ·
  [Sparse packages / context menus on Win11](https://learn.microsoft.com/windows/apps/desktop/modernize/context-menus)
