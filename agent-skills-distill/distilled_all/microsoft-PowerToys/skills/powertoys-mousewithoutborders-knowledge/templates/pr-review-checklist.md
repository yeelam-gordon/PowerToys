# Mouse Without Borders — PR Review Checklist

Apply **after** reading the diff cold (see anti-anchoring in SKILL.md). Only check rows for the code
paths the PR actually touches. Security (crypto / wire format) and elevation are the highest-stakes
areas.

## Security: encryption & wire format (if `Encryption.cs` / `SocketStuff.cs` / `Common.cs` handshake touched)
- [ ] Salt and IV are **random per connection** (`RandomNumberGenerator`), never a fixed constant
      (regression: MSRC 118042 / #48742).
- [ ] No derived-key cache reintroduced (a per-connection salt makes caching pointless/wrong).
- [ ] Key derivation unchanged unless security-reviewed: PBKDF2 / SHA512 / 50000 iterations /
      32-byte AES-256 key; magic numbers use the named constants (`KeyDerivationIterations`,
      `DerivedKeyLength`, `SaltSize`).
- [ ] `MagicNumber` still derived from `MyKey`; packet auth check in `ProcessReceivedDataEx` intact.
- [ ] `MyKey` / key material is **never logged** or written to widened-permission files.
- [ ] Any change to the byte layout (salt/IV header, random first block, packet framing) is applied
      **symmetrically** to sender and receiver, and flagged as a breaking, all-machines-must-update change.
- [ ] Regression tests still pass: `EncryptingSamePlainTextTwiceShouldProduceDifferentBytesOnTheWire`,
      `EachEncryptedStreamShouldEmitAUniqueHeader`, `EncryptThenDecryptShouldRoundTripPlainText`.
- [ ] Expected-disconnect handling preserved in `ExchangeEncryptionHeader` (don't turn socket-close
      into a hard error).

## Machine pool / matrix
- [ ] `MAX_MACHINE = 4` cap preserved (pool, `MAX_SOCKET`, UI).
- [ ] Existing machines are not dropped when an add fails; matrix string round-trips losslessly (#48825).

## Input injection
- [ ] Every injected key-down has a matching key-up; held modifiers released on machine switch/disable
      (#49149, AltGr #48704).
- [ ] Injection into elevated foreground windows still routes through the MWB service (UIPI, #47633);
      service registration + security descriptor untouched (or intentionally, correctly changed).

## Clipboard / file transfer
- [ ] Richest clipboard format preserved with correct fallback order (#49176, #47828).
- [ ] Large transfers framed safely and run off the UI thread.
- [ ] `MouseWithoutBordersClipboardFileTransferEvent` telemetry still accurate.

## Settings
- [ ] settings.json opened with sharing + retry; no single-writer assumption (#47039, #48708).
- [ ] `MyKey` handling keeps the ≥16-char rule and stays out of logs.

## Refactor / build hygiene
- [ ] Refactors are behaviour-preserving (MWB is a like-for-like port; #44283 / #44553 series).
- [ ] No brittle tests serializing environment-dependent data (e.g. stack traces, #44553).
- [ ] Project files use `$(RepoRoot)`, not `..\..\` (#44639); `packages.config` and `vcxproj`
      package versions in sync (#49050).
