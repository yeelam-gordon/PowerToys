# Mouse Without Borders — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This catalog is the progressive-disclosure evidence record for `SKILL.md`.

> **Role split:** `SKILL.md` owns actionable symptom → root-cause → guardrail guidance. This file
> owns provenance: exact source anchors, historical decisions, reviewer rationale, unresolved issue
> clusters, chronology, and confidence caveats. Do not duplicate the playbook prose here.

## Verified source-anchor ledger

| Area | Exact source anchors | Evidence retained |
|---|---|---|
| Encryption constants | `App/Core/Encryption.cs`: `SaltSize = 16`, `SymAlBlockSize = 16`, `KeyDerivationIterations = 100000`, `DerivedKeyLength = 32` | AES-256/CBC uses `PaddingMode.Zeros`; PBKDF2 uses SHA512. |
| Connection header | `Encryption.GetEncryptedStream`, `GetDecryptedStream`, `ExchangeEncryptionHeader` | Cleartext layout is salt (16 bytes) followed by IV (16 bytes). |
| Handshake/framing | `Common.SendOrReceiveARandomDataBlockPerInitialIV`; `SocketStuff.TcpSendData`, `ProcessReceivedDataEx` | Header → random first block → framed packets. No wire-version negotiation was identified. |
| Shared-key token | `Encryption.Get24BitHash`, `Encryption.MagicNumber`; validation in `SocketStuff.ProcessReceivedDataEx` | The misleadingly named helper incorporates four hash bytes, while packet framing transmits and validates only the upper 16 bits. It is not the encryption layer. |
| Key lifecycle | `Encryption.CreateRandomKey`, `IsKeyValid`; `App/Class/Setting.cs` | Generated keys are 16 characters; validation requires at least 16; the shared secret persists in `settings.json`. |
| Machine limit | `App/Core/MachineStuff.cs`: `MAX_MACHINE = 4`, `MAX_SOCKET = MAX_MACHINE * 2`; `App/Class/MachinePool.cs` | Pool enforcement throws when `list.Count >= 4`; UI anchors are `App/Control/MachineMatrix.*` and `App/Form/frmMatrix.*`. |

## Decision chronology

Ordered by the repository history represented in this corpus.

