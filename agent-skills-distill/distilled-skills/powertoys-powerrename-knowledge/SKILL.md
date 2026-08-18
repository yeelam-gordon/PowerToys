---
name: powertoys-powerrename-knowledge
description: 'PowerToys PowerRename module knowledge: feature->file/function map, recurring regression playbooks (Unicode NFC/NBSP normalization, date & photo-metadata token collisions, enumeration counter, dual std/boost regex engines, Win10 verb + Win11 sparse-MSIX + GPO context-menu registration), maintainer review rules, and gotchas. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/powerrename — search/replace, regex, case transforms, date/metadata tokens, counters, randomizers, context menu, settings, WinUI editor. Keywords: PowerRename, bulk rename, regex, boost, WIC EXIF, context menu, MSIX, GPO, normalization, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys PowerRename Knowledge

Grounded engineering knowledge for the PowerToys **PowerRename** module — a bulk/advanced file
renamer that plugs into the Windows Explorer context menu (search/replace plain or regex, case
transforms, date/time & photo-metadata tokens, counters, randomizers, applied across nested
selections). Use it to localize code fast, avoid known regression traps, and enforce the
conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/powerrename/` and needing prior art.
- Fixing/triaging a PowerRename bug: search/replace not matching, tokens misbehaving, counter not
  advancing, context menu missing/duplicated, crash on startup, metadata not extracted.
- Reviewing a PowerRename PR and checking it against maintainer conventions and regression traps.
- Adding a new `$TOKEN`, a case transform, a metadata field, or touching the regex/normalization core.
- Working on context-menu registration (Win10 verb, Win11 sparse MSIX, GPO gating).

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Win11 context-menu entry (sparse pkg) | `PowerRenameContextMenu/dllmain.cpp`; runtime (un)registration in `dll/RuntimeRegistration.h` via `PowerRenameRuntimeRegistration::EnsureRegistered/Unregister` |
| Enable/disable, GPO gate, sparse-package registration | `dll/dllmain.cpp` `PowerRenameModule::enable/disable/init_settings`, `gpo_policy_enabled_configuration`, `UpdateRegistration` |
| Legacy classic-menu COM handler (Win10 verb) | `dll/PowerRenameExt.cpp` |
| Search / replace core (plain + regex, case-insensitive, match-all) | `lib/PowerRenameRegEx.cpp` `CPowerRenameRegEx::Replace`, `_Find`, `RegexReplaceEx` |
| Dual regex engine (std::wregex ↔ boost::wregex) | `PowerRenameRegEx.cpp` `RegexReplaceDispatch`, `_useBoostLib = GetUseBoostLib()` |
| Unicode sanitize + NFC normalization | `PowerRenameRegEx.cpp::SanitizeAndNormalize` (Win32 `NormalizeString(NormalizationC,…)`) |
| Counter / enumeration `${start=,increment=,padding=}` | `lib/Enumerating.cpp` `parseEnumOptions`, `Enumerator::printTo`; applied in `Replace`; increment gated by `shouldIncrementCounter` |
| Random-string tokens `rstringalnum/rstringalpha/rstringdigit/ruuidv4` | `lib/Randomizer.cpp` `parseRandomizerOptions`, `RandomizerOptions::randomize` |
| Case transforms (UPPER/lower/Title/Capitalize, name/ext scope) | `lib/Helpers.cpp` `GetTransformedFileName` |
| Date/time tokens `$YYYY $MM $DD $hh $mm $ss $fff $TT` | `Helpers.cpp::GetDatedFileName`; usage detection `isFileTimeUsed` |
| Photo-metadata tokens `$DATE_TAKEN_* $CAMERA_* $WIDTH $HEIGHT $DESCRIPTION` | `Helpers.cpp::GetMetadataFileName` + `lib/MetadataPatternExtractor.cpp`, `lib/MetadataFormatHelper.cpp` |
| WIC EXIF/XMP extraction (JPEG/HEIF/HEIC/AVIF) | `lib/WICMetadataExtractor.cpp`; cache `lib/MetadataResultCache.cpp` |
| Rename orchestration, depth-ordered apply, worker thread | `lib/PowerRenameManager.cpp` `s_fileOpWorkerThread` (deepest-depth-first via `maxDepth` matrix) |
| Per-item rename glue (transform → new name → set) | `lib/Renaming.cpp` `DoRename` |
| Include/exclude filters (files/folders/subfolder content) | flags `ExcludeFiles=0x10 / ExcludeFolders=0x20 / ExcludeSubfolders=0x40` in `lib/PowerRenameInterfaces.h`; enforced in `Renaming.cpp::DoRename` |
| File-time source (created/modified/accessed) | flags in `PowerRenameInterfaces.h`; resolved by `lib/PowerRenameItem.cpp` `CPowerRenameItem::GetTime` (CreateFileW + `GetFileTime`) |
| Item model + enumeration | `lib/PowerRenameItem.cpp`, `lib/PowerRenameEnum.cpp` |
| Search/replace MRU (autocomplete history) | `lib/PowerRenameMRU.cpp`, `lib/MRUListHandler.cpp` |
| Settings (flags, MRU enabled, boost toggle) | `lib/Settings.cpp`; UI VM `src/settings-ui/Settings.UI/ViewModels/PowerRenameViewModel.cs` |
| Main WinUI 3 editor window | `PowerRenameUILib/PowerRenameXAML/MainWindow.xaml.cpp`, bootstrap `App.xaml.cpp` |
| Preview list view-model / data source | `PowerRenameUILib/ExplorerItemViewModel.cpp`, `ExplorerItemsSource.cpp` |

**Token engine ordering (critical):** in `Replace`, transforms apply in a fixed pipeline —
date tokens (`GetDatedFileName`) → metadata tokens (`GetMetadataFileName`) → enumerator/randomizer
insertion → regex/plain search-replace. All token matchers share the `(([^\$]|^)(\$\$)*)` prefix,
so `$$` escapes a literal `$`.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Unicode / NBSP mismatch (NFD vs NFC)
- **Symptom:** search term looks identical to filename but no match — esp. macOS-created (NFD)
  files and web-downloaded names with non-breaking spaces; also crash on some CJK input.
- **Where:** `PowerRenameRegEx.cpp::SanitizeAndNormalize`, applied to search term, replace term, and each source name.
- **Root cause:** comparing raw UTF-16 without normalization.
- **Guardrail:** NFC-normalize source **and** search term before comparing; never compare raw UTF-16.
  Verify buffer sizing (2-pass `NormalizeString`). See [Unicode UAX #15](https://unicode.org/reports/tr15/).
  Evidence: issues [#43971](https://github.com/microsoft/PowerToys/issues/43971),
  [#42653](https://github.com/microsoft/PowerToys/issues/42653); fix
  [PR #43972](https://github.com/microsoft/PowerToys/pull/43972); hardened
  [PR #44944](https://github.com/microsoft/PowerToys/pull/44944).

### Date/metadata `$TOKEN` prefix collision
- **Symptom:** `$YYYYABC` dropped the token; a date token directly followed by a capital letter fails.
- **Where:** `Helpers.cpp::GetDatedFileName` (token regexes ~`Helpers.cpp:457`).
- **Root cause:** short date tokens collided with longer metadata tokens (`$D` vs `$DATE_TAKEN_`, `$H` vs `$HEIGHT`).
- **Guardrail:** any new `$TOKEN` must use negative lookahead against overlapping longer tokens
  (e.g. `\$D(?!(ATE_TAKEN_|ESCRIPTION|OCUMENT_ID))`, `\$H(?!(EIGHT))`). Evidence: issue
  [#44202](https://github.com/microsoft/PowerToys/issues/44202); fix
  [PR #44267](https://github.com/microsoft/PowerToys/pull/44267).

### Enumeration counter stalls
- **Symptom:** with enumeration + regex + a `${}` counter, the counter fails to advance when a
  rename result coincides with the original filename.
- **Where:** `PowerRenameRegEx.cpp::Replace`, counter increment via `shouldIncrementCounter`; parsing in `Enumerating.cpp`.
- **Root cause:** increment was tied to whether the name actually changed.
- **Guardrail:** decouple counter advance from name-change; increment per matched item
  (`regex_search`). Evidence: fix [PR #42006](https://github.com/microsoft/PowerToys/pull/42006);
  related open reports [#41950](https://github.com/microsoft/PowerToys/issues/41950),
  [#39731](https://github.com/microsoft/PowerToys/issues/39731).

### Dual std::regex vs Boost divergence
- **Symptom:** a previously-working regex pattern (group transpose, `$` at boundaries) breaks after
  toggling the engine setting.
- **Where:** `PowerRenameRegEx.cpp` `RegexReplaceDispatch[_useBoostLib]`.
- **Root cause:** `std::regex` and `boost::wregex` differ in ECMAScript semantics (backreferences, groups, anchors).
- **Guardrail:** validate any matching change under **both** `_useBoostLib` values; add a test for
  each engine. Evidence: issues [#45385](https://github.com/microsoft/PowerToys/issues/45385),
  [#44202](https://github.com/microsoft/PowerToys/issues/44202),
  [#44942](https://github.com/microsoft/PowerToys/issues/44942) (`$` boundary; tests in
  [PR #44944](https://github.com/microsoft/PowerToys/pull/44944)).

### Context menu missing or duplicated
- **Symptom:** "Rename with PowerRename" absent, or shown twice, after upgrade / GPO change /
  third-party shells (Nilesoft, Total Commander).
- **Where:** `dll/dllmain.cpp` `enable/disable/UpdateRegistration`, `dll/RuntimeRegistration.h`
  (Win11 sparse MSIX); `dll/PowerRenameExt.cpp` (Win10 classic verb).
- **Root cause:** sparse-package + runtime-registration lifecycle not idempotent / not GPO-aware.
- **Guardrail:** make register/unregister idempotent and honor GPO state at construction so registry
  entries are cleaned when policy-disabled. Evidence: issues
  [#48951](https://github.com/microsoft/PowerToys/issues/48951),
  [#38425](https://github.com/microsoft/PowerToys/issues/38425),
  [#44381](https://github.com/microsoft/PowerToys/issues/44381),
  [#48696](https://github.com/microsoft/PowerToys/issues/48696); fix
  [PR #41411](https://github.com/microsoft/PowerToys/pull/41411).

### WinAppSDK bump → missing PRI resources (crash on launch)
- **Symptom:** editor window crashes / strings fail to load after a WinAppSDK version bump.
- **Where:** `MainWindow.xaml.cpp` string loads; project PRI generation.
- **Root cause:** WinAppSDK 1.8 stopped auto-generating PRI files for unpackaged apps.
- **Guardrail:** re-import MSIX SDK build tools for standalone PRI generation; rebuild and
  smoke-test the editor after any SDK/CppWinRT bump. Evidence:
  [PR #42300](https://github.com/microsoft/PowerToys/pull/42300);
  [#41723](https://github.com/microsoft/PowerToys/pull/41723) /
  [#44146](https://github.com/microsoft/PowerToys/pull/44146).

## Review Rules

Enforce these when reviewing or authoring PowerRename changes:

- **Validate matching under both regex engines.** Any change to `Replace`/`_Find`/normalization must
  be tested with `_useBoostLib` true and false — engines diverge (#45385, #44202).
- **Normalize at the matching boundary, not the UI.** Route new match inputs through
  `SanitizeAndNormalize`; don't add ad-hoc normalization in view-models
  ([PR #43972](https://github.com/microsoft/PowerToys/pull/43972)).
- **Reject `async void`** in `PowerRenameViewModel.cs` install/refresh commands — use `async Task` or
  catch inside; `async void` drops exceptions and is untestable
  ([async best practices](https://learn.microsoft.com/en-us/archive/msdn-magazine/2013/march/async-await-best-practices-in-asynchronous-programming#avoid-async-void),
  [PR #44466](https://github.com/microsoft/PowerToys/pull/44466#discussion_r2663376392)).
- **Never read a union member you didn't write.** File-time compares must use
  [`CompareFileTime`](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-comparefiletime),
  not field-by-field `SYSTEMTIME`/`FILETIME` union access — caused UB
  [#42843](https://github.com/microsoft/PowerToys/issues/42843), fix
  [PR #42845](https://github.com/microsoft/PowerToys/pull/42845).
- **Confirm the COM apartment is STA and intentional** when touching `App.xaml.cpp::OnLaunched` or WIC
  calls; don't silently swallow `RPC_E_CHANGED_MODE`
  ([STA/MTA](https://learn.microsoft.com/en-us/windows/win32/com/single-threaded-apartments),
  [PR #41728](https://github.com/microsoft/PowerToys/pull/41728#discussion_r2458602377)).
- **Keep rename ordering deepest-depth-first.** `s_fileOpWorkerThread` must rename children before
  parents — this is a correctness invariant, not an optimization (`PowerRenameManager.cpp`).
- **No bare relative paths in project files.** Use `$(RepoRoot)`, not `..\..\..\`; add deps to
  `Directory.Packages.props`. ([#43920](https://github.com/microsoft/PowerToys/pull/43920),
  [#44639](https://github.com/microsoft/PowerToys/pull/44639)).
- **Ship a test with every fix.** Suites live in `src/modules/powerrename/unittests`
  (`HelpersTests`, `CommonRegExTests`, `WICMetadataExtractorTests`).

## Gotchas

- **Never** add a new `$TOKEN` without checking prefix collisions against existing tokens — short
  tokens (`$D`, `$H`) silently shadow longer metadata tokens; use negative lookahead (#44267).
- **Never** compare raw UTF-16 in matching — NFD (macOS) and NBSP names look identical but won't
  match; always NFC-normalize first (#43971).
- **Never** assume one regex engine — a pattern valid under Boost may break under `std::regex` and
  vice versa; the engine is a runtime setting (#45385).
- **Never** rename parents before children in the worker thread — it invalidates child paths.
- **Mind fixed `MAX_PATH` buffers** in `GetDatedFileName`/`GetMetadataFileName`/`Replace` — long
  paths and large replacements risk truncation/overflow (#44555); prefer bounded `StringCch*` APIs.
- **Keep UI hint text, docs, and `parseRandomizerOptions` regexes in sync** — random tokens are the
  `r`-prefixed forms (`rstringalnum`), not `stringalnum` (#44526).
- **PowerRename can emit Win32-reserved names** (`CON`, `PRN`, …) — validate candidate names before
  enabling Apply (#39623).
- **Binary-size wins don't outrank clean teardown** — Hybrid CRT was reverted because the process
  couldn't quit safely ([PR #43484](https://github.com/microsoft/PowerToys/pull/43484)).

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim this file's playbooks and then hunt the diff for those
themes — that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

When localizing a bug, if the symptom doesn't map cleanly to a row above, reason from the symptom
and verify in source — a thin/absent map entry can anchor you onto a confident, wrong file.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a PowerRename PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/powerrename/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/powerrename)
- [Unicode NFC / UAX #15](https://unicode.org/reports/tr15/) · [CompareFileTime](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-comparefiletime) · [STA/MTA](https://learn.microsoft.com/en-us/windows/win32/com/single-threaded-apartments)
