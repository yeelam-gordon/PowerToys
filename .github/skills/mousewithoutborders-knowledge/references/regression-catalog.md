# Mouse Without Borders — Regression Catalog & Constants

Progressive-disclosure detail behind SKILL.md. All grounded in `src/modules/MouseWithoutBorders/`
source and the PR/issue history in this module's raw data.

## Crypto / wire constants (`App/Core/Encryption.cs`)

| Constant | Value | Meaning |
|---|---|---|
| `SaltSize` | 16 | Random per-connection PBKDF2 salt, sent cleartext |
| `SymAlBlockSize` | 16 | AES block; also the per-connection IV length |
| `KeyDerivationIterations` | 50000 | PBKDF2 iteration count (SHA512) |
| `DerivedKeyLength` | 32 | AES-256 key length (bytes) |
| Cleartext header | `SaltSize + SymAlBlockSize` = 32 bytes | salt ‖ IV, exchanged before cipher text |
| AES config | KeySize 256, BlockSize 128, `CipherMode.CBC`, `PaddingMode.Zeros` | `InitEncryption` |
| Key derivation | `Rfc2898DeriveBytes.Pbkdf2(MyKey, salt, 50000, SHA512, 32)` | `GenLegalKey` |
| Packet auth | `MagicNumber` = `Get24BitHash(MyKey)` (SHA512 iterated 50000×) | routing/auth token, **not** encryption |
| Key rules | `CreateRandomKey` = 16 chars from curated charset; `IsKeyValid` requires ≥16 chars | shared secret in settings.json |

**Handshake order (per connection):** salt/IV header (`ExchangeEncryptionHeader`) → random first
block (`Common.SendOrReceiveARandomDataBlockPerInitialIV`) → framed packets with `MagicNumber`
(`SocketStuff.TcpSendData` / `ProcessReceivedDataEx`). Any change to this order/layout is a breaking
wire change (no version negotiation).

## Machine pool constants (`App/Core/MachineStuff.cs`)

| Constant | Value |
|---|---|
| `MAX_MACHINE` | 4 |
| `MAX_SOCKET` | `MAX_MACHINE * 2` = 8 |

Pool enforcement lives in `App/Class/MachinePool.cs` (`list.Count >= 4` throws
`ArgumentException` naming `MAX_MACHINE`). UI: `App/Control/MachineMatrix.*`, `App/Form/frmMatrix.*`.

## Regression / issue index (this module's history)

### Security & wire format
- **Per-connection random salt + IV** — [PR #48742](https://github.com/microsoft/PowerToys/pull/48742),
  references **MSRC 118042** in `EncryptionTests.cs`. Replaced a single fixed `InitialIV`/`GenLegalIV`
  constant; removed the derived-key cache; changed the on-the-wire format (all machines must update).
- **PBKDF2 constant clarity** — [PR #41280](https://github.com/microsoft/PowerToys/pull/41280) review:
  migrated `Rfc2898DeriveBytes` constructor → static `Pbkdf2`; reviewer asked for named constants for
  iteration count (50000) and key length (32) — now `KeyDerivationIterations` / `DerivedKeyLength`.

### Machine pool / connectivity (open issues — hypotheses, confirm in source)
- Adding a PC drops the others — [#48825](https://github.com/microsoft/PowerToys/issues/48825).
- Non-ASCII / Japanese device name can't connect — [#47673](https://github.com/microsoft/PowerToys/issues/47673).
- Various "won't connect" reports — [#48551](https://github.com/microsoft/PowerToys/issues/48551),
  [#48208](https://github.com/microsoft/PowerToys/issues/48208), [#48136](https://github.com/microsoft/PowerToys/issues/48136).

### Input injection / keyboard sync
- Modifier stuck when toggling "All PC mode" — [#49149](https://github.com/microsoft/PowerToys/issues/49149).
- Keyboard sync bug — [#48450](https://github.com/microsoft/PowerToys/issues/48450);
  keyboard dead on remote — [#49282](https://github.com/microsoft/PowerToys/issues/49282).
- AltGr not working — [#48704](https://github.com/microsoft/PowerToys/issues/48704).
- Easy Mouse fails when Windows Terminal / elevated app focused —
  [#47633](https://github.com/microsoft/PowerToys/issues/47633) (closed),
  [#47561](https://github.com/microsoft/PowerToys/issues/47561).

### Clipboard / file transfer
- Excel cells become pictures — [#49176](https://github.com/microsoft/PowerToys/issues/49176).
- Rich text mangled — [#47828](https://github.com/microsoft/PowerToys/issues/47828).
- Share-clipboard crashes network stack with a specific NIC — [#47782](https://github.com/microsoft/PowerToys/issues/47782).

### Service / elevation / settings
- Can't run as administrator / can't close admin mode — [#48137](https://github.com/microsoft/PowerToys/issues/48137),
  [#48047](https://github.com/microsoft/PowerToys/issues/48047).
- "Disable C-A-D" toggles secpol policy — [#47746](https://github.com/microsoft/PowerToys/issues/47746).
- Closing the PowerToys window kills MWB — [#48787](https://github.com/microsoft/PowerToys/issues/48787) (closed).
- settings.json IOException / read by another program — [#47039](https://github.com/microsoft/PowerToys/issues/47039),
  [#48708](https://github.com/microsoft/PowerToys/issues/48708).

## Refactor / build history (context, mostly low-signal)
- Incremental `Common` god-class split into partial classes — [PR #44283](https://github.com/microsoft/PowerToys/pull/44283)
  (part 7 of 7), [PR #44553](https://github.com/microsoft/PowerToys/pull/44553). MWB was ported
  like-for-like from the standalone app; cleanup must stay behaviour-preserving. A `Logger.GetStackTrace`
  test proved brittle across test hosts — avoid serializing environment-dependent data in tests.
- `$(RepoRoot)` project-path migration — [PR #44639](https://github.com/microsoft/PowerToys/pull/44639)
  (use `$(RepoRoot)`, not `..\..\`; avoid mixing `$(SolutionDir)`/`$(ProjectDir)$(RepoRoot)`).
- Remove unused dependencies / shrink installer — [PR #47233](https://github.com/microsoft/PowerToys/pull/47233)
  (trimmed MWB `.csproj` package references; installer-side changes were not MWB-specific).
- Windows.ImplementationLibrary vcxproj vs packages.config drift — [PR #49050](https://github.com/microsoft/PowerToys/pull/49050).
- Dependency bumps: WIL [#43503](https://github.com/microsoft/PowerToys/pull/43503),
  CppWinRT [#45420](https://github.com/microsoft/PowerToys/pull/45420) (reviewer asked for x64+ARM64
  Debug+Release build evidence on solution-wide C++/WinRT bumps), .NET 10 [#41280](https://github.com/microsoft/PowerToys/pull/41280),
  MTP migration [#37651](https://github.com/microsoft/PowerToys/pull/37651).

## Excluded as noise (not distilled)
Pure formatting/blank-line nits, "fixed in latest commit", `/azp run` CI chatter, LGTM, and
installer/WinUI3Apps hardlink review threads in [#47233](https://github.com/microsoft/PowerToys/pull/47233)
that are not specific to the MouseWithoutBorders module.
