# RegistryPreview PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces.

## General (any RegistryPreview PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] Parser changes covered by `RegistryPreview.FuzzTests` (or unit coverage); no throw-to-UI on bad input.
- [ ] Localized error strings used for invalid values (`InvalidBinary/Dword/Qword/String`, `ZeroLength`).

## `.reg` parsing (`ParseRegistryFile`, `ParseHelper.cs`)
- [ ] Every value-type prefix has a matching decode `case`; no silent fall-through to raw string.
- [ ] 2-hex-digit-per-byte invariant preserved; malformed chunk → `Type = "ERROR"`, not exception.
- [ ] REG_MULTI_SZ/EXPAND decoded with `Encoding.Unicode`; `\0`→`\r`, trailing terminator trimmed.
- [ ] REG_QWORD uses `BitConverter.ToUInt64` (little-endian); DWORD via `uint.TryParse` HexNumber.
- [ ] Multi-line continuation (`,\`) loop keeps `ScanAndRemoveComments` + `TrimStart`; lone `\` padded.
- [ ] REG_SZ escape validation intact (only `\"` and `\\` allowed); `StripEscapedCharacters` applied.
- [ ] Header validation (`REGEDIT4` / v5.00) unchanged; invalid file → message box, not crash.
- [ ] `@=` / `@=-` still rewritten to `(Default)` before the value path.

## Root / key validation (`CheckForKnownGoodBranches`, `AddTextToTree`)
- [ ] New root aliases added in all forms (long + abbrev; bare `[HKLM]` and sub-path `[HKLM\...`).
- [ ] Tree dedup uses `LastIndexOf(@"\name")`, not `Replace` (avoids collapsing repeated segments).
- [ ] Deleted key `[-...]` and deleted value `"x"=-` still get their special icons/handling.

## Data preview (`RegistryPreviewMainPage.DataPreview.cs`, `Controls/HexBox`)
- [ ] Dialog dispatch by type matches parser types (DWORD/QWORD → hex+dec; BINARY/NONE → HexBox; etc.).
- [ ] HexBox endianness/format correct; binary text view filters to printable bytes.

## Save / write-to-registry (`SaveFile`, `OpenRegistryEditor`)
- [ ] `.reg` saved as `Encoding.Unicode` (UTF-16), not UTF-8.
- [ ] `regedit.exe` launch stays in try/catch; declined UAC / missing target → localized dialog.
- [ ] No silent write to the live registry; the merge/elevation path is treated as destructive.

## Window / lifecycle (`MainWindow.Utilities.cs`, `MainWindow.Events.cs`)
- [ ] Window placement JSON round-trips; real state persisted on close (maximized/size).
- [ ] Dirty-close flow (`HandleDirtyClosing`) doesn't crash on Save/Discard/Cancel.

## Build / SDK bumps
- [ ] After WinAppSDK/CppWinRT bump: Monaco editor + HexBox render; PRI/resources load.
- [ ] Project references use `$(RepoRoot)`; deps in `Directory.Packages.props`.
