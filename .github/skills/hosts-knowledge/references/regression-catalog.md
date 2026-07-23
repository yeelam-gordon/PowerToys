# Hosts File Editor — Regression Catalog (fuller list)

Progressive-disclosure companion to `SKILL.md`. Every entry is grounded in source and/or a real
issue/PR. Signal filter applied: durable lessons about parse/write round-trip, encoding, elevation,
read-only/hidden files, backups, validation/duplicates, and default-editor security — not one-off
UI nits. Issue bodies were unavailable in the raw dump; titles + source are the evidence.

## Parse / write round-trip

- **Whole-file rewrite on every change.** `HostsService.WriteAsync` regenerates the entire file from
  the `Entry` list plus `AdditionalLines`; `MainViewModel.Entry_PropertyChanged` triggers a save on
  most property changes (Ping/Pinging/Duplicate excluded). Any parse/format asymmetry is persisted.
- **Invalid/unparsed lines are written verbatim** (`if (!e.Valid) lineBuilder.Append(e.Line)`), which is
  the mechanism that must preserve foreign sections (Docker/Tailscale) and unusual formatting
  (#35979). Toggling Active reformats a line, exposing round-trip bugs (#44389).
- **Formatting details:** disabled entries get a leading `# `; when any entry is disabled and
  `NoLeadingSpaces` is off, active entries get two leading spaces for alignment; address and hosts are
  padded to column width; comment appended as `# comment`; trailing whitespace trimmed.
- **9-host split:** on read, an entry whose host list exceeds `Consts.MaxHostsCount` (9) is cloned into
  multiple entries (`ReadAsync` … `Chunk(Consts.MaxHostsCount)`), and `ShowSplittedEntriesTooltip`
  informs the UI.
- **Sample lines ignored:** `Entry.Validate` explicitly returns false for the Windows template lines
  `102.54.94.97 rhino.acme.com` and `38.25.63.10 x.acme.com`.

## Encoding

- **UTF-8 vs UTF-8-BOM** is a user setting (`HostsEncoding` → `HostsService.Encoding`:
  `new UTF8Encoding(false)` vs `new UTF8Encoding(true)`). Read (`ReadAllLinesAsync`) and write
  (`StreamWriter`) share the property. A mismatch with the on-disk encoding corrupts non-ASCII comments
  and adds/strips a BOM (#39770).

## Elevation / file attributes

- **Writes require Administrator.** `WriteAsync` throws `NotRunningElevatedException` when
  `!IsElevated`. `ElevationHelper` uses `WindowsPrincipal.IsInRole(Administrator)`.
- **Read-only hosts file:** `WriteAsync` throws `ReadOnlyHostsException` when the file `IsReadOnly`;
  the user clears it via `OverwriteHosts` → `RemoveReadOnlyAttribute` (#34291 area).
- **Hidden hosts file:** `FileMode.OpenOrCreate` is deliberately used to avoid
  `UnauthorizedAccessException` when the file is hidden (#34291).
- **Locked big file (svchost):** `MainViewModel.SaveAsync` catches `IOException` with
  `(HResult & 0xFFFF) == 32` and shows a "file in use" message (#28066).
- **Admin startup warning behavior** and launching as admin were reported repeatedly
  (#40600, #44022, #44100). The C++ launcher `dllmain.cpp::launch_process(bool runas)` uses
  `ShellExecuteExW` with the `runas` verb and a dedicated `SHOW_HOSTS_ADMIN_EVENT`.

## Backups

- **Opt-in + once per session.** `BackupManager.Create` early-returns on `_backupDone ||
  !BackupHosts || !File.Exists`. Backup file: `hosts_PowerToysBackup_<yyyyMMddHHmmss>` under
  `BackupPath`.
- **Retention** via `Delete` → `DeleteByCount` / `DeleteByAge`, globbing `*_PowerToysBackup_*`. Renaming
  the suffix orphans existing backups. Data-loss reports underscore keeping this robust (#37666).

## Validation / duplicates

- **Address typing:** `Entry.OnAddressChanged` sets `AddressType` via `ValidIPv4`/`ValidIPv6` regexes.
- **Hostnames:** `ValidHosts` splits on space, rejects when count > `MaxHostsCount` (when validating
  length) and when `Uri.CheckHostName(host) == Unknown` — the classifier rejects some local/short names
  (#46719).
- **Duplicate rules** (`DuplicateService.SetDuplicate`): same `Type` + at least one shared host ⇒
  duplicate; or same `Type` + address with more than one sub-`MaxHostsCount` entry ⇒ duplicate.
  Loopback addresses (`0.0.0.0`, `127.0.0.1`, `::1`, …) are excluded unless `LoopbackDuplicates` is on.
  Runs on a background queue thread; results marshaled via the `DispatcherQueue`.

## Security / packaging / tests (key decisions)

- **Default-editor hardening:** [PR #46194](https://github.com/microsoft/PowerToys/pull/46194) removed
  the registry-based open-method lookup and hardcoded Notepad in `OpenHostsFile` (report #46195).
  Review feedback asked to make the launch testable behind an interface and to log the attempted path.
- **Future-proof tests:** [PR #46679](https://github.com/microsoft/PowerToys/pull/46679) added
  `ValidationHelper` unit tests; reviewers required host lists built from `Consts.MaxHostsCount + 1`
  rather than a hardcoded 12 so tests track the constant.
- **Fuzz tests:** [PR #45784](https://github.com/microsoft/PowerToys/pull/45784) fixed libFuzzer target
  class-name resolution in `Hosts.FuzzTests` (parsing is fuzzed — keep the parser robust to garbage).
- **Empty-title guard:** [PR #49069](https://github.com/microsoft/PowerToys/pull/49069) added the
  non-empty `Title` default in `MainWindow.xaml.cs` to stop a WinUI TitleBar startup fault.
- **Enabled-by-default off:** `dllmain.cpp::is_enabled_by_default()` returns `false`; module is GPO
  gated ([PR #47144](https://github.com/microsoft/PowerToys/pull/47144) aligned defaults).
- **Build hygiene:** use `$(RepoRoot)` not bare relative paths
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)); more reliable PowerShell script
  invocation and avoiding blanket warning suppression
  ([PR #46729](https://github.com/microsoft/PowerToys/pull/46729)).

## Excluded as noise (not distilled)

Keyword-matched issues unrelated to Hosts durable logic: Ctrl+C/ZoomIt hijack (#49204), Mouse Without
Borders connectivity (#45640), Settings scroll wheel (#41157), PowerToys Run PATH (#44072), theme/caption
button rendering (#46199, #43283), expand/collapse animation (#44403), pure typo/grammar
([PR #47539](https://github.com/microsoft/PowerToys/pull/47539)). High-CPU (#42135) and outdated-notepad
(#33240) are UX/servicing symptoms without a durable code lesson beyond the hardcoded-Notepad decision.
