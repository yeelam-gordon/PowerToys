# Hosts File Editor Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

Historical evidence for the PowerToys **Hosts File Editor**. Source anchors are under
`src/modules/Hosts/`.

> **Role split:** `SKILL.md` owns current mechanics, guardrails, and review workflow. This ledger owns
> provenance: issue/PR evidence, exact source anchors, chronology, reviewer decisions, and evidence
> caveats. Confirm current behavior in source before applying historical conclusions.

> **Corpus caveat:** issue bodies were unavailable in the source dump used for this distillation.
> Issue titles plus source establish the evidence below; they do not justify details beyond what the
> source confirms.

## Chronology and evidence ledger

| Sequence | Evidence | Decision or observed regression | Exact source anchors |
|---|---|---|---|
| 1 | [#28066](https://github.com/microsoft/PowerToys/issues/28066) | A locked large hosts file (reported with `svchost`) required a distinct “file in use” surface. | `HostsUILib/ViewModels/MainViewModel.cs::SaveAsync`; `IOException` where `(HResult & 0xFFFF) == 32` |
| 2 | [#34291](https://github.com/microsoft/PowerToys/issues/34291) | Read-only and hidden-file failures established separate handling: explicit removal of read-only and `OpenOrCreate` for hidden files. | `HostsUILib/Helpers/HostsService.cs::WriteAsync`; `RemoveReadOnlyAttribute`; VM `OverwriteHosts`; `FileMode.OpenOrCreate` |
| 3 | [#35979](https://github.com/microsoft/PowerToys/issues/35979), [#44389](https://github.com/microsoft/PowerToys/issues/44389) | Whole-file rewrite exposed parse/write asymmetry and foreign-section damage. Unowned/invalid lines must remain verbatim; toggling an owned entry may reformat it. | `HostsService.cs::ReadAsync`, `WriteAsync`; `HostsUILib/Models/Entry.cs::Parse`; `if (!e.Valid) lineBuilder.Append(e.Line)`; `HostsData.AdditionalLines`; `MainViewModel.Entry_PropertyChanged` |
| 4 | [#37666](https://github.com/microsoft/PowerToys/issues/37666) | Data-loss reports reinforced backup-before-write. Existing retention depends on the stable `_PowerToysBackup_` suffix; backup remains opt-in and once per session. | `HostsUILib/Helpers/BackupManager.cs::Create`, `Delete`, `DeleteByCount`, `DeleteByAge`; `_backupDone`; `BackupHosts`; `HostsService.WriteAsync` |
| 5 | [#39770](https://github.com/microsoft/PowerToys/issues/39770) | Non-ASCII/BOM failures established that read and write must use the same selected UTF-8 encoding. | `HostsService.cs::Encoding`; `ReadAllLinesAsync`; `StreamWriter`; `HostsUILib/Settings/HostsEncoding.cs` |
| 6 | [#40600](https://github.com/microsoft/PowerToys/issues/40600), [#44022](https://github.com/microsoft/PowerToys/issues/44022), [#44100](https://github.com/microsoft/PowerToys/issues/44100) | Repeated reports documented admin-startup/warning friction without changing the elevation requirement. | `HostsService.WriteAsync`; `Helpers/ElevationHelper.cs::IsElevated`; `HostsModuleInterface/dllmain.cpp::launch_process(bool runas)`; `SHOW_HOSTS_ADMIN_EVENT` |
| 7 | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | Build review standardized project paths on `$(RepoRoot)` rather than fragile bare relative paths. | Hosts project files touched by #44639 |
| 8 | [PR #45784](https://github.com/microsoft/PowerToys/pull/45784) | Fixed libFuzzer target class-name resolution, preserving parser fuzz coverage. | `Hosts.FuzzTests` target configuration |
| 9 | [PR #46194](https://github.com/microsoft/PowerToys/pull/46194), report [#46195](https://github.com/microsoft/PowerToys/issues/46195) | Security hardening removed registry-based default-editor lookup and fixed the launch target to Notepad. Review requested an interface seam for testability and logging of the attempted path. | `HostsUILib/Helpers/HostsService.cs::OpenHostsFile` |
| 10 | [PR #46679](https://github.com/microsoft/PowerToys/pull/46679), report [#46719](https://github.com/microsoft/PowerToys/issues/46719) | Added validation tests. Review required host-count boundary data to use `Consts.MaxHostsCount + 1`, not hardcoded `12`; local/short-name rejection remains tied to `Uri.CheckHostName`. | `HostsUILib/Helpers/ValidationHelper.cs::ValidIPv4`, `ValidIPv6`, `ValidHosts`; `Models/Entry.cs::OnAddressChanged`, `Validate`; `Hosts.Tests/ValidationHelperTest.cs`; `Consts.MaxHostsCount` |
| 11 | [PR #46729](https://github.com/microsoft/PowerToys/pull/46729) | Build review favored reliable quoted PowerShell invocation and rejected blanket warning suppression. | Build/script files touched by #46729 |
| 12 | [PR #47144](https://github.com/microsoft/PowerToys/pull/47144) | Aligned the module's default to disabled while retaining GPO gating. | `HostsModuleInterface/dllmain.cpp::is_enabled_by_default`; `gpo_policy_enabled_configuration` |
| 13 | [PR #49069](https://github.com/microsoft/PowerToys/pull/49069) | Added a non-empty title fallback after an empty resource result could fault the WinUI TitleBar during deferred layout. | `Hosts/HostsXAML/MainWindow.xaml.cs` constructor |

## Source-backed behavioral anchors

These details disambiguate the evidence; their operating instructions remain in `SKILL.md`.

| Area | Source-backed fact | Exact source anchors |
|---|---|---|
| Rewrite scope | Every save regenerates the file from parsed `Entry` objects plus `AdditionalLines`; most entry property changes trigger save, excluding Ping/Pinging/Duplicate. | `HostsService.WriteAsync`; `MainViewModel.Entry_PropertyChanged` |
| Formatting ownership | Invalid entries use their original `Line`; owned disabled entries gain `# `, optional alignment changes leading spaces, columns are padded, comments are appended, and trailing whitespace is trimmed. | `HostsService.WriteAsync`; `Entry.Line`; `Entry.Valid`; `NoLeadingSpaces` |
| Host splitting | Reads clone entries in chunks when the host list exceeds nine; the UI exposes that split. | `HostsService.ReadAsync`; `Consts.MaxHostsCount`; `ShowSplittedEntriesTooltip` |
| Template exclusion | The Windows sample lines are deliberately rejected as entries. | `Entry.Validate`; `102.54.94.97 rhino.acme.com`; `38.25.63.10 x.acme.com` |
| Duplicate semantics | Same type plus a shared host is duplicate; same type/address across multiple sub-limit entries is duplicate. Loopback addresses are excluded unless `LoopbackDuplicates` is enabled. Work runs on a queue thread and returns through `DispatcherQueue`. | `HostsUILib/Helpers/DuplicateService.cs::SetDuplicate`; `_loopbackAddresses`; `IUserSettings.LoopbackDuplicates` |
| Backup identity | `hosts_PowerToysBackup_<yyyyMMddHHmmss>` files under `BackupPath` are discovered by `*_PowerToysBackup_*`. | `BackupManager.Create`; `DeleteByCount`; `DeleteByAge` |

## Reviewer decision ledger

- **Default-editor hardening:** the accepted security boundary is a fixed Notepad launch, not a
  registry association. The follow-up review request was to hide process launch behind an interface
  and log the attempted path ([#46194](https://github.com/microsoft/PowerToys/pull/46194)).
- **Boundary tests follow constants:** validation tests construct over-limit data from
  `Consts.MaxHostsCount + 1`; a literal `12` was rejected because it would drift if the product
  constant changed ([#46679](https://github.com/microsoft/PowerToys/pull/46679)).
- **Build hygiene:** use `$(RepoRoot)`, quote script path arguments, and fix warnings rather than
  suppressing them wholesale ([#44639](https://github.com/microsoft/PowerToys/pull/44639),
  [#46729](https://github.com/microsoft/PowerToys/pull/46729)).
- **Parser robustness:** fuzz-target repair in
  [#45784](https://github.com/microsoft/PowerToys/pull/45784) is evidence that arbitrary input is an
  intentional test surface, not incidental coverage.

## Open caveats and excluded evidence

- `Uri.CheckHostName(host) == Unknown` still rejects some local/short names
  ([#46719](https://github.com/microsoft/PowerToys/issues/46719)); this ledger records no accepted
  replacement classifier.
- Admin-startup reports #40600/#44022/#44100 establish recurring UX friction, but not a decision to
  relax elevation-gated writes.
- High CPU [#42135](https://github.com/microsoft/PowerToys/issues/42135) and outdated Notepad
  [#33240](https://github.com/microsoft/PowerToys/issues/33240) were not distilled into a durable
  source-backed decision beyond the fixed-Notepad security choice.
- Keyword matches excluded as unrelated/noisy: Ctrl+C/ZoomIt [#49204](https://github.com/microsoft/PowerToys/issues/49204),
  Mouse Without Borders [#45640](https://github.com/microsoft/PowerToys/issues/45640), Settings scroll
  [#41157](https://github.com/microsoft/PowerToys/issues/41157), PowerToys Run PATH
  [#44072](https://github.com/microsoft/PowerToys/issues/44072), theme/caption rendering
  [#46199](https://github.com/microsoft/PowerToys/issues/46199) and
  [#43283](https://github.com/microsoft/PowerToys/issues/43283), animation
  [#44403](https://github.com/microsoft/PowerToys/issues/44403), and typo-only
  [PR #47539](https://github.com/microsoft/PowerToys/pull/47539).