| Change | Evidence | Decision / reviewer record |
|---|---|---|
| MTP migration | [PR #37651](https://github.com/microsoft/PowerToys/pull/37651) | Dependency/platform migration; no MWB-specific behavioral decision was distilled. |
| .NET 10 and PBKDF2 API update | [PR #41280](https://github.com/microsoft/PowerToys/pull/41280) | Reviewer requested named constants for the 50,000 iterations and 32-byte derived key rather than embedded literals. |
| WIL update | [PR #43503](https://github.com/microsoft/PowerToys/pull/43503) | Dependency update retained only as build-history context. |
| `Common` split, part 7 | [PR #44283](https://github.com/microsoft/PowerToys/pull/44283) | Incremental structural split; behavior preservation remained the governing constraint. |
| Like-for-like port/refactor context | [PR #44553](https://github.com/microsoft/PowerToys/pull/44553) | Maintainers emphasized defect avoidance. A `Logger.GetStackTrace` test was brittle across test hosts, establishing a caveat against environment-dependent serialized test data. |
| Project-path migration | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | Use `$(RepoRoot)`; avoid `..\..\` and mixed `$(SolutionDir)`/`$(ProjectDir)$(RepoRoot)` path construction. |
| C++/WinRT update | [PR #45420](https://github.com/microsoft/PowerToys/pull/45420) | Reviewer requested x64 and ARM64, Debug and Release evidence for solution-wide C++/WinRT changes. |
| Dependency trimming | [PR #47233](https://github.com/microsoft/PowerToys/pull/47233) | MWB package references were reduced; installer-side discussion was not treated as MWB behavioral evidence. |
| Per-connection salt and IV | [PR #48742](https://github.com/microsoft/PowerToys/pull/48742) | Security fix referencing **MSRC 118042** in `EncryptionTests.cs`: removed fixed `InitialIV`/`GenLegalIV` behavior and the derived-key cache. The wire layout changed, so all peers must update together. |
| WIL project/package drift | [PR #49050](https://github.com/microsoft/PowerToys/pull/49050) | Keep `vcxproj` and `packages.config` dependency versions synchronized. |

## Open symptom-cluster ledger

These are issue signals, not established root causes. Confirm against current source and obtain a
reproduction before changing behavior.

| Cluster | Reports | Current evidence boundary |
|---|---|---|
| Adding/pairing machines disrupts the pool | [#48825](https://github.com/microsoft/PowerToys/issues/48825) | Relevant anchors are `MachinePool` parsing/addition and the four-machine cap; the issue alone does not prove overflow is the cause. |
| Non-ASCII machine identity | [#47673](https://github.com/microsoft/PowerToys/issues/47673) | Japanese/non-ASCII device name cannot connect; encoding boundary remains unverified here. |
| General connectivity failures | [#48551](https://github.com/microsoft/PowerToys/issues/48551), [#48208](https://github.com/microsoft/PowerToys/issues/48208), [#48136](https://github.com/microsoft/PowerToys/issues/48136) | Similar symptoms may span discovery, handshake, firewall, key, or version mismatch. Do not collapse them into one cause. |
| Modifier/keyboard state | [#49149](https://github.com/microsoft/PowerToys/issues/49149), [#48450](https://github.com/microsoft/PowerToys/issues/48450), [#49282](https://github.com/microsoft/PowerToys/issues/49282), [#48704](https://github.com/microsoft/PowerToys/issues/48704) | Covers All PC mode, keyboard sync/dead remote keyboard, and AltGr. Input-event loss is a hypothesis, not a finding for every report. |
| Elevated-window input | [#47633](https://github.com/microsoft/PowerToys/issues/47633) (closed), [#47561](https://github.com/microsoft/PowerToys/issues/47561) | Correlates with Windows Terminal/elevated focus; validate service/elevation state before changing injection code. |
| Clipboard format fidelity | [#49176](https://github.com/microsoft/PowerToys/issues/49176), [#47828](https://github.com/microsoft/PowerToys/issues/47828) | Excel cells becoming pictures and rich-text corruption may involve different clipboard-format negotiation paths. |
| Clipboard/network failure | [#47782](https://github.com/microsoft/PowerToys/issues/47782) | Specific-NIC network-stack crash report; hardware/driver specificity limits generalization. |
| Admin/service lifecycle | [#48137](https://github.com/microsoft/PowerToys/issues/48137), [#48047](https://github.com/microsoft/PowerToys/issues/48047), [#47746](https://github.com/microsoft/PowerToys/issues/47746), [#48787](https://github.com/microsoft/PowerToys/issues/48787) (closed) | Includes run/exit-admin behavior, the “Disable C-A-D” secpol effect, and Runner-window lifecycle. Treat as separate service/policy/lifecycle paths. |
| Settings-file contention | [#47039](https://github.com/microsoft/PowerToys/issues/47039), [#48708](https://github.com/microsoft/PowerToys/issues/48708) | Reports `IOException`/file-in-use symptoms. Multiple desktop/service processes are relevant, but the exact locking sequence must be reproduced. |

## Caveats and exclusions

- The 32-byte salt/IV header is intentionally cleartext; this ledger records layout, not a claim
  that the header authenticates the peer.
- `MagicNumber` is shared-key-derived, but current packet framing validates only its upper 16 bits;
  do not infer modern authenticated-encryption guarantees from it.
- The open-issue table preserves symptoms and candidate anchors only. Issue titles are not proof of
  causality, common scope, or current reproducibility.
- Pure formatting, “fixed in latest commit,” `/azp run`, LGTM, and non-MWB installer/WinUI3Apps
  hardlink threads from [PR #47233](https://github.com/microsoft/PowerToys/pull/47233) were excluded.
