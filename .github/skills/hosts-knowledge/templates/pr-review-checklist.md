# Hosts File Editor — PR Review Checklist

Apply to any PR touching `src/modules/Hosts/`. **Read the diff cold first**, then use this list only
for the code paths the diff actually touches (see anti-anchoring note in SKILL.md).

## Read / parse path (`HostsService.ReadAsync`, `Entry.Parse`)
- [ ] Round-trip is byte-stable for entries the parser doesn't change (invalid/unparsed lines written verbatim).
- [ ] Comments (including non-ASCII) and the disabled `#` prefix are preserved.
- [ ] Lines with > `Consts.MaxHostsCount` (9) hosts still split correctly; the constant is not duplicated as a literal.
- [ ] Windows sample lines (`rhino.acme.com`, `x.acme.com`) remain ignored by `Entry.Validate`.

## Write / format path (`HostsService.WriteAsync`)
- [ ] Elevation gate (`NotRunningElevatedException`) preserved.
- [ ] Read-only detection (`ReadOnlyHostsException`) preserved; overwrite only via explicit path.
- [ ] `FileMode.OpenOrCreate` retained (hidden-file support).
- [ ] `_backupManager.Create(HostsFilePath)` runs before the write stream.
- [ ] `FileSystemWatcher.EnableRaisingEvents` toggled off during the write and restored in `finally`.
- [ ] Additional/unparsed lines re-emitted at the configured top/bottom position.

## Encoding (`HostsService.Encoding`, `HostsEncoding`)
- [ ] Read and write use the same `Encoding` value.
- [ ] UTF-8 vs UTF-8-BOM choice honored; no hardcoded default encoding.

## Backups (`BackupManager`)
- [ ] `BackupHosts` gate + once-per-session `_backupDone` intact.
- [ ] `_PowerToysBackup_` + timestamp naming unchanged (retention globs depend on it).
- [ ] Retention (`DeleteByCount`/`DeleteByAge`) still matches the naming.

## Validation & duplicates (`ValidationHelper`, `DuplicateService`, `Entry`)
- [ ] IPv4/IPv6/hostname paths kept in sync; new acceptance covered by `Hosts.Tests/ValidationHelperTest.cs`.
- [ ] Tests build host lists from `Consts.MaxHostsCount + 1`, not a literal.
- [ ] Loopback exclusion (`LoopbackDuplicates`) behavior unchanged unless intended.

## Security / launch / lifecycle
- [ ] "Open hosts file" still hardcodes Notepad — no registry default-editor lookup.
- [ ] Window `Title` never empty (WinUI TitleBar fault guard).
- [ ] `is_enabled_by_default()` stays `false`; GPO gate intact.
- [ ] Project files use `$(RepoRoot)`, not bare relative paths.

## Save error handling (`MainViewModel.SaveAsync`)
- [ ] Distinct messages for not-elevated / read-only / file-in-use (IOException HResult 32) preserved.
