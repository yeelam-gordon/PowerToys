---
name: mousewithoutborders-knowledge
description: 'PowerToys Mouse Without Borders (MWB) module knowledge — a keyboard/mouse-sharing tool that links up to four machines over TCP. Covers the security-critical encrypted socket layer (per-connection random PBKDF2 salt + AES-CBC IV, shared-key MagicNumber packet auth, tolerant handshake), the 4-machine pool / matrix and screen-edge transitions, cross-machine input injection (SendInput/keybd_event, Easy Mouse), clipboard + drag/drop file transfer, and the Windows service / elevation path used to inject on the logon / secure / UAC desktop. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/MouseWithoutBorders.'
license: Complete terms in LICENSE.txt
---

# PowerToys Mouse Without Borders Knowledge

Grounded engineering knowledge for the PowerToys **Mouse Without Borders** module
(`src/modules/MouseWithoutBorders/`). MWB lets one keyboard/mouse control **up to four** machines
on a LAN; it streams input, clipboard, and dragged files between them over encrypted TCP sockets.

The code was **ported like-for-like from the original standalone Mouse Without Borders app** into
PowerToys with deliberately minimal refactoring "in order to avoid introducing any defects while it
was being onboarded" ([PR #44553](https://github.com/microsoft/PowerToys/pull/44553) discussion).
It is now being cleaned up incrementally — the old `Common` god-class was split into partial classes
(`MachineStuff`, `Receiver`, `Clipboard`, …) across a 7-part series
([PR #44283](https://github.com/microsoft/PowerToys/pull/44283) = part 7 of 7). Expect old idioms:
static mutable state, hand-rolled socket framing, WinForms UI.

The module has two halves:
- **C++ module interface** (`ModuleInterface/`) — enable/disable, hotkey, and **service registration**.
- **C#/.NET app + helper + service** (`App/`) — all runtime logic (networking, input, clipboard, UI).

## When to Use This Skill

- Planning or implementing a change under `src/modules/MouseWithoutBorders/` and needing prior art.
- Touching the **encrypted socket layer** — key derivation, salt/IV, packet framing, the handshake,
  or anything on the wire (this is security-critical; see Regression Playbooks).
- Fixing/triaging: machines won't pair / "adding a PC makes the others vanish", connection drops,
  a modifier key stuck on a remote PC, Easy Mouse failing over an elevated window, clipboard/file
  transfer corrupting content, settings.json IO errors, "can't run as admin".
- Reviewing an MWB PR against the module's security invariants and regression traps.
- Working on the **service / elevation** path (inject on logon / UAC / screensaver desktop).

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring
below). Root: `src/modules/MouseWithoutBorders/`.

### Security: encryption & key exchange (`App/Core/Encryption.cs`) — HIGH SIGNAL
| Sub-feature | Implementation (file · symbol) |
|---|---|
| AES setup (256-bit, CBC, **Zeros** padding, 16-byte block) | `Encryption.cs` `InitEncryption` (`AesCryptoServiceProvider symAl`) |
| Key derivation from shared secret | `Encryption.cs` `GenLegalKey` = `Rfc2898DeriveBytes.Pbkdf2(MyKey, salt, KeyDerivationIterations=50000, SHA512, DerivedKeyLength=32)` |
| **Per-connection random salt + IV** (cleartext header) | `Encryption.cs` `GetEncryptedStream`/`GetDecryptedStream` build a `SaltSize(16)+SymAlBlockSize(16)` header via `RandomNumberGenerator.Fill`, exchanged by `ExchangeEncryptionHeader` |
| Tolerant header exchange (expected disconnect handling) | `Encryption.cs` `ExchangeEncryptionHeader` (swallows `EndOfStream`/`SocketException`/`ObjectDisposedException`) |
| Shared-key packet auth token | `Encryption.cs` `Get24BitHash` → `Encryption.MagicNumber` (four hash bytes; one initial SHA-512 plus 50,000 rehash rounds), with the upper 16 bits checked on the wire in `SocketStuff.ProcessReceivedDataEx` |
| Key generation / validation / display | `Encryption.cs` `CreateRandomKey` (16 chars), `IsKeyValid` (≥16 chars), `KeyDisplayedText` |
| Regression guard tests | `MouseWithoutBorders.UnitTests/Core/EncryptionTests.cs` |

### Networking / sockets (`App/Class/`)
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Per-connection encrypted/decrypted streams + random first-block handshake | `SocketStuff.cs` `EncryptedStream`/`DecryptedStream` → `Encryption.Get*Stream` + `Common.SendOrReceiveARandomDataBlockPerInitialIV` |
| TCP send/receive framing + MagicNumber check | `SocketStuff.cs` `TcpSendData`, `ProcessReceivedDataEx` |
| Two TCP servers (clipboard + message) | `SocketStuff.cs` `skClipboardServer`/`skMessageServer`, `TcpServer.cs` |
| Failed-attempt throttling | `SocketStuff.cs` `FailedAttempt` (`ConcurrentDictionary`) |
| Random block per initial IV (legacy handshake) | `Core/Common.cs` `SendOrReceiveARandomDataBlockPerInitialIV` |

### Machine pool / matrix & edge transitions (`App/Core/MachineStuff.cs`, `App/Class/MachinePool.cs`)
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Hard cap of **4** machines | `MachineStuff.cs` `MAX_MACHINE = 4` (`MAX_SOCKET = MAX_MACHINE*2`); enforced in `MachinePool.cs` (`list.Count >= 4` throws) |
| Add/track machines in the pool | `MachineStuff.cs` `AddToMachinePool`; `MachinePool.cs` (`Initialize`, `TryAddMachine`) |
| Screen-edge → neighbour switching | `MachineStuff.cs` `MoveToMyNeighbourIfNeeded`, `MoveLeft`/`MoveRight`, `NewDesMachineID`/`newDesMachineIdEx` |
| Machine matrix UI (2×2) | `App/Control/MachineMatrix.*`, `App/Form/frmMatrix.*`, `App/Control/Machine*.cs` |
| Package routing / handshake types | `Core/Receiver.cs` (`PackageType.Handshake`/`HandshakeAck`), `Core/PackageType.cs` |

### Cross-machine input injection (`App/Class/InputSimulation.cs`, `InputHook.cs`)
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Inject keyboard/mouse on remote | `InputSimulation.cs` `SendInputEx` (`SendInput`/`SendInput64`), `keybd_event`, `mouse_event` |
| Local capture hook | `InputHook.cs` (`WH_MOUSE_LL`/`WH_KEYBOARD_LL` in `NativeMethods`) |
| Easy Mouse (edge cross gated by Ctrl/Shift) | `InputHook.cs` `UpdateEasyMouseKeyDown`, `EasyMouseKeyDown`; `Class/EasyMouseOption.cs`; toggle hotkey `HotKeyToggleEasyMouse` |
| Wire payloads | `Core/DATA.cs`, `Core/KEYBDDATA.cs`, `Core/MOUSEDATA.cs` |

### Clipboard & file transfer (`App/Core/Clipboard.cs`, `DragDrop.cs`, `App/Class/IClipboardHelper.cs`)
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Clipboard share (text/image/files) | `Core/Clipboard.cs`, `Core/ClipboardPostAction.cs`, `Class/IClipboardHelper.cs` |
| Drag-and-drop file transfer | `Core/DragDrop.cs` |
| Telemetry for transfers | `App/Telemetry/MouseWithoutBordersClipboardFileTransferEvent.cs` |

### Service / elevation (`ModuleInterface/dllmain.cpp`, `App/Core/Service.cs`, `App/Service/`)
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Launch app, optionally in **service mode** | `ModuleInterface/dllmain.cpp` `launch_process` (`run_in_service_mode`, `UseService` property) |
| Register/unregister Windows service | `dllmain.cpp` `register_service`/`unregister_service` (`SERVICE_NAME = L"PowerToys.MWB.Service"`, `PowerToys.MouseWithoutBordersService.exe`) |
| Named-object DACL for cross-session/elevation IPC | `ModuleInterface/generateSecurityDescriptor.h` `generateSecurityDescriptor` |
| Run on logon / secure / screensaver desktop | `Core/Service.cs` `StartMouseWithoutBordersService` (winlogon session), `Common.RunOnLogonDesktop`/`RunOnScrSaverDesktop` |
| Settings persistence & key storage | `App/Class/Setting.cs` (`MyKey`, `IsMyKeyRandom`, settings.json) |

### External integration
| Sub-feature | Implementation |
|---|---|
| Command Palette (CmdPal) extension: toggle move, reconnect, toggle KBM | [PR #45350](https://github.com/microsoft/PowerToys/pull/45350) |

## Regression Playbooks

Rule by rule: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Fixed/reused salt & IV across connections (crypto weakness) — SECURITY
- **Symptom:** identical plaintext produces identical bytes on the wire; every connection reuses the
  same key-derivation material, enabling precomputed brute-force / rainbow-table attacks and IV reuse.
  This is the generic **static-IV / nonce-reuse** vulnerability class ([CWE-329 predictable/static IV](https://cwe.mitre.org/data/definitions/329.html),
  [CWE-323 nonce reuse](https://cwe.mitre.org/data/definitions/323.html)) applied to MWB's socket
  encryption. Evidence: [PR #48742](https://github.com/microsoft/PowerToys/pull/48742) (references **MSRC 118042**
  in `EncryptionTests.cs`).
- **Where:** `App/Core/Encryption.cs` `GetEncryptedStream`/`GetDecryptedStream`, `GenLegalKey`.
- **Root cause:** PBKDF2 salt and AES-CBC IV were both derived from a single fixed constant (the old
  `InitialIV`/`GenLegalIV`), so key + IV were constant across all sessions.
- **Guardrail:** generate a **fresh random salt + IV per connection** with `RandomNumberGenerator`,
  send them as the cleartext header, derive the key from the per-connection salt. Never cache the
  derived key (a per-connection salt makes caching pointless — the cache was removed). Keep the two
  regression tests green: `EncryptingSamePlainTextTwiceShouldProduceDifferentBytesOnTheWire` and
  `EachEncryptedStreamShouldEmitAUniqueHeader`. See [OWASP key-management](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
  and [random IV guidance](https://learn.microsoft.com/en-us/dotnet/standard/security/cryptographic-services).

### On-the-wire format change breaks mixed versions — COMPATIBILITY
- **Symptom:** paired machines fail to connect / decrypt after one is updated.
- **Where:** anything that changes the byte layout of the encrypted stream — the salt/IV header
  (`Encryption.cs`), the random first block (`Common.SendOrReceiveARandomDataBlockPerInitialIV`),
  `SocketStuff.TcpSendData`/`ProcessReceivedDataEx` framing, `MagicNumber`.
- **Root cause:** MWB has **no wire-version negotiation**; both ends assume an identical format. The
  salt/IV change ([#48742](https://github.com/microsoft/PowerToys/pull/48742)) explicitly noted "all
  paired machines must run this version."
- **Guardrail:** treat any wire change as breaking — call it out in the PR, keep sender and receiver
  symmetric, and update `GetEncryptedStream`/`GetDecryptedStream` (and any handshake) together. Do not
  ship a change that only one side understands.

### Packet auth checks only the upper 16 bits of MagicNumber — SECURITY
- **Symptom:** packets from a wrong-key/hostile peer must be rejected; a weak or mismatched token
  causes silent accept or total connection failure.
- **Where:** `Encryption.cs` `Get24BitHash`/`MagicNumber`; validated in `SocketStuff.cs`
  `ProcessReceivedDataEx` (`magic != (MagicNumber & 0xFFFF0000)`).
- **Root cause:** despite the `Get24BitHash` name, the derived value incorporates four hash bytes;
  packet framing transmits and validates only `MagicNumber & 0xFFFF0000`. The effective wire check
  is therefore 16 bits and gates routing, not confidentiality (confidentiality is the AES layer).
- **Guardrail:** don't weaken or bypass the MagicNumber check; keep it derived from `MyKey` and keep
  the shared-key requirement. Any change to key handling must update `MagicNumber` on both ends.

### Adding a machine drops the others / pool exceeds 4 — RELIABILITY
- **Symptom:** "every time I try to add another PC, all others vanish"; machines silently disappear
  from the matrix. Evidence: [#48825](https://github.com/microsoft/PowerToys/issues/48825).
- **Where:** `App/Class/MachinePool.cs` (`list.Count >= 4` throws
  `ArgumentException("…exceeded the maximum allowed limit of {MAX_MACHINE}…")`),
  `MachineStuff.cs` `AddToMachinePool`.
- **Root cause:** MWB supports **at most 4 machines** (`MAX_MACHINE = 4`); the pool rebuilds from the
  machine-matrix string, so a bad parse / overflow can wipe existing entries.
- **Guardrail:** preserve the 4-machine cap and its exception path; when editing pool add/replace
  logic, keep existing machines when a new one can't be added, and round-trip the matrix string
  without loss.

### Modifier key stuck / not released on the remote machine — RELIABILITY
- **Symptom:** Ctrl/Shift/Alt/AltGr stays "held" on the other PC (e.g. after toggling "All PC mode"),
  keyboard sync misbehaves. Evidence: [#49149](https://github.com/microsoft/PowerToys/issues/49149),
  [#48450](https://github.com/microsoft/PowerToys/issues/48450),
  [#48704](https://github.com/microsoft/PowerToys/issues/48704) (AltGr).
- **Where:** `App/Class/InputSimulation.cs` (`SendInputEx`, `keybd_event`), `InputHook.cs`
  key-up/down tracking, `Core/KEYBDDATA.cs`.
- **Root cause:** if a key-up event is dropped during a machine switch/toggle, the injected key on the
  remote side is never released; AltGr (`LCONTROL`+`RMENU`) is especially fragile.
- **Guardrail:** on machine switch / disable, flush/release held modifiers on the target; ensure every
  injected key-down has a matching key-up path even when the desktop switches.

### Easy Mouse / input fails when an elevated window is focused — SECURITY/ELEVATION
- **Symptom:** control doesn't transfer or keystrokes are dropped when an elevated app (e.g. Windows
  Terminal running as admin) is focused. Evidence:
  [#47633](https://github.com/microsoft/PowerToys/issues/47633),
  [#47561](https://github.com/microsoft/PowerToys/issues/47561).
- **Where:** `App/Class/InputSimulation.cs` injection; service path `Core/Service.cs`,
  `ModuleInterface/dllmain.cpp` service registration.
- **Root cause:** **UIPI** blocks a non-elevated process from injecting input into an elevated
  foreground window; MWB must run elevated / via the service to reach the secure/elevated desktop.
  See [UIPI / User Interface Privilege Isolation](https://learn.microsoft.com/en-us/windows/win32/winmsg/about-messages-and-message-queues#uipi).
- **Guardrail:** don't "fix" this purely in the injection layer — it requires the elevated **MWB
  service**. Preserve the service registration (`PowerToys.MWB.Service`) and its DACL; when changing
  input injection, test against an elevated foreground window.

### Clipboard / file transfer corrupts or crashes — RELIABILITY
- **Symptom:** Excel cells arrive as pictures ([#49176](https://github.com/microsoft/PowerToys/issues/49176)),
  rich text mangled ([#47828](https://github.com/microsoft/PowerToys/issues/47828)), or sharing the
  clipboard crashes the network stack with a specific NIC
  ([#47782](https://github.com/microsoft/PowerToys/issues/47782)).
- **Where:** `Core/Clipboard.cs`, `Core/ClipboardPostAction.cs`, `Class/IClipboardHelper.cs`,
  `Core/DragDrop.cs`.
- **Root cause:** clipboard formats are negotiated/downgraded (e.g. only a bitmap format survives);
  large transfers stress the socket path.
- **Guardrail:** preserve the richest available clipboard format and its fallback order; guard large
  payload framing; keep transfers off the UI thread. Add/adjust `MouseWithoutBordersClipboardFileTransferEvent`
  telemetry when changing transfer paths.

### settings.json read/write failures — RELIABILITY
- **Symptom:** "Exception encountered while saving settings … System.IO.IOException"
  ([#47039](https://github.com/microsoft/PowerToys/issues/47039)); settings file locked/"read by
  another program" ([#48708](https://github.com/microsoft/PowerToys/issues/48708)).
- **Where:** `App/Class/Setting.cs`.
- **Root cause:** multiple MWB processes (per-desktop / service instances) contend for settings.json;
  no shared-read/retry on IO.
- **Guardrail:** open settings with sharing + retry; never assume single-writer. The **shared secret
  `MyKey` lives here** — don't log it, don't widen its file permissions.

## Review Rules

Enforce these when reviewing or authoring MWB changes:

- **Never regress to a fixed salt/IV or a cached derived key.** Per-connection random salt + IV are a
  security fix ([#48742](https://github.com/microsoft/PowerToys/pull/48742), MSRC 118042). Keep
  `EncryptionTests` green.
- **Any wire-format change is a breaking, all-machines-must-update change.** MWB has no version
  negotiation — sender and receiver must change together; call it out explicitly.
- **Keep the shared-key model intact.** Key derivation = PBKDF2/SHA512, 50000 iterations, 32-byte
  AES-256 key; `MagicNumber` is derived from four hash bytes, while packet validation uses its upper
  16 bits. Don't lower iteration count, key size, or effective token strength without security
  review. Prefer named constants (`KeyDerivationIterations`,
  `DerivedKeyLength`, `SaltSize`) over magic numbers ([PR #41280](https://github.com/microsoft/PowerToys/pull/41280) review).
- **Never log or leak `MyKey`** (the shared secret) or the negotiated key material. Salt/IV are
  intentionally cleartext; the key never is.
- **Respect `MAX_MACHINE = 4`** and preserve existing pool entries when an add fails
  ([#48825](https://github.com/microsoft/PowerToys/issues/48825)).
- **Balance every injected key-down with a key-up**, and release held modifiers on machine switch /
  disable ([#49149](https://github.com/microsoft/PowerToys/issues/49149), AltGr [#48704](https://github.com/microsoft/PowerToys/issues/48704)).
- **Elevated-desktop input requires the MWB service** — don't remove/short-circuit service
  registration or its security descriptor when refactoring launch/elevation
  ([#47633](https://github.com/microsoft/PowerToys/issues/47633)).
- **Clipboard/file transfer must preserve format fidelity and run off the UI thread**; keep the
  transfer telemetry event accurate ([#49176](https://github.com/microsoft/PowerToys/issues/49176),
  [#47828](https://github.com/microsoft/PowerToys/issues/47828)).
- **Refactors stay behaviour-preserving.** MWB was ported like-for-like; the cleanup series
  ([#44283](https://github.com/microsoft/PowerToys/pull/44283), [#44553](https://github.com/microsoft/PowerToys/pull/44553))
  only reshapes structure. Avoid brittle tests that serialize environment-dependent data such as stack
  traces (`Logger.GetStackTrace` test was flaky across test hosts, [#44553](https://github.com/microsoft/PowerToys/pull/44553)).
- **Build hygiene:** use `$(RepoRoot)` not relative `..\..\` in project files
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)); keep `packages.config` and
  `vcxproj` package versions in sync ([#49050](https://github.com/microsoft/PowerToys/pull/49050)).

## Pitfalls

- **Salt and IV are sent in the clear on purpose** — confidentiality depends only on the shared-key
  PBKDF2 derivation, not on the salt/IV being secret. Do not "harden" by hiding them; do keep them
  **random per connection**.
- **`MagicNumber` is only a routing/auth token**, not encryption. It is derived from `MyKey` after
  one initial SHA-512 plus 50,000 rehash rounds, but current framing validates only its upper
  16 bits; AES-CBC provides confidentiality.
- **AES padding is `PaddingMode.Zeros`** (not PKCS7) with a 256-bit key / 128-bit block. Round-trip
  tests use lengths that are exact multiples of 16 to avoid trailing-zero ambiguity — remember this
  when writing new crypto tests.
- **Header exchange deliberately swallows expected disconnects** (`ExchangeEncryptionHeader` ignores
  `EndOfStreamException` / `SocketException` / `ObjectDisposedException`) because remote machines close
  connections during desktop switches. Don't turn these into hard errors.
- **MWB runs multiple processes** (per desktop: default, logon, screensaver; plus a service). State in
  `static` fields is per-process; settings.json is shared and contended.
- **At most 4 machines.** UI, sockets (`MAX_SOCKET = 8`), and pool all assume it.
- **The shared secret `MyKey` (min 16 chars) is stored in settings.json.** Treat that file as
  sensitive; other apps reading it is a real complaint ([#48708](https://github.com/microsoft/PowerToys/issues/48708)).
- **Injecting into an elevated foreground window needs elevation/the service** (UIPI) — a bug here is
  usually a privilege problem, not an injection-code bug.
- **Two independent TCP servers** carry clipboard vs. control messages; a change to framing must be
  applied to both paths.
- **The C# `App` is a straight port** — expect non-idiomatic patterns; prefer minimal, verified
  changes over sweeping rewrites in one PR.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + crypto/wire constants.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to an MWB PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/MouseWithoutBorders/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/MouseWithoutBorders)
- [.NET cryptography services](https://learn.microsoft.com/en-us/dotnet/standard/security/cryptographic-services) · [PBKDF2 / Rfc2898DeriveBytes](https://learn.microsoft.com/en-us/dotnet/api/system.security.cryptography.rfc2898derivebytes) · [OWASP Cryptographic Storage](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html) · [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) · [UIPI](https://learn.microsoft.com/en-us/windows/win32/winmsg/about-messages-and-message-queues#uipi) · [Windows services](https://learn.microsoft.com/en-us/windows/win32/services/services)
