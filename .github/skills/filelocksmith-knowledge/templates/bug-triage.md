# File Locksmith — Bug Triage (symptom → likely file/function)

Use the Module Map in SKILL.md as hypotheses to **confirm in source**. If a symptom doesn't map
cleanly, reason from the symptom — don't force-fit.

| Symptom | Start here | Notes / evidence |
|---|---|---|
| Unicode/non-ASCII path scanned wrong, esp. after "Restart elevated" | `IPC.cpp::Writer::start`, `NativeMethods.cpp::StartAsElevated` | text-mode `ofstream` corrupts `0x0A` bytes — must be `std::ios::binary` (#46949 / PR #47361) |
| Context menu opens empty / no paths | `IPC.cpp::Writer` (`is_open`, double-`start`), `ReadPathsFromFile` | failed open returned `S_OK`; double-open (#46948) |
| Crash `0xc000027b` on a listed process | `PidToIconConverter.cs` → `App.xaml.cs::App_UnhandledException` | image path exists in list but file deleted; `ExtractAssociatedIcon` throws per-row during virtualization (#48693 / PR #48719) |
| Runs a few seconds then crashes / crashes scanning an executable | `PidToIconConverter.cs`, `find_processes_recursive` (module scan), `handles` | icon guard + module enumeration paths (#47200, #46944) |
| Scan hangs, never finishes | `NtdllExtensions.cpp::handles` (watchdog), `MainViewModel.FindProcesses` (`Task.Run`) | `NtQueryObject`/`GetFileType` hang; watchdog terminates & resumes |
| Context menu missing after install | `PowerToysModule.cpp::enable` (Win11 sparse pkg), `RuntimeRegistration.h` (Win10) | wrong/absent registration for the OS (#48951, #45863, #44374) |
| Duplicate context-menu entry | `PowerToysModule.cpp` + `RuntimeRegistration.h` + `FileLocksmithContextMenu/dllmain.cpp` | both classic and MSIX registered (#44394) |
| Context-menu click does nothing | `ExplorerCommand.cpp::LaunchUI` | `CreateProcessW` failure swallowed (#46950) |
| Elevated scan shows no system processes | `App.xaml.cs::OnLaunched` → `NativeMethods.SetDebugPrivilege` | `SE_DEBUG_NAME` not enabled |
| Process reported for a file it doesn't lock (sibling dir) | `FileLocksmith.cpp::kernel_paths_contain` | directory-prefix `\` boundary |
| "End task" doesn't remove the row / errors | `MainViewModel.cs::EndTask`, `WatchProcess` | process may already be gone; handle-get failure removes the row |
