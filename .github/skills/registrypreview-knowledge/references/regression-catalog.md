# RegistryPreview Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This file is the historical evidence store behind `SKILL.md`.

**Role split:** `SKILL.md` owns the current engineering playbooks, review rules, and actionable
guardrails. This ledger retains provenance: source locations, issue/PR evidence, chronology,
maintainer decisions, unresolved clusters, and evidence caveats. Do not duplicate the playbook
mechanics here; confirm all source observations against the current tree before acting.

## Evidence ledger

| ID | Evidence / observation | Source location | History / provenance | Caveat |
|---|---|---|---|---|
| RP-E01 | Accepted headers are `REGEDIT4` and `Windows Registry Editor Version 5.00`, case-insensitively; other headers produce `InvalidRegistryFile`. | `RegistryPreviewUILib/RegistryPreviewMainPage.Utilities.cs` · `ParseRegistryFile`; header constants in `RegistryPreviewMainPage.xaml.cs` | Source verification | Current implementation evidence, not an external format guarantee. |
| RP-E02 | Parsing normalizes `\r\n` to `\r`, splits on `\r`, and joins value continuations ending in `,\`; continuation lines are comment-stripped and left-trimmed. Lone `\` payloads are padded to `,\` before continuation processing. | `RegistryPreviewMainPage.Utilities.cs` · `ParseRegistryFile`, `ScanAndRemoveComments` | Source verification | Ordering is material; this records observed behavior rather than prescribing a parser redesign. |
| RP-E03 | Prefix mapping is `dword:`→`REG_DWORD`, `hex(b):`→`REG_QWORD`, `hex:`→`REG_BINARY`, `hex(0):`→`REG_NONE`, `hex(2):`→`REG_EXPAND_SZ`, `hex(7):`→`REG_MULTI_SZ`, and quoted text→`REG_SZ`. Invalid right-hand sides become `ERROR`. | `RegistryPreviewMainPage.Utilities.cs` · `ParseRegistryFile` | Source verification; wrong-type report [#30713](https://github.com/microsoft/PowerToys/issues/30713) | Type labels and decode behavior have historically drifted together. |
| RP-E04 | Hex payload chunks must be exactly two digits. DWORD uses hexadecimal `uint.TryParse`; QWORD uses parsed bytes with `BitConverter.ToUInt64` (little-endian); binary/none render byte values; empty `REG_NONE` has a distinct zero-length display. | `RegistryPreviewMainPage.Utilities.cs` · `ParseRegistryFile` decode cases | Source verification; [#30713](https://github.com/microsoft/PowerToys/issues/30713) | Localized error text varies by type. |
| RP-E05 | `REG_EXPAND_SZ` and `REG_MULTI_SZ` decode with `Encoding.Unicode`; NUL separators are converted to carriage returns and trailing separators removed. Expand-string preview additionally calls `Environment.ExpandEnvironmentVariables`. | `ParseRegistryFile` `REG_EXPAND_SZ`/`REG_MULTI_SZ` cases; `RegistryPreviewMainPage.DataPreview.cs` | Line-break regression [#36629](https://github.com/microsoft/PowerToys/issues/36629), whose fix is cited in source | The issue establishes the regression; current behavior was verified in source. |
| RP-E06 | REG_SZ escape validation accepts only `\"` and `\\`. Semicolon comments begin outside quoted strings; the REG_SZ path intentionally avoids ordinary comment scanning. | `ParseRegistryFile`; `ScanAndRemoveComments`; `ParseHelper.cs::StripEscapedCharacters` | Commented-URL report [#37447](https://github.com/microsoft/PowerToys/issues/37447) | Monaco link activation is a separate surface from parser comment removal. |
| RP-E07 | `@=` is normalized to the `(Default)` value and `@=-` clears it. | `ParseHelper.cs::ProcessRegistryLine`; corresponding `ParseRegistryFile` handling | Source verification | Both helper and inline parsing paths exist. |
| RP-E08 | Root validation accepts the five long HKEY names plus `HKCR`, `HKCU`, `HKU`, `HKLM`, and `HKCC`, both bare and with subpaths. | `ParseHelper.cs::CheckForKnownGoodBranches` | Valid abbreviations were rejected in [#31562](https://github.com/microsoft/PowerToys/issues/31562); fixed by PR #31552 | Issue/PR chronology is preserved; re-check the allow-list before extending aliases. |
| RP-E09 | Parser helpers are fuzzed with arbitrary bytes, including bracket checks, first/last stripping, escaped-character stripping, and registry-line processing. | `RegistryPreview.FuzzTests/FuzzTests.cs` | Source verification | The fuzz harness identifies a hardening boundary, not proof that every parser path is covered. |
| RP-E10 | Registry editing shells `regedit.exe` with `UseShellExecute = true`; exceptions surface through the localized UAC/error dialog. | `RegistryPreviewMainPage.Utilities.cs` · `OpenRegistryEditor` | Edit/launch failures [#34269](https://github.com/microsoft/PowerToys/issues/34269), [#36920](https://github.com/microsoft/PowerToys/issues/36920) | Reports do not isolate one universal launch cause. |
| RP-E11 | Window placement is persisted as JSON and falls back to `{ }` on load/parse/IO failure. | `RegistryPreview/MainWindow.Utilities.cs`; `MainWindow.Events.cs` | Close/restore reports [#36630](https://github.com/microsoft/PowerToys/issues/36630), later [#46573](https://github.com/microsoft/PowerToys/issues/46573) | The reports cluster symptoms; source verification supplies the persistence mechanism. |
| RP-E12 | Tree de-duplication walks key paths backward and uses the last matching segment; replacing every matching segment would collapse repeated key names. | `RegistryPreviewMainPage.Utilities.cs` · `AddTextToTree` and its source comment | Source verification | No linked issue was retained in the mined corpus. |

## Decision ledger

| ID | Decision / review outcome | Basis | Status |
|---|---|---|---|
| RP-D01 | Preserve long and abbreviated registry roots in bare and subpath forms. | #31562 → PR #31552; `CheckForKnownGoodBranches` | Accepted and implemented |
| RP-D02 | Treat malformed hex as a localized value-level `ERROR`, not an exception escaping to the UI. | #30713; decode cases; fuzz boundary | Established parser contract |
| RP-D03 | Keep UTF-16LE decoding and visible separator conversion for expand/multi-string values. | #36629; current decode cases | Accepted and implemented |
| RP-D04 | Keep registry-process launch best-effort and user-visible on failure; do not infer success from `Process.Start`. | #34269, #36920; `OpenRegistryEditor` | Established review decision |
| RP-D05 | Preserve UTF-16 output for saved `.reg` files. | `SaveFile` uses `System.Text.Encoding.Unicode` | Current implementation decision |
| RP-D06 | Require malformed-input coverage when parser helpers or branches change. | Existing fuzz harness and historical parser regressions | Ongoing review decision |
| RP-D07 | Treat shared Monaco/HexBox behavior as cross-component evidence when SDK or shared-control changes land. | Module history and shared-control ownership | Ongoing review decision |

## Open evidence clusters

- **Window lifecycle / placement:** [#36630](https://github.com/microsoft/PowerToys/issues/36630)
  and [#46573](https://github.com/microsoft/PowerToys/issues/46573) describe close/restore symptoms,
  but the corpus does not establish a single closed root cause.
- **Editor launch environment:** [#34269](https://github.com/microsoft/PowerToys/issues/34269)
  and [#36920](https://github.com/microsoft/PowerToys/issues/36920) remain a symptom cluster across
  shell, edit-target, and UAC conditions.
- **Accessibility and theming:** High Contrast selection and Narrator behavior involve the shared
  Monaco editor and value grid; evidence should be checked at the shared-control boundary rather
  than attributed solely to RegistryPreview.
- **SDK/build churn:** much of the mined module history was repo-wide (.NET 10, WASDK 1.8.5,
  CppWinRT, and `$(RepoRoot)` cleanup), so a module smoke test is evidence of integration health,
  not evidence that RegistryPreview logic itself changed.

## Evidence caveats

- Source locations describe the tree inspected when this ledger was assembled and may move.
- Issue links often establish the user-visible symptom, while root-cause detail comes from source;
  do not present inferred causality as maintainer-authored issue text.
- The ledger intentionally omits the repeated symptom → root cause → guardrail instructions already
  maintained in `SKILL.md`.
