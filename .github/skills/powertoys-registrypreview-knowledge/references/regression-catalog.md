# RegistryPreview Regression Catalog

Fuller reference behind the SKILL.md `Regression Playbooks`. Progressive disclosure — load when you
need the detail. Every entry is grounded in source (`src/modules/registrypreview/`) and/or a real
GitHub issue. Confirm in source before acting.

## Parser reference (grounded in `ParseRegistryFile` / `ParseHelper.cs`)

### Value-type prefixes → decode
| `.reg` prefix | RegistryValue.Type | Decode |
|---|---|---|
| `dword:` | `REG_DWORD` | `uint.TryParse(NumberStyles.HexNumber)` → `0x{dword:x8} ({dword})` |
| `hex(b):` | `REG_QWORD` | comma bytes (each 2 digits) → `BitConverter.ToUInt64` (little-endian) |
| `hex:` | `REG_BINARY` | comma bytes → space-joined `x2` hex |
| `hex(0):` | `REG_NONE` | same as binary; empty → `IsEmptyBinary`, `ZeroLength` text |
| `hex(2):` | `REG_EXPAND_SZ` | bytes → `Encoding.Unicode`, `\0`→`\r`, TrimEnd; expanded via `Environment.ExpandEnvironmentVariables` in preview |
| `hex(7):` | `REG_MULTI_SZ` | bytes → `Encoding.Unicode`, `\0`→`\r`, TrimEnd |
| `"..."` | `REG_SZ` | escaped-char validated; only `\"` and `\\` allowed |
| (no valid RHS) | `ERROR` | shows localized `Invalid*` string |

### Invariants
- Header must be `REGEDIT4` or `Windows Registry Editor Version 5.00` (case-insensitive) or the file
  is rejected with `InvalidRegistryFile`.
- Input normalized `\r\n`→`\r`, then split on `\r`; a value line ending `,\` continues to the next
  line (comment-stripped + left-trimmed) before decoding.
- Each comma-separated hex chunk must be exactly 2 digits; otherwise the value becomes `ERROR`.
- `@=` → `"(Default)"=`; `@=-` → clears the default value (`"(Default)"=""`).
- Keys must start with a known root (long name or `HKCR/HKCU/HKU/HKLM/HKCC` abbreviation), bare or
  with a sub-path, else the error icon is shown (`CheckForKnownGoodBranches`).
- Comments start at the first `;` **outside** a quoted string; REG_SZ skips comment scanning.

## Regression entries

### R1 — REG_MULTI_SZ / REG_EXPAND_SZ line breaks ignored
- **Issue:** [#36629](https://github.com/microsoft/PowerToys/issues/36629) (fix cited in-source).
- **Where:** `ParseRegistryFile` `case "REG_EXPAND_SZ"/"REG_MULTI_SZ"`.
- **Root cause:** UTF-16 decode kept NUL (`\0`) separators, so multi-strings rendered as one line.
- **Guardrail:** `value.Replace('\0', '\r').TrimEnd('\r')` after `Encoding.Unicode.GetString`.

### R2 — Value type incorrectly identified
- **Issue:** [#30713](https://github.com/microsoft/PowerToys/issues/30713).
- **Where:** prefix detection + decode `case`; 2-hex-digit chunk parse.
- **Root cause:** type label and decode branch can diverge; non-2-digit chunks mis-handled.
- **Guardrail:** keep detection and decode in sync; malformed chunk → `ERROR` with localized text.

### R3 — HKEY abbreviations rejected
- **Issue:** [#31562](https://github.com/microsoft/PowerToys/issues/31562) (PR #31552).
- **Where:** `ParseHelper.cs::CheckForKnownGoodBranches`.
- **Root cause:** allow-list originally only had the five long root names.
- **Guardrail:** include long + abbreviated roots, in bare and sub-path forms.

### R4 — Multi-line hex continuation
- **Where:** `ParseRegistryFile` continuation `while` loop + lone-`\` padding pre-checks.
- **Root cause:** long hex values span lines with trailing `\`; continuation lines need
  comment-strip + trim, and a bare `\` after the marker must become `,\` to trigger the loop.
- **Guardrail:** preserve the padding and per-line `ScanAndRemoveComments`/`TrimStart`.

### R5 — "The REG file editor could not be opened" / Edit failures
- **Issues:** [#36920](https://github.com/microsoft/PowerToys/issues/36920),
  [#34269](https://github.com/microsoft/PowerToys/issues/34269).
- **Where:** `OpenRegistryEditor` (`regedit.exe`, `UseShellExecute = true`), try/catch → `UACDialogError`.
- **Root cause:** external launch depends on edit target / shell / UAC; failures surface generically.
- **Guardrail:** keep launch guarded and show the localized dialog; never assume the process starts.

### R6 — Window state not restored / closes on open
- **Issues:** [#46573](https://github.com/microsoft/PowerToys/issues/46573),
  [#36630](https://github.com/microsoft/PowerToys/issues/36630).
- **Where:** `MainWindow.Utilities.cs` placement JSON; `MainWindow.Events.cs`.
- **Root cause:** placement JSON is best-effort (`{ }` fallback) and desyncs from window state.
- **Guardrail:** persist real state on close; validate JSON round-trip.

### R7 — Commented URL opens browser / comment scope
- **Issue:** [#37447](https://github.com/microsoft/PowerToys/issues/37447).
- **Where:** `ScanAndRemoveComments`; Monaco link handling; REG_SZ excluded from comment scan.
- **Root cause:** a `;`-commented URL was still made clickable by the editor.
- **Guardrail:** only strip comments for non-string types; don't action commented text.

### R8 — Malformed input hardening (fuzzed surface)
- **Where:** `RegistryPreview.FuzzTests/FuzzTests.cs` exercises `CheckKeyLineForBrackets`,
  `StripFirstAndLast`, `StripEscapedCharacters`, `ProcessRegistryLine` with arbitrary bytes.
- **Guardrail:** these helpers must never throw on adversarial input — e.g. `StripFirstAndLast`
  guards `Length > 1`; `CheckKeyLineForBrackets` handles a missing `]`. Add fuzz/unit coverage for
  any new parser helper.

## Cross-cutting notes (from module history)
- Most PRs touching this module in the mined window were repo-wide build/deps/upgrade changes
  (e.g. .NET 10, WASDK 1.8.5, CppWinRT bump, `$(RepoRoot)` reference cleanup) rather than
  RegistryPreview features — after any SDK/build bump, smoke-test the Monaco editor and HexBox.
- Known accessibility/theming issues touch the shared Monaco editor and the value grid
  (High Contrast text selection, Narrator on the value list); verify against the shared control,
  not RegistryPreview-only code.
