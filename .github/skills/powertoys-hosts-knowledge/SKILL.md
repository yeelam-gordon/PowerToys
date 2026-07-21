---
name: powertoys-hosts-knowledge
description: 'PowerToys Hosts File Editor module knowledge: feature->file/function map, recurring regression playbooks (hosts-file parse/write round-trip, UTF-8 vs UTF-8-BOM encoding & non-ASCII comments, elevation-gated writes, read-only/hidden hosts file, backup creation & retention, entry validation IPv4/IPv6/hostname, duplicate detection, 9-host line splitting, default-editor security hardening), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/Hosts — reading/writing C:\Windows\System32\drivers\etc\hosts, elevation, encoding, backups, duplicates, WinUI editor. Keywords: hosts file, Hosts File Editor, hosts, etc/hosts, elevation, admin, runas, UTF-8 BOM, encoding, backup, duplicate, IPv4, IPv6, hostname validation, read-only, WinUI3, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Hosts File Editor Knowledge

Grounded engineering knowledge for the PowerToys **Hosts File Editor** module — a WinUI 3 desktop
app that reads, edits, and writes the Windows hosts file
(`%WINDIR%\System32\drivers\etc\hosts`) with entry validation, duplicate detection, automatic
backups, and selectable encoding. Use it to localize code fast, avoid known regression traps
(parse/write round-trip, encoding/BOM, elevation, read-only/hidden files, backups, duplicates),
and enforce conventions maintainers established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/Hosts/` and needing prior art.
- Fixing/triaging a Hosts bug: non-ASCII (e.g. Japanese) comments corrupted, entries reverting on
  enable/disable toggle, sections from other apps (Docker/Tailscale) broken into duplicates, backups
  not created / file lost, "can't save" when non-elevated / read-only / hidden hosts file, local
  addresses failing validation, high CPU, admin-warning behavior.
- Reviewing a Hosts PR against maintainer conventions and regression traps.
- Touching the read/parse path, the write/format path, the encoding selector, the backup engine,
  elevation gating, duplicate detection, or the default-editor launch.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring
below). Root: `src/modules/Hosts/`. Shared UI/logic lives in `HostsUILib/`; the app host in `Hosts/`;
the C++ launcher in `HostsModuleInterface/`.

| Sub-feature | Implementation (file · function) |
|---|---|
| Hosts file path resolution | `HostsUILib/Helpers/HostsService.cs` ctor (`...\System32\drivers\etc\hosts`) |
| Read + parse whole file | `HostsService.cs::ReadAsync` (reads with `Encoding`, builds `Entry` list + unparsed lines) |
| Parse a single line (address/hosts/comment, active) | `HostsUILib/Models/Entry.cs::Parse` (leading `#` = disabled; split on `#`) |
| Split lines with > 9 hosts | `ReadAsync` (chunks `Entry.SplittedHosts` by `Consts.MaxHostsCount`) |
| Max hosts per line constant (= 9) | `HostsUILib/Consts.cs::MaxHostsCount` |
| Ignore the Windows sample lines | `Entry.cs::Validate` (rejects `102.54.94.97 rhino.acme.com`, `38.25.63.10 x.acme.com`) |
| Write + format/align + additional lines | `HostsService.cs::WriteAsync` (address/hosts padding; disabled `# `; leading spaces; top/bottom extra lines) |
| Encoding selection (UTF-8 vs UTF-8-BOM) | `HostsService.cs::Encoding` prop → `HostsUILib/Settings/HostsEncoding.cs` |
| Elevation gate for writes | `WriteAsync` throws `NotRunningElevatedException`; `HostsUILib/Helpers/ElevationHelper.cs::IsElevated` |
| Read-only hosts file handling | `WriteAsync` throws `ReadOnlyHostsException`; `HostsService.cs::RemoveReadOnlyAttribute`; VM `OverwriteHosts` |
| Hidden hosts file save | `WriteAsync` uses `FileMode.OpenOrCreate` (comment: prevents `UnauthorizedAccessException` when hidden) |
| Backup create / retention | `HostsUILib/Helpers/BackupManager.cs::Create` / `Delete` (suffix `_PowerToysBackup_`, gated on `BackupHosts`) |
| External-change watch (self-write ignore) | `HostsService.cs` `FileSystemWatcher` + `FileChanged`; disabled around each write |
| Save error surfaces (elevation/read-only/in-use) | `HostsUILib/ViewModels/MainViewModel.cs::SaveAsync` (incl. `IOException` HResult 32 for locked big files) |
| Address validation (IPv4 / IPv6) | `HostsUILib/Helpers/ValidationHelper.cs::ValidIPv4` / `ValidIPv6`; `Entry.OnAddressChanged` → `AddressType` |
| Hostname validation | `ValidationHelper.cs::ValidHosts` (`Uri.CheckHostName`; length capped by `MaxHostsCount`) |
| Duplicate detection engine | `HostsUILib/Helpers/DuplicateService.cs` (background queue; loopback list; `MaxHostsCount` rule) |
| Loopback-duplicate opt-out | `DuplicateService.cs` `_loopbackAddresses` + `IUserSettings.LoopbackDuplicates` |
| Open hosts in editor (hardcoded notepad) | `HostsService.cs::OpenHostsFile` (security: always Notepad, no registry default-editor lookup) |
| Ping an entry's address | `HostsService.cs::PingAsync` |
| Launch app (normal + elevated) | `HostsModuleInterface/dllmain.cpp::launch_process` (`ShellExecuteExW`, `runas` verb; `SHOW_HOSTS_ADMIN_EVENT`) |
| Enabled-by-default = false; GPO gate | `dllmain.cpp::is_enabled_by_default` (returns `false`), `gpo_policy_enabled_configuration` |
| Empty-title TitleBar guard | `Hosts/HostsXAML/MainWindow.xaml.cs` ctor (fallback to "Hosts File Editor") |
| Settings load (defaults, retry, watcher) | `Hosts/Settings/UserSettings.cs` |

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Non-ASCII (e.g. Japanese) comments corrupt entries / entries stop working
- **Symptom:** entries with non-ASCII comment text fail or the file is mangled after an edit.
- **Where:** `HostsService.cs::Encoding` (returns `new UTF8Encoding(false)` for `Utf8` vs
  `new UTF8Encoding(true)` for `Utf8Bom`); read (`ReadAllLinesAsync`) and write (`StreamWriter`) both
  use this single property.
