# PowerRename PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
links to the Regression Playbook / Review Rule it enforces.

## General (any PowerRename PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] A unit test accompanies each behavior change (`src/modules/powerrename/unittests`).
- [ ] No bare relative paths in `.vcxproj`; uses `$(RepoRoot)`; deps in `Directory.Packages.props`.

## Search / replace / regex (`PowerRenameRegEx.cpp`)
- [ ] Change validated under **both** `_useBoostLib` = true and false; a test exists per engine.
- [ ] Match inputs normalized via `SanitizeAndNormalize` (NFC); no raw UTF-16 compare.
- [ ] `NormalizeString` buffer sizing correct (2-pass); no truncation of long strings.
- [ ] Shared regex state accessed under `CSRWSharedAutoLock`.

## Tokens (date / metadata / random / counter)
- [ ] New `$TOKEN` checked for prefix collision; negative lookahead added if it shadows a longer token.
- [ ] Token pipeline order preserved: date → metadata → enumerator/randomizer → search-replace.
- [ ] `$$` still escapes a literal `$`.
- [ ] Counter increment decoupled from name-change (`shouldIncrementCounter`).
- [ ] Random tokens use `r`-prefixed forms; UI hint text + docs + `parseRandomizerOptions` in sync.
- [ ] Date formatting stays locale-aware (`GetDateFormatEx`); no hardcoded month/day strings.
- [ ] Fixed `MAX_PATH` buffers respected; bounded `StringCch*` used for large replacements.

## Metadata / WIC (`WICMetadataExtractor.cpp`, `PowerRenameViewModel.cs`)
- [ ] New container formats degrade gracefully when Store image extension absent.
- [ ] No `async void` install/refresh commands — use `async Task` or catch inside.
- [ ] COM apartment is STA and intentional; `RPC_E_CHANGED_MODE` not silently swallowed.

## Rename orchestration (`PowerRenameManager.cpp`, `Renaming.cpp`)
- [ ] Deepest-depth-first ordering preserved (children before parents).
- [ ] Item-count scaling considered for large selections (avoid needless double traversal).
- [ ] Exclude flags (`ExcludeFiles/Folders/Subfolders`) honored in `DoRename`.
- [ ] Candidate names validated (no Win32-reserved names like `CON`, `PRN`).

## File-time handling (`PowerRenameItem.cpp::GetTime`, `PowerRenameRegEx.cpp::PutFileTime`)
- [ ] File-time compares use `CompareFileTime`; no reading of unwritten union members.

## Context menu / registration (`dll/dllmain.cpp`, `RuntimeRegistration.h`, `PowerRenameExt.cpp`)
- [ ] register/unregister idempotent (no missing/duplicated entry on upgrade).
- [ ] GPO state honored at construction; registry cleaned when policy-disabled.
- [ ] Win10 verb and Win11 sparse-MSIX paths both considered.

## Build / SDK bumps
- [ ] After WinAppSDK/CppWinRT bump: PRI generation intact; editor window smoke-tested.
- [ ] `Microsoft.Cpp.*.props` import order unchanged.
- [ ] Shutdown/teardown still clean (no size-vs-safety regressions like Hybrid CRT).
