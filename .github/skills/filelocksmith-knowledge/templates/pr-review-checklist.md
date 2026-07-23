# File Locksmith — PR Review Checklist

Apply **after** reading the diff cold (see anti-anchoring in SKILL.md). Only check rows whose files
the diff actually touches.

## IPC / `last-run.log` (`FileLocksmithLib/IPC.cpp`, `FileLocksmithLibInterop/NativeMethods.cpp`)
- [ ] Every writer opens the file with `std::ios::binary` (text mode corrupts UTF-16 paths — #46949).
- [ ] Stream state checked after open (`is_open()` → `E_FAIL`); no inert `try/catch` around `ofstream` (#46948).
- [ ] `Writer::start()` not called twice (ctor already calls it) — no double-open.
- [ ] `ReadPathsFromFile` stays byte-compatible with the writers (UTF-16, trailing blank line ends input).

## Handle / process enumeration (`FileLocksmithLibInterop/NtdllExtensions.cpp`, `FileLocksmith.cpp`)
- [ ] Hang watchdog preserved: offload `std::thread` + `Sleep(200)` progress poll + `TerminateThread`.
- [ ] `NtQuerySystemInformationMemoryLoop` still grows buffer on `STATUS_INFO_LENGTH_MISMATCH` (capped).
- [ ] `file_handle_to_kernel_name` keeps the `GetFileType == FILE_TYPE_DISK` guard.
- [ ] Duplicated handles and opened process handles are always `CloseHandle`d (leak check).
- [ ] Directory-prefix match keeps the trailing `\` boundary (no sibling-dir false positives).
- [ ] Matching done in kernel-name space (`path_to_kernel_name`), not raw Win32 paths.

## UI (`FileLocksmithUI/`)
- [ ] `PidToIconConverter` guards `Icon.ExtractAssociatedIcon` with `try/catch` + placeholder + `File.Exists` fast-path (#48693).
- [ ] `EndTask` / `WatchProcess` handle "process already gone" without throwing to the UI.
- [ ] Startup keeps the GPO gate and (when elevated) `SetDebugPrivilege`.

## Context menu / registration (`FileLocksmithExt/`, `FileLocksmithContextMenu/`)
- [ ] Win11 sparse-MSIX and Win10 classic registration stay mutually exclusive (no double entry — #44394).
- [ ] Both handlers gate identically on `GetEnabled()` and `GetShowInExtendedContextMenu()`.
- [ ] `CreateProcessW` / `ShellExecuteExW` / `RunNonElevatedEx` return values are checked (#46950).

## Build / packaging
- [ ] Project references use `$(RepoRoot)`, not bare relative paths (PR #44639).
- [ ] MSIX context-menu icons cover the standard logo set (#48924 / PR #48925).