- **Root cause:** if the on-disk encoding doesn't match the configured `HostsEncoding`, multi-byte
  characters round-trip incorrectly (BOM added/stripped, bytes reinterpreted).
- **Guardrail:** read and write MUST use the **same** `Encoding` value; expose the BOM choice via the
  `HostsEncoding` setting and never hardcode ASCII/default. Evidence:
  [#39770](https://github.com/microsoft/PowerToys/issues/39770).

### Entries revert when toggled on/off; sections from other apps break into duplicates
- **Symptom:** toggling Active reverts the entry; blocks written by Docker/Tailscale get reformatted
  and duplicated.
- **Where:** round-trip between `Entry.cs::Parse` (read) and `HostsService.cs::WriteAsync` (write);
  unparsed content preserved via `HostsData.AdditionalLines` and re-emitted top/bottom.
- **Root cause:** the editor rewrites the whole file on every change; any parse/format asymmetry (lost
  comments, reordered/duplicated hosts, structured blocks it doesn't understand) is written back.
- **Guardrail:** preserve exact content for lines the parser doesn't own — invalid/unparsed lines are
  written verbatim (`if (!e.Valid) lineBuilder.Append(e.Line)`); don't "normalize" foreign sections.
  Any parser change must round-trip byte-stable for untouched entries. Evidence:
  [#44389](https://github.com/microsoft/PowerToys/issues/44389),
  [#35979](https://github.com/microsoft/PowerToys/issues/35979).

### Backup not created / user loses hosts file
- **Symptom:** no backup exists after edits; original content lost.
- **Where:** `BackupManager.cs::Create` — returns early if `_backupDone || !_userSettings.BackupHosts
  || !File.Exists`; `WriteAsync` calls `Create` before writing.
- **Root cause:** backups are **opt-in** (`BackupHosts`) and created **at most once per session**
  (`_backupDone`); if disabled, or if the write path changes and skips `Create`, the pre-edit file is
  unrecoverable.
- **Guardrail:** keep `_backupManager.Create(HostsFilePath)` before the write stream; keep the
  `_PowerToysBackup_` suffix + timestamp naming stable (retention in `Delete`/`DeleteByCount`/
  `DeleteByAge` globs `*_PowerToysBackup_*`). Evidence:
  [#37666](https://github.com/microsoft/PowerToys/issues/37666).

### "Can't save" — non-elevated, read-only, or hidden hosts file
- **Symptom:** save fails; hosts file is read-only or hidden, or PowerToys is not elevated.
- **Where:** `WriteAsync` (elevation + read-only guards, `FileMode.OpenOrCreate`),
  `MainViewModel.cs::SaveAsync` (maps exceptions to localized messages), `RemoveReadOnlyAttribute` /
  VM `OverwriteHosts`.
- **Root cause:** writing `%WINDIR%\...\etc\hosts` requires Administrator; the file is often read-only
  or hidden; a hidden file breaks a plain create with `UnauthorizedAccessException`.
- **Guardrail:** keep the elevation gate (`NotRunningElevatedException`), the read-only detection +
  explicit overwrite path (`ReadOnlyHostsException`), and `FileMode.OpenOrCreate` for hidden files;
  surface distinct messages, don't silently swallow. Evidence:
  [#34291](https://github.com/microsoft/PowerToys/issues/34291),
  [#44022](https://github.com/microsoft/PowerToys/issues/44022),
  [#40600](https://github.com/microsoft/PowerToys/issues/40600). Locked big-file case (svchost):
  `SaveAsync` `IOException` HResult 32 ([#28066](https://github.com/microsoft/PowerToys/issues/28066)).

### Default-editor hijack when opening the hosts file (security)
- **Symptom:** "Open hosts file" launched whatever the registry had registered for the file type.
- **Where:** `HostsService.cs::OpenHostsFile`.
- **Root cause:** resolving the default editor via a registry lookup lets a malicious/misconfigured
  association run an arbitrary program elevated.
- **Guardrail:** **hardcode** the launch to `System32\notepad.exe`; do not reintroduce a default-editor
  lookup. Evidence: [PR #46194](https://github.com/microsoft/PowerToys/pull/46194),
  report [#46195](https://github.com/microsoft/PowerToys/issues/46195).

### Local / non-standard addresses fail validation
- **Symptom:** valid local hostnames or addresses are rejected in the editor.
- **Where:** `ValidationHelper.cs::ValidHosts` (`Uri.CheckHostName` per host) and
  `ValidIPv4`/`ValidIPv6`; `Entry.cs::Validate`.
- **Root cause:** hostname acceptance is delegated to `Uri.CheckHostName`, which classifies some
  local/short names as `Unknown`.
- **Guardrail:** when changing acceptance, add unit tests in `Hosts.Tests/ValidationHelperTest.cs`
  (build host lists from `Consts.MaxHostsCount + 1`, not a hardcoded number) and keep IPv4/IPv6/hostname
  paths in sync. Evidence: [#46719](https://github.com/microsoft/PowerToys/issues/46719),
  [PR #46679](https://github.com/microsoft/PowerToys/pull/46679).

### Startup crash / empty admin window title
- **Symptom:** editor faults on launch, notably when elevated.
- **Where:** `MainWindow.xaml.cs` ctor — elevated path uses the `WindowAdminTitle` resource; the WinUI
  TitleBar reads `AppWindow.Title` during a deferred layout pass.
- **Root cause:** `ResourceLoader.GetString` can return `""` when the resource map fails to resolve; an
  empty native window title faults the windowing layer.
- **Guardrail:** never leave `Title` empty — keep the non-empty fallback ("Hosts File Editor").
  Evidence: [PR #49069](https://github.com/microsoft/PowerToys/pull/49069).

## Review Rules

Enforce these when reviewing or authoring Hosts changes:

- **Never write the hosts file without elevation or when read-only.** Preserve the `IsElevated` gate
  (`NotRunningElevatedException`) and the read-only guard (`ReadOnlyHostsException`) in `WriteAsync`;
  read-only is cleared only via the explicit `OverwriteHosts` / `RemoveReadOnlyAttribute` path.
- **Read and write with the same configured `Encoding`.** UTF-8 vs UTF-8-BOM must round-trip; keep the
  `HostsEncoding`-driven `Encoding` property and don't hardcode a default encoding (#39770).
- **Keep the whole-file rewrite round-trip stable.** Unparsed/invalid lines are written verbatim; extra
  content goes back via `AdditionalLines` at the configured top/bottom position. Don't reformat entries
  the parser doesn't own (#35979, #44389).
- **Always back up before the first write of a session.** Keep `_backupManager.Create` ahead of the
  write stream and the `_PowerToysBackup_`+timestamp naming stable (retention globs on it) (#37666).
- **Disable the `FileSystemWatcher` around writes.** `WriteAsync` sets `EnableRaisingEvents = false`
  before writing and restores it after, so the module doesn't treat its own write as an external change
  and reload/loop.
- **Launch the hosts editor with a hardcoded Notepad path.** No registry default-editor lookup — it is a
  security hole ([PR #46194](https://github.com/microsoft/PowerToys/pull/46194), #46195).
- **Keep `Consts.MaxHostsCount` (9) as the single source of truth** for line splitting, hostname-count
  validation, and duplicate logic; tests build host lists from `MaxHostsCount + 1`, never a literal
  ([PR #46679](https://github.com/microsoft/PowerToys/pull/46679)).
- **Keep the Windows sample lines ignored** in `Entry.Validate` so the template `rhino.acme.com` /
  `x.acme.com` lines are never treated as real entries.
- **Never leave the native window title empty** — WinUI TitleBar faults on an empty `AppWindow.Title`
  ([PR #49069](https://github.com/microsoft/PowerToys/pull/49069)).
- **This module is disabled by default** (`is_enabled_by_default() == false`) and GPO-gated; don't
  assume it's active (#47144). Use `$(RepoRoot)`, not bare relative paths, in project files
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).

## Pitfalls

- **Never** call `Environment...`-style default-editor resolution for "open hosts file" — it must be
  hardcoded Notepad; a registry association can run arbitrary code elevated (#46194/#46195).
- **Never** change `Encoding` on only the read or only the write side — a mismatch corrupts non-ASCII
  comments and adds/strips a BOM (#39770).
- **Backups are opt-in and once-per-session** (`BackupHosts` + `_backupDone`). Don't assume a backup
  exists; disabling the setting means an edit is unrecoverable (#37666).
- **The whole file is rewritten on every entry change** (`WriteAsync`), and a save is triggered on most
  `Entry` property changes (`MainViewModel.Entry_PropertyChanged`) — Ping/Pinging/Duplicate are
  explicitly excluded so they don't cause writes. Keep that exclusion.
- **Writes require elevation**; the file is often **read-only** and sometimes **hidden**
  (`FileMode.OpenOrCreate` is deliberate for hidden files). Don't "simplify" the file-open flags (#34291).
- **Lines with more than 9 hosts are split** on read (`Consts.MaxHostsCount`) and this same constant
  drives duplicate detection — changing it shifts multiple behaviors.
- **Loopback addresses are excluded from duplicate flagging by default** (`_loopbackAddresses` +
  `LoopbackDuplicates`), because many valid entries legitimately point at `127.0.0.1`/`::1`.
- **The `FileSystemWatcher` watches the real hosts file**; the module suppresses its own writes by
  toggling `EnableRaisingEvents`. External edits set `FileChanged` and prompt a reload.
- **Module is off by default and GPO-gated** — integration flows must enable it first (#47144).

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim this file's playbooks and then hunt the diff for those
themes — that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

When localizing a bug, if the symptom doesn't map cleanly to a row above, reason from the symptom and
verify in source — a thin/absent map entry can anchor you onto a confident, wrong file.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Hosts PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/Hosts/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/Hosts)
- [Public docs](https://learn.microsoft.com/en-us/windows/powertoys/hosts-file-editor) ·
  [Uri.CheckHostName](https://learn.microsoft.com/en-us/dotnet/api/system.uri.checkhostname) ·
  [UTF-8 BOM background (Unicode FAQ)](https://www.unicode.org/faq/utf_bom.html#bom5)
