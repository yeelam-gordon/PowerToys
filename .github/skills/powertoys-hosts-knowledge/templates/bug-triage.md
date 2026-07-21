# Hosts File Editor — Bug Triage (symptom → likely file/function)

Use the Module Map as **hypotheses to confirm in source**. If the symptom doesn't map cleanly, reason
from the symptom and verify — don't force-fit.

| Symptom | Start here | Notes / evidence |
|---|---|---|
| Non-ASCII (Japanese/etc.) comments corrupted; file mangled | `HostsService.Encoding`, `ReadAsync`/`WriteAsync`, `HostsEncoding` | Read/write encoding mismatch; UTF-8 vs BOM (#39770) |
| Entry reverts when toggled on/off | `Entry.Parse` ↔ `HostsService.WriteAsync` | Parse/format round-trip asymmetry (#44389) |
| Sections from Docker/Tailscale broken → duplicates | `WriteAsync` (verbatim invalid lines), `HostsData.AdditionalLines` | Whole-file rewrite reformatting foreign blocks (#35979) |
| No backup / lost hosts file | `BackupManager.Create` (`BackupHosts`, `_backupDone`) | Opt-in + once-per-session (#37666) |
| "Can't save" without elevation | `WriteAsync` → `NotRunningElevatedException`; `ElevationHelper.IsElevated` | Needs admin (#40600, #44022) |
| "Can't save" hidden file | `WriteAsync` `FileMode.OpenOrCreate` | Hidden-file create guard (#34291) |
| "Can't save" read-only file | `WriteAsync` → `ReadOnlyHostsException`; `RemoveReadOnlyAttribute`; VM `OverwriteHosts` | Explicit overwrite path |
| Save fails on large file (locked) | `MainViewModel.SaveAsync` `IOException` HResult 32 | svchost lock (#28066) |
| Local/short hostname rejected | `ValidationHelper.ValidHosts` (`Uri.CheckHostName`) | Classifier returns `Unknown` (#46719) |
| IPv4/IPv6 wrongly (in)valid | `ValidationHelper.ValidIPv4`/`ValidIPv6`; `Entry.OnAddressChanged` → `AddressType` | |
| Wrong/unexpected editor opens hosts file | `HostsService.OpenHostsFile` | Must be hardcoded Notepad (#46194/#46195) |
| False/missing duplicate flags | `DuplicateService.FindDuplicates`/`SetDuplicate`; `_loopbackAddresses`; `LoopbackDuplicates` | 9-host rule via `Consts.MaxHostsCount` |
| Line with many hosts split unexpectedly | `ReadAsync` chunking + `Consts.MaxHostsCount` (9) | |
| Startup crash / empty admin title | `MainWindow.xaml.cs` ctor (`WindowAdminTitle` default) | Empty title faults TitleBar (PR #49069) |
| Module not running in integration flow | `dllmain.cpp::is_enabled_by_default` (false), GPO gate | Off by default (#47144) |
| External edit not detected / self-write loop | `HostsService` `FileSystemWatcher`, `FileChanged`, `EnableRaisingEvents` toggling | |
