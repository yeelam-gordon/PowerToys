# File Locksmith — Regression Catalog & Key Decisions

Fuller list backing the SKILL.md playbooks (progressive disclosure). All entries are grounded in the
module source and the mined PR/issue history. Confirm in source before acting.

## Regressions & fixes

### R1 — `last-run.log` IPC opened in text mode corrupts Unicode paths
- **Evidence:** [#46949](https://github.com/microsoft/PowerToys/issues/46949) →
  [PR #47361](https://github.com/microsoft/PowerToys/pull/47361).
- **Root cause:** `std::ofstream` default text mode translates newlines, corrupting UTF-16 bytes
  equal to `0x0A`. Two writers touch the same file: `IPC.cpp::Writer::start` (context menu) and
  `NativeMethods.cpp::StartAsElevated` (elevated relaunch). The PR initially fixed only the first;
  review (Copilot) flagged the second, and it was fixed too.
- **Guardrail:** both writers now open with `std::ios::binary`. When adding any new writer of
  `last-run.log`, use binary mode.

### R2 — `Writer::start()` returned S_OK on failure; opened twice; dead `get_read_handle`
- **Evidence:** [#46948](https://github.com/microsoft/PowerToys/issues/46948),
  [#46951](https://github.com/microsoft/PowerToys/issues/46951).
- **Root cause:** `std::ofstream` doesn't throw on open failure, so the `try/catch` in `start()` was
  inert; the `Writer()` ctor calls `start()` and callers call it again (double-open). `IPC.h`
  declared `HANDLE get_read_handle()` with no implementation.
- **Guardrail:** `start()` now checks `m_stream.is_open()` and returns `E_FAIL`. Avoid re-calling
  `start()` after construction; remove dead declarations.

### R3 — Crash 0xc000027b from unguarded icon extraction
- **Evidence:** [#48693](https://github.com/microsoft/PowerToys/issues/48693) →
  [PR #48719](https://github.com/microsoft/PowerToys/pull/48719).
- **Root cause:** `PidToIconConverter.Convert` calls `Icon.ExtractAssociatedIcon` on a process image
  path that is non-empty but no longer exists on disk (self-updating apps delete old versioned
  folders while the process still runs). The converter runs per-row during ListView virtualization,
  so the throw reaches `App.xaml.cs::App_UnhandledException` and fast-fails the app.
- **Guardrail:** `try/catch` → log warning → placeholder `BitmapImage`. Review note (Copilot):
  add a `File.Exists` fast-path because the converter re-runs many times per row and exception-based
  control flow is expensive and log-flooding.

### R4 — Handle enumeration hangs on a single handle
- **Evidence:** in-source design in `NtdllExtensions.cpp::handles` (comment references
  the known `NtQueryObject` hang; see StackOverflow "Enumerate handles"). Related crash reports:
  [#47200](https://github.com/microsoft/PowerToys/issues/47200),
  [#46944](https://github.com/microsoft/PowerToys/issues/46944),
  [#45158](https://github.com/microsoft/PowerToys/issues/45158).
- **Root cause:** `NtQueryObject`/`GetFileType` can block indefinitely on certain handles, with no
  timeout-capable alternative API.
- **Guardrail:** the per-handle loop runs on a detached `std::thread`; the caller polls progress every
  200 ms and `TerminateThread`s + resumes (`i++`) on a stall. Documented as an intentional (unsafe,
  possibly leaking) HACK. Keep it.

### R5 — Context menu missing / duplicated / absent after install
- **Evidence:** [#48951](https://github.com/microsoft/PowerToys/issues/48951),
  [#45863](https://github.com/microsoft/PowerToys/issues/45863),
  [#44394](https://github.com/microsoft/PowerToys/issues/44394) (double entry),
  [#44374](https://github.com/microsoft/PowerToys/issues/44374).
- **Root cause:** two registration mechanisms. Win11: `PowerToysModule.cpp::enable` registers a sparse
  MSIX package (`FileLocksmithContextMenuPackage.msix`) handled by `FileLocksmithContextMenu`. Win10:
  classic registry `ContextMenuHandlers` from `RuntimeRegistration.h` handled by `ExplorerCommand`.
  Both registered → double entry; neither → missing.
- **Guardrail:** keep them mutually exclusive by OS and gate identically on `GetEnabled()` /
  `GetShowInExtendedContextMenu()`.

### R6 — `LaunchUI` swallows `CreateProcessW` failure
- **Evidence:** [#46950](https://github.com/microsoft/PowerToys/issues/46950).
- **Root cause:** `ExplorerCommand::LaunchUI` ignores the `CreateProcessW` return, returns `S_OK`, and
  proceeds to write paths to a UI that never launched.
- **Guardrail:** check the return; surface/trace failure.

## Key decisions / conventions (from history)

- **Icon sizing for MSIX context menu** must cover the standard logo set
  ([#48924](https://github.com/microsoft/PowerToys/issues/48924) →
  [PR #48925](https://github.com/microsoft/PowerToys/pull/48925)).
- **Project references use `$(RepoRoot)`** instead of bare relative paths
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).
- **Module is GPO-gated** (`getConfiguredFileLocksmithEnabledValue` /
  `GPOWrapper.GetConfiguredFileLocksmithEnabledValue`) — the UI exits early when disabled by policy.
- **Elevated visibility requires `SeDebugPrivilege`** (`NativeMethods::SetDebugPrivilege`), enabled in
  `App.xaml.cs::OnLaunched` only when the process is elevated.
- **A process matches if it holds the path as an open handle OR as a loaded module** — `find_processes_recursive`
  scans both `handles()` and each process's `modules`.

## Excluded as noise (not distilled)
- Repo-wide build/tooling PRs that only incidentally touch this module: .NET 10 upgrade (#41280),
  VS 2026 support (#44304), CppWinRT bump (#45420), WASDK 1.8.5 / TitleBar workarounds (#45532),
  CLI telemetry (#46872), PowerShell script invocation (#46729), sign fix (#44609), empty-title
  TitleBar guard (#49069 — does not touch FileLocksmith). Kept only #44639 and #48925 which have
  a concrete File Locksmith convention/asset impact.
- Environment/setup complaints not specific to module logic (e.g. ".NET Desktop Runtime required"
  #48432, "tools turned off after restart" #46165).
