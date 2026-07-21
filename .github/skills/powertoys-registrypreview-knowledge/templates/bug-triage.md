# RegistryPreview Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro `.reg` snippet / inputs:**
- **PowerToys version / install method:**
- **Running as admin? "Default app" toggle on?:**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Multi-string value runs together / breaks lost | `ParseRegistryFile` `case "REG_MULTI_SZ"/"REG_EXPAND_SZ"` | Unicode `\0`→`\r` | MULTI_SZ line breaks |
| Value shows wrong type / mis-decoded | `ParseRegistryFile` prefix chain + decode `case` | Type/decode mismatch | Value type |
| Valid key shown with error icon | `ParseHelper.cs::CheckForKnownGoodBranches` | Root allow-list | HKEY abbreviations |
| `HKLM`/`HKCU`/… rejected | `ParseHelper.cs::CheckForKnownGoodBranches` | Abbrev allow-list | HKEY abbreviations |
| Long hex value truncated/garbled | `ParseRegistryFile` `while (value.EndsWith(@",\"))` | Multi-line continuation | Hex continuation |
| Binary/QWORD "Invalid…" on valid data | `case "REG_BINARY"/"REG_QWORD"` (2-digit chunk parse) | Byte invariant | Value type |
| "REG file editor could not be opened" | `OpenRegistryEditor` (`regedit.exe`, ShellExecute) | External launch/UAC | Editor launch |
| Crash on open/close, save dialog crash | `MainWindow.Events.cs`; `HandleDirtyClosing`; `SaveFile` | Lifecycle | (Review Rules) |
| Window not restored / wrong size / closes on open | `MainWindow.Utilities.cs` placement JSON | Window state | Window state |
| Commented (`;`) URL still opens browser | `ScanAndRemoveComments`; Monaco link handling | Comment scope | Comments |
| `(Default)` value wrong / `@=-` mishandled | `ParseHelper.cs::ProcessRegistryLine` | Default value | (Pitfalls) |
| Tree drawn wrong / duplicated segments | `AddTextToTree` (`LastIndexOf`, not `Replace`) | Tree de-dup | (Pitfalls) |
| Hex/decimal/binary preview wrong | `RegistryPreviewMainPage.DataPreview.cs` `ShowExtendedDataPreview` | Data preview | Module Map |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues in the Regression Catalog for prior fixes/guardrails.
3. Reproduce with the reporter's exact `.reg` bytes (headers, case, continuation `\`, encoding).
4. Add/extend coverage in `RegistryPreview.FuzzTests` before fixing parser code.
