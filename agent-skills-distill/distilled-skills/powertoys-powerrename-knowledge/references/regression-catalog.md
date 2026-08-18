# PowerRename Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each
claim in source before acting. Symptoms map to `src/modules/powerrename/`.

## Key Decisions (context for the playbooks)

- **Dual regex engine, selectable at runtime.** `Replace` dispatches via
  `RegexReplaceDispatch[_useBoostLib]` (`PowerRenameRegEx.cpp`), choosing `boost::wregex` or
  `std::wregex` from `GetUseBoostLib()`. Validate matching under both — #44202 / #45385.
- **Normalize once at the boundary.** Search term, replace term, and each source name pass through
  `SanitizeAndNormalize` (NFC + control-char strip) before comparison
  ([PR #43972](https://github.com/microsoft/PowerToys/pull/43972)). Normalization is a matching-layer
  concern, not a UI concern.
- **Deepest-first rename ordering.** `s_fileOpWorkerThread` computes `maxDepth`, buckets items into a
  depth matrix, then renames greatest-depth → 0 (`PowerRenameManager.cpp`). Correctness invariant.
- **Win11 context menu = sparse MSIX + runtime registration.** `enable()` registers
  `PowerRenameContextMenuPackage.msix` only on Win11 and only if not already registered for the
  current version; `disable()` unregisters. Root of the "menu missing/duplicated" bug class.
- **Photo-metadata via WIC, graceful degradation.**
  [PR #41728](https://github.com/microsoft/PowerToys/pull/41728) introduced `WICMetadataExtractor`;
  [PR #44466](https://github.com/microsoft/PowerToys/pull/44466) extended it to HEIF/HEIC/AVIF by
  detecting container format and picking the right WIC path, degrading cleanly when the Windows Store
  image extension is absent.
- **Hybrid CRT reverted for shutdown safety.**
  [PR #42073](https://github.com/microsoft/PowerToys/pull/42073) adopted Hybrid CRT to shrink bundle
  size; reverted in [PR #43484](https://github.com/microsoft/PowerToys/pull/43484) because PowerToys
  could not quit safely. Binary-size wins do not outrank clean teardown.

## Regression Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| Unicode/NBSP | Search matches nothing for visually identical name; crash on some CJK | `PowerRenameRegEx.cpp::SanitizeAndNormalize` | Raw UTF-16 compare (NFD vs NFC, NBSP) | NFC-normalize source + search term; fix buffer sizing | [#43971](https://github.com/microsoft/PowerToys/issues/43971), [#42653](https://github.com/microsoft/PowerToys/issues/42653), [PR #43972](https://github.com/microsoft/PowerToys/pull/43972), [PR #44944](https://github.com/microsoft/PowerToys/pull/44944) |
| Token collision | `$YYYYABC` drops token; date token + capital fails | `Helpers.cpp::GetDatedFileName` (~:457) | Short date token shadows longer metadata token | Negative lookahead `\$D(?!(ATE_TAKEN_...))`, `\$H(?!(EIGHT))` | [#44202](https://github.com/microsoft/PowerToys/issues/44202), [PR #44267](https://github.com/microsoft/PowerToys/pull/44267) |
| Counter | Enumeration counter stalls when result == original | `PowerRenameRegEx.cpp::Replace` `shouldIncrementCounter`; `Enumerating.cpp` | Increment tied to name-change | Decouple increment from name-change (regex_search per item) | [PR #42006](https://github.com/microsoft/PowerToys/pull/42006), [#41950](https://github.com/microsoft/PowerToys/issues/41950), [#39731](https://github.com/microsoft/PowerToys/issues/39731) |
| Regex engine | Pattern works in one engine, breaks in other | `PowerRenameRegEx.cpp` `RegexReplaceDispatch[_useBoostLib]` | std vs boost ECMAScript semantics | Test both `_useBoostLib` values | [#45385](https://github.com/microsoft/PowerToys/issues/45385), [#44942](https://github.com/microsoft/PowerToys/issues/44942), [PR #44944](https://github.com/microsoft/PowerToys/pull/44944) |
| Context menu | Entry missing / duplicated after upgrade/GPO/3rd-party shell | `dll/dllmain.cpp` enable/disable/UpdateRegistration; `dll/RuntimeRegistration.h`; `dll/PowerRenameExt.cpp` | Non-idempotent, not GPO-aware registration | Idempotent register/unregister; honor GPO at construction | [#48951](https://github.com/microsoft/PowerToys/issues/48951), [#38425](https://github.com/microsoft/PowerToys/issues/38425), [#44381](https://github.com/microsoft/PowerToys/issues/44381), [#48696](https://github.com/microsoft/PowerToys/issues/48696), [#39218](https://github.com/microsoft/PowerToys/issues/39218), [#43746](https://github.com/microsoft/PowerToys/issues/43746), [PR #41411](https://github.com/microsoft/PowerToys/pull/41411) |
| File-time UB | Undefined behavior in file-time compare | `PowerRenameRegEx.cpp` `PutFileTime` | Reading unwritten union member | Use `CompareFileTime`; write-then-read same member | [#42843](https://github.com/microsoft/PowerToys/issues/42843), [PR #42845](https://github.com/microsoft/PowerToys/pull/42845) |
| WinAppSDK/PRI | Crash / strings fail after SDK bump | `MainWindow.xaml.cpp` string loads; project PRI gen | WinAppSDK 1.8 dropped auto-PRI for unpackaged apps | Re-import MSIX SDK build tools; smoke-test editor | [PR #42300](https://github.com/microsoft/PowerToys/pull/42300), [#41723](https://github.com/microsoft/PowerToys/pull/41723), [#44146](https://github.com/microsoft/PowerToys/pull/44146) |
| Metadata (HEIF/AVIF) | Metadata tokens empty for HEIC/AVIF | `WICMetadataExtractor.cpp`; `PowerRenameViewModel.cs` install commands | Missing container handling / Store extension | Detect format, degrade gracefully; install-extension commands (not `async void`) | [PR #44466](https://github.com/microsoft/PowerToys/pull/44466) |
| Docs drift | Random tokens documented without `r` prefix | `Randomizer.cpp` `parseRandomizerOptions` (~:7-10) | UI/docs out of sync with engine regexes | Keep hint text, docs, regexes in sync | [#44526](https://github.com/microsoft/PowerToys/issues/44526) |
| MAX_PATH | Truncation/overflow on long paths | `GetDatedFileName`, `GetMetadataFileName`, `Replace` | Fixed `wchar_t[MAX_PATH]` scratch buffers | Use bounded `StringCch*`; reason about ~260 ceiling | [#44555](https://github.com/microsoft/PowerToys/issues/44555) |
| Invalid targets | Produces Win32-reserved names | `Renaming.cpp::DoRename` / name validation | No reserved-name check | Validate before enabling Apply | [#39623](https://github.com/microsoft/PowerToys/issues/39623) |

## Common Practices (enforced in review)

- **Locale-aware date formatting.** `GetDatedFileName` uses `GetUserDefaultLocaleName` +
  `GetDateFormatEx`, falling back to `en_US`. Don't hardcode month/day strings.
- **`$TOKEN` namespace collisions.** All replacers share the `(([^\$]|^)(\$\$)*)` escape prefix;
  overlapping prefixes require negative lookahead (#44267).
- **Concurrency.** `CPowerRenameRegEx::Replace` takes `CSRWSharedAutoLock`; keep long file ops on
  `s_fileOpWorkerThread`, off the UI thread; don't touch shared regex state unlocked.
- **Packaging/build.** Never reorder `Microsoft.Cpp.*.props` imports; use `$(RepoRoot)` not relative
  paths; central package management in `Directory.Packages.props` (#44639, #43920).
- **Testing.** Every regression above shipped with tests in `src/modules/powerrename/unittests`
  (`HelpersTests`, `CommonRegExTests`, `WICMetadataExtractorTests`). Add cases per engine.

---
*Corpus: 24 merged PRs, 155 review comments, 60 bug issues + source verification against
`src/modules/powerrename`.*
