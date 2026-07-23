---
name: registrypreview-knowledge
description: 'PowerToys RegistryPreview module knowledge: feature->file/function map, .reg parsing rules (dword/hex/hex(b)/hex(2)/hex(7)/hex(0) type prefixes, multi-line hex continuation, REG_MULTI_SZ/REG_EXPAND_SZ Unicode decode, QWORD little-endian, HKEY abbreviations), tree visualization, write-to-registry via regedit + UAC elevation, malformed-input hardening/fuzz tests, review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/registrypreview — .reg parsing, value type detection, hex/binary decoding, TreeView build, data preview (HexBox/Monaco), save/merge, elevation. Keywords: RegistryPreview, .reg file, registry, hex, dword, qword, REG_MULTI_SZ, REG_BINARY, regedit, elevation, TreeView, Monaco, HexBox, fuzz, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys RegistryPreview Knowledge

Grounded engineering knowledge for the PowerToys **RegistryPreview** module — a WinUI 3 utility that
parses a Windows `.reg` file, renders its keys as a `TreeView` and its values in a grid (with a
per-value hex/binary/expand data preview), lets the user edit the raw text in a Monaco editor, and
optionally writes the file into the real registry by launching `regedit.exe`. Use it to localize
code fast, avoid known parsing/regression traps, and enforce conventions maintainers established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/registrypreview/` and needing prior art.
- Fixing/triaging a RegistryPreview bug: value shown with wrong type, hex/binary/QWORD decoded
  wrong, REG_MULTI_SZ line breaks lost, a valid key flagged as an error, tree drawn wrong,
  "REG file editor could not be opened", crash on open/close, or window-state not restored.
- Reviewing a RegistryPreview PR and checking it against parser invariants and regression traps.
- Adding a new value-type prefix, touching the `.reg` tokenizer, the `TreeView` builder, the
  data-preview dialog (`HexBox`), or the write-to-registry / elevation path.
- Hardening the parser against malformed input (fuzz-tested surface).

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| `.reg` file open → read text | `RegistryPreviewUILib/RegistryPreviewMainPage.Utilities.cs` `OpenRegistryFile` |
| **Core `.reg` parser** (line-by-line state machine) | `RegistryPreviewMainPage.Utilities.cs` `ParseRegistryFile` |
| Header validation (`REGEDIT4` / `Windows Registry Editor Version 5.00`) | `ParseRegistryFile` header `switch`; consts `REGISTRYHEADER4/5` in `RegistryPreviewMainPage.xaml.cs` |
| Key line bracket check + known-root validation | `RegistryPreviewUILib/ParseHelper.cs` `CheckKeyLineForBrackets`, `CheckForKnownGoodBranches` |
| HKEY abbreviation support (`HKCR/HKCU/HKU/HKLM/HKCC`) | `ParseHelper.cs::CheckForKnownGoodBranches` |
| `@=` → `(Default)` value, `@=-` delete-default | `ParseHelper.cs::ProcessRegistryLine`; also inline in `ParseRegistryFile` |
| Value-type detection (`dword:`, `hex:`, `hex(b):`, `hex(2):`, `hex(7):`, `hex(0):`) | `ParseRegistryFile` prefix `if/else` chain |
| Multi-line hex continuation (`,\` at EOL keeps reading) | `ParseRegistryFile` `while (value.EndsWith(@",\"))` loop |
| Comment stripping (`;` to end of line) | `RegistryPreviewMainPage.Utilities.cs` `ScanAndRemoveComments` |
| Escaped-char unescape (`\\`→`\`, `\"`→`"`) | `ParseHelper.cs::StripEscapedCharacters` (and Utilities copy) |
| REG_SZ escape validation (only `\"` and `\\`) | `ParseRegistryFile` `case "REG_SZ"` |
| REG_BINARY / REG_NONE decode (2-hex-digit bytes) | `ParseRegistryFile` `case "REG_BINARY"/"REG_NONE"` |
| REG_DWORD decode (`uint.TryParse` HexNumber → `0x%08x (dec)`) | `ParseRegistryFile` `case "REG_DWORD"` |
| REG_QWORD decode (`hex(b):` bytes → `BitConverter.ToUInt64`, little-endian) | `ParseRegistryFile` `case "REG_QWORD"` |
| REG_EXPAND_SZ / REG_MULTI_SZ decode (`Encoding.Unicode`, `\0`→`\r`, TrimEnd) | `ParseRegistryFile` `case "REG_EXPAND_SZ"/"REG_MULTI_SZ"` |
| Deleted key `[-...]` / deleted value `"x"=-` | `ParseRegistryFile` `StartsWith("[-")` / `EndsWith("=-")` branches |
| **TreeView build** (nodes, de-dup, backward key walk) | `RegistryPreviewMainPage.Utilities.cs` `AddTextToTree`; node map `mapRegistryKeys` |
| Value grid model + tooltip/type text | `RegistryValue.xaml.cs`, `RegistryKey.xaml.cs`; `SetValueToolTip`, `GetFolderToolTip` |
| Extended data preview dialog (dispatch by type) | `RegistryPreviewMainPage.DataPreview.cs` `ShowExtendedDataPreview` |
| Hex/decimal view (DWORD/QWORD), binary HexBox, expand-var view | `DataPreview.cs` `AddHexView`, `AddBinaryView`, `AddExpandStringView` |
| HexBox custom control | `RegistryPreviewUILib/Controls/HexBox/*` (+ `Library/EndianConvert/*`) |
| Monaco raw-text editor | `RegistryPreviewUILib/Controls/MonacoEditor/*` |
| Save edited `.reg` (UTF-16 write) | `RegistryPreviewMainPage.Utilities.cs` `SaveFile` (`Encoding.Unicode`) |
| **Write to registry / merge → launches regedit (UAC)** | `RegistryPreviewMainPage.Utilities.cs` `OpenRegistryEditor` (`regedit.exe`, `UseShellExecute = true`) |
| Window placement persistence (JSON) | `RegistryPreview/MainWindow.Utilities.cs` `OpenWindowPlacementFile`, `SaveWindowPlacementFile` |
| App/window bootstrap | `RegistryPreview/RegistryPreviewXAML/App.xaml.cs`, `MainWindow.xaml.cs`, `MainWindow.Events.cs` |
| Fuzz harness for parser helpers | `RegistryPreview.FuzzTests/FuzzTests.cs` |
| Telemetry (editor start/finish) | `RegistryPreview/Telemetry/RegistryPreviewEditorStart*Event.cs` |

**Parser pipeline (critical order):** `ParseRegistryFile` normalizes `\r\n`→`\r`, splits on `\r`,
validates the header, then walks lines: a `[...]` line is a **key** (→ `AddTextToTree`); a `"..."=`
line is a **value** whose type is set by prefix, then decoded; `;` starts a comment; blank/other
lines fall through. A value line ending in `,\` **continues onto the next line** before decoding.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### REG_MULTI_SZ / REG_EXPAND_SZ line breaks lost
- **Symptom:** a multi-string value renders as one run-together line; embedded string breaks vanish.
- **Where:** `ParseRegistryFile` `case "REG_EXPAND_SZ"/"REG_MULTI_SZ"`.
- **Root cause:** hex bytes are decoded with `Encoding.Unicode.GetString`, but the NUL separators
  (`\0`) between strings weren't turned into visible breaks.
- **Guardrail:** after decode, `value.Replace('\0', '\r').TrimEnd('\r')` — convert NUL separators to
  CR and drop the trailing terminator. Keep this when touching multi-string decode. Evidence:
  issue [#36629](https://github.com/microsoft/PowerToys/issues/36629) (fix is cited in-source at
  `case "REG_MULTI_SZ"`).

### Value type incorrectly identified
- **Symptom:** a value's type/rendering is wrong (e.g. a short/odd hex payload mis-decoded).
- **Where:** `ParseRegistryFile` prefix detection + `case "REG_BINARY"/"REG_QWORD"/…` decode.
- **Root cause:** each comma-separated hex chunk must be exactly two digits; type text and decode
  must agree. Chunks are parsed as `c.Length == 2 ? byte.Parse(...) : throw` → any non-2-digit chunk
  makes the **whole value** an `ERROR` (`InvalidBinary`).
- **Guardrail:** preserve the 2-hex-digit invariant and keep type detection and decode branch in
  sync; a new prefix needs a matching `case`. Evidence:
  issue [#30713](https://github.com/microsoft/PowerToys/issues/30713).

### HKEY abbreviations rejected as invalid
- **Symptom:** keys using `HKLM`/`HKCU`/`HKCR`/`HKU`/`HKCC` are flagged with the error icon though
  `reg.exe` accepts them.
- **Where:** `ParseHelper.cs::CheckForKnownGoodBranches`.
- **Root cause:** validation originally only allowed the five long root names.
- **Guardrail:** the allow-list must include both the long roots **and** the abbreviations, each in
  bare (`[HKLM]`) and sub-path (`[HKLM\...`) forms; add any new alias to all three groups. Evidence:
  issue [#31562](https://github.com/microsoft/PowerToys/issues/31562) (fixed via PR #31552).

### Multi-line hex continuation broken
- **Symptom:** long REG_BINARY/QWORD/MULTI_SZ values split across lines with trailing `\` decode
  wrong or truncate.
- **Where:** `ParseRegistryFile` `while (value.EndsWith(@",\"))` continuation loop, plus the two
  pre-checks that pad a lone `\` to `,\`.
- **Root cause:** each continuation line must be comment-stripped and left-trimmed before being
  concatenated; a bare `\` right after the type marker must be normalized so the loop triggers.
- **Guardrail:** keep `ScanAndRemoveComments` + `TrimStart` inside the continuation loop, and keep
  the `if (value == @"\") value = @",\";` padding for QWORD/BINARY/EXPAND_SZ/MULTI_SZ. Never assume a
  hex value fits on one line.

### "The REG file editor could not be opened" / launch failures
- **Symptom:** clicking Edit or merging shows the UAC/launch error dialog even on valid files.
- **Where:** `RegistryPreviewMainPage.Utilities.cs` `OpenRegistryEditor` (`regedit.exe`,
  `UseShellExecute = true`) wrapped in try/catch → `UACDialogError`.
- **Root cause:** external-process launch depends on the "Default app"/edit target and shell state;
  failures (declined UAC, missing target) surface as the generic error.
- **Guardrail:** keep the launch in try/catch and surface the localized dialog; don't assume the
  process starts. Evidence: issues
  [#36920](https://github.com/microsoft/PowerToys/issues/36920),
  [#34269](https://github.com/microsoft/PowerToys/issues/34269).

### Window state not restored / closes on open
- **Symptom:** window opens minimized/at wrong size, forgets maximized state, or the app closes when
  it should restore.
- **Where:** `RegistryPreview/MainWindow.Utilities.cs` window-placement JSON load/save;
  `MainWindow.Events.cs`.
- **Root cause:** placement JSON is best-effort (`{ }` fallback on any parse/IO error) and easily
  loses sync with actual window state.
- **Guardrail:** persist real window state on close and validate the JSON round-trips. Evidence:
  issues [#46573](https://github.com/microsoft/PowerToys/issues/46573),
  [#36630](https://github.com/microsoft/PowerToys/issues/36630).

### Comment (`;`) handling opens URLs / eats data
- **Symptom:** a commented-out URL is still actionable (Ctrl-click opens a browser); or a `;` inside
  a quoted string is wrongly treated as a comment.
- **Where:** `ScanAndRemoveComments` (strips from first `;`); REG_SZ path deliberately skips comment
  scanning; value parsing takes the **last** `"` to avoid trailing comments.
- **Root cause:** `;` is only a comment outside quoted string values; the Monaco editor separately
  makes URLs clickable.
- **Guardrail:** only strip comments for non-string types (REG_SZ/ERROR are excluded from
  `ScanAndRemoveComments`); don't make commented text actionable. Evidence:
  issue [#37447](https://github.com/microsoft/PowerToys/issues/37447).

## Review Rules

Enforce these when reviewing or authoring RegistryPreview changes:

- **Keep type detection and decode in lock-step.** Every value-type prefix (`dword:`, `hex:`,
  `hex(b):`, `hex(2):`, `hex(7):`, `hex(0):`) must have a matching decode `case`; a new prefix
  without a `case` silently falls through to the `default` (raw string). See `ParseRegistryFile`.
- **Preserve the 2-hex-digit byte invariant.** Binary/QWORD/EXPAND/MULTI decode requires each
  comma-separated chunk to be exactly two hex digits; malformed input must set `Type = "ERROR"` with
  the localized message, never throw to the UI (#30713).
- **Decode REG_MULTI_SZ/EXPAND with `Encoding.Unicode` and translate `\0`→`\r`.** Registry
  wide-strings are UTF-16LE; NUL separators must become visible breaks (#36629).
- **QWORD is little-endian 8 bytes.** Use `BitConverter.ToUInt64` over the parsed bytes; don't
  hand-roll endianness (`case "REG_QWORD"`).
- **Validate the root branch against the allow-list.** New root aliases go into
  `CheckForKnownGoodBranches` in all three forms (long, abbrev; bare and sub-path) (#31562).
- **Never trust external-process launch.** `OpenRegistryEditor` must stay in try/catch and surface
  the localized UAC/error dialog; writing to the registry runs `regedit.exe` elevated via the shell
  (#36920).
- **Save `.reg` as UTF-16.** `SaveFile` writes with `System.Text.Encoding.Unicode`; don't switch to
  UTF-8 — Registry Editor expects Unicode for v5 files.
- **Harden helpers against arbitrary bytes.** `ParseHelper` methods are fuzzed
  (`RegistryPreview.FuzzTests`); changes to `CheckKeyLineForBrackets`, `StripFirstAndLast`,
  `StripEscapedCharacters`, `ProcessRegistryLine` must not throw on adversarial input — guard lengths
  (e.g. `StripFirstAndLast` requires `Length > 1`).
- **Ship a test with parser changes.** Add/extend `RegistryPreview.FuzzTests` or unit coverage for
  any new parsing branch.

## Pitfalls

- **Never** decode a wide-string registry type as ASCII/UTF-8 — REG_MULTI_SZ/REG_EXPAND_SZ are
  UTF-16LE (`Encoding.Unicode`); and you must map `\0`→`\r` or all line breaks vanish (#36629).
- **Never** let a malformed hex chunk throw into the UI — any chunk whose length ≠ 2 must flip the
  value to `Type = "ERROR"` with the localized `InvalidBinary/Dword/Qword/String` text (#30713).
- **Never** assume a hex value is on one line — a trailing `,\` (or a lone `\` after the type
  marker) means "continue on the next line"; comment-strip and left-trim each continuation.
- **Never** add a value-type prefix without a matching decode `case` — it falls through to raw
  string rendering with the wrong type label.
- **HKEY abbreviations are valid input** (`HKLM`, `HKCU`, `HKCR`, `HKU`, `HKCC`) — the allow-list
  must accept them or valid files show the error icon (#31562).
- **Comments (`;`) only outside quoted strings** — REG_SZ deliberately skips comment scanning, and
  value parsing keeps the last `"` to avoid clipping a trailing comment; don't make commented URLs
  clickable (#37447).
- **`@=` is the `(Default)` value** and `@=-` deletes/clears it — both are rewritten to
  `"(Default)"=...` before the normal value path (`ProcessRegistryLine`).
- **Tree de-dup walks keys backwards** and must use `LastIndexOf(@"\name")`, not `Replace` — a plain
  `Replace` collapses repeated key-name segments and corrupts the tree (see comment in `AddTextToTree`).
- **Writing to the registry is an elevated, irreversible action** — `OpenRegistryEditor` shells
  `regedit.exe` which prompts for UAC and merges into the live registry; treat as destructive.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + parser reference.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a RegistryPreview PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/registrypreview/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/registrypreview)
- [.reg file format / Registry Editor](https://learn.microsoft.com/en-us/troubleshoot/windows-server/performance/windows-registry-advanced-users#registry-editor) · [UTF-16 REG_MULTI_SZ](https://learn.microsoft.com/en-us/windows/win32/sysinfo/registry-value-types) · [ShellExecute / UAC elevation](https://learn.microsoft.com/en-us/windows/win32/api/shellapi/nf-shellapi-shellexecutew)
