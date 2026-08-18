# PowerRename Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / inputs:**
- **OS / build / Win10 vs Win11:**
- **Regex mode? engine setting (boost)?:**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Search matches nothing though names look identical | `PowerRenameRegEx.cpp::SanitizeAndNormalize` | Unicode NFC/NBSP | Unicode/NBSP |
| Crash on certain (e.g. CJK) input | `PowerRenameRegEx.cpp::SanitizeAndNormalize` (buffer sizing) | Unicode | Unicode/NBSP |
| `$YYYYABC` / date token before capital drops token | `Helpers.cpp::GetDatedFileName` | Token collision | Token collision |
| Metadata token empty (HEIC/AVIF) | `WICMetadataExtractor.cpp`; Store extension install | Metadata/WIC | Metadata |
| Counter doesn't advance | `PowerRenameRegEx.cpp::Replace` `shouldIncrementCounter`; `Enumerating.cpp` | Counter | Counter |
| Pattern broke after toggling engine | `PowerRenameRegEx.cpp` `RegexReplaceDispatch[_useBoostLib]` | Regex engine | Regex engine |
| Context menu missing / duplicated | `dll/dllmain.cpp` enable/disable/UpdateRegistration; `RuntimeRegistration.h`; `PowerRenameExt.cpp` | Registration lifecycle | Context menu |
| Wrong file-time used / odd sort | `PowerRenameItem.cpp::GetTime`; `PowerRenameRegEx.cpp::PutFileTime` | File-time UB | File-time |
| Crash on launch after update | `MainWindow.xaml.cpp` string loads; PRI generation | WinAppSDK/PRI | WinAppSDK bump |
| Produces reserved/invalid name | `Renaming.cpp::DoRename` / name validation | Invalid targets | (Gotchas) |
| Random token literal `stringalnum` not working | `Randomizer.cpp::parseRandomizerOptions` | Docs drift (`r` prefix) | (Gotchas) |
| Excluded files/folders still renamed | `Renaming.cpp::DoRename` exclude flags | Filter flags | Module Map |
| Parent renamed before child / broken nested paths | `PowerRenameManager.cpp` `s_fileOpWorkerThread` | Depth ordering | Review Rules |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
3. Reproduce with the reporter's inputs (note regex mode + engine setting).
4. Add/extend a unit test in `src/modules/powerrename/unittests` before fixing.
