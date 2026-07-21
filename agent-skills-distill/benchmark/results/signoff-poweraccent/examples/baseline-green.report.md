# Sign-off Report — PowerAccent (Quick Accent)

- **Gate:** ✅ PASS (all P0 checks must PASS)
- **Generated:** 2026-07-05T07:53:01.811988+00:00
- **Target:** `{"app": "PowerAccent (Quick Accent)", "exe": "C:\\s\\powertoys\\x64\\Release\\WinUI3Apps\\PowerToys.PowerAccent.exe", "common_dll": "C:\\s\\powertoys\\x64\\Release\\tests\\PowerAccent.Common.UnitTests\\PowerToys.PowerAccent.Common.UnitTests.dll", "core_dll": "C:\\s\\powertoys\\x64\\Release\\tests\\PowerAccent.Core.UnitTests\\PowerToys.PowerAccent.Core.UnitTests.dll"}`
- **Totals:** 20/20 passed, 0 failed

## Results by priority

| Priority | Passed | Failed | Total |
|----------|--------|--------|-------|
| P0 | 5 | 0 | 5 |
| P1 | 11 | 0 | 11 |
| P2 | 4 | 0 | 4 |

## P0 checks

### ✅ `candidates-populated` — Holding an accent-capable letter yields a non-empty candidate list; unmapped keys / no languages yield nothing (the data the overlay renders).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetCharacters_AllLanguages_ReturnsNonEmptyForCommonKey` | ✅ | GetCharacters_AllLanguages_ReturnsNonEmptyForCommonKey=Passed |
| 2 | mstest | `GetCharacters_UnmappedKey_ReturnsEmpty` | ✅ | GetCharacters_UnmappedKey_ReturnsEmpty=Passed |
| 3 | mstest | `GetCharacters_EmptyLanguages_ReturnsEmpty` | ✅ | GetCharacters_EmptyLanguages_ReturnsEmpty=Passed |

### ✅ `glyph-fr-a-exact` — French 'a' overlay shows exactly [a-grave, a-circumflex, a-acute, a-diaeresis, a-tilde, ae] in order.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | glyph | `glyph-fr-a-exact` | ✅ | expected=[à,â,á,ä,ã,æ] actual=[à,â,á,ä,ã,æ] |

### ✅ `glyph-fr-e-exact` — French 'e' overlay shows exactly [e-acute, e-grave, e-circumflex, e-diaeresis, euro] in order.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | glyph | `glyph-fr-e-exact` | ✅ | expected=[é,è,ê,ë,€] actual=[é,è,ê,ë,€] |

### ✅ `glyph-fr-c-exact` — French 'c' overlay shows exactly [c-cedilla].

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | glyph | `glyph-fr-c-exact` | ✅ | expected=[ç] actual=[ç] |

### ✅ `lifecycle-launch-enable` — Enabling Quick Accent launches PowerToys.PowerAccent.exe, which stays resident and materializes its settings (module initialized).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | launch | `PowerToys.PowerAccent.exe` | ✅ | pid=24284 alive=True |
| 2 | assert | `settings.json materialized` | ✅ | exists=True path=C:\Users\yeelam\AppData\Local\Microsoft\PowerToys\QuickAccent\settings.json |

## P1 checks

### ✅ `candidates-dedup` — When multiple selected languages map the same glyph, the overlay shows it once (no duplicate candidates).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetCharacters_DeduplicatesCharactersAcrossLanguages` | ✅ | GetCharacters_DeduplicatesCharactersAcrossLanguages=Passed |

### ✅ `candidates-ordering` — Candidates are ordered by language DisplayOrder; a single selected language yields only that language's glyphs.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetCharacters_SortsOutput_AccordingToDisplayOrder` | ✅ | GetCharacters_SortsOutput_AccordingToDisplayOrder=Passed |
| 2 | mstest | `GetCharacters_SingleLanguage_ReturnsOnlyThatLanguagesCharacters` | ✅ | GetCharacters_SingleLanguage_ReturnsOnlyThatLanguagesCharacters=Passed |

### ✅ `charset-all-expansion` — 'ALL' language setting expands to every language exactly once and returns a stable cached union.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetCharacters_AllLanguagesCachedResult_IsConsistent` | ✅ | GetCharacters_AllLanguagesCachedResult_IsConsistent=Passed |
| 2 | mstest | `All_ContainsEveryLanguageEnumValue_ExactlyOnce` | ✅ | All_ContainsEveryLanguageEnumValue_ExactlyOnce=Passed |

### ✅ `enum-lockstep-native-managed` — The managed LetterKey enum stays in lockstep (names + values) with the native WinRT keyboard-hook enum, so the right key triggers the right candidates.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `ManagedLetterKey_MatchesWinRtLetterKey_AllNamesPresent` | ✅ | ManagedLetterKey_MatchesWinRtLetterKey_AllNamesPresent=Passed |
| 2 | mstest | `ManagedLetterKey_MatchesWinRtLetterKey_ValuesMatch` | ✅ | ManagedLetterKey_MatchesWinRtLetterKey_ValuesMatch=Passed |

### ✅ `language-metadata-integrity` — Every language entry is well-formed (non-empty id, non-null glyphs, present in display/group order maps) so the language picker and overlay never break.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `All_EveryEntry_HasNonEmptyIdentifier` | ✅ | All_EveryEntry_HasNonEmptyIdentifier=Passed |
| 2 | mstest | `All_EveryEntry_HasNonNullCharacters` | ✅ | All_EveryEntry_HasNonNullCharacters=Passed |
| 3 | mstest | `All_Characters_ContainsNoNullOrEmptyEntries` | ✅ | All_Characters_ContainsNoNullOrEmptyEntries=Passed |
| 4 | mstest | `All_EveryEntry_ExistsInDisplayOrder` | ✅ | All_EveryEntry_ExistsInDisplayOrder=Passed |
| 5 | mstest | `All_EveryLanguageGroupValue_IsUsedAtLeastOnce` | ✅ | All_EveryLanguageGroupValue_IsUsedAtLeastOnce=Passed |
| 6 | mstest | `DisplayOrder_ContainsEveryLanguageEnumValue_ExactlyOnce` | ✅ | DisplayOrder_ContainsEveryLanguageEnumValue_ExactlyOnce=Passed |
| 7 | mstest | `GroupDisplayOrder_ContainsEveryLanguageGroupValue_ExactlyOnce` | ✅ | GroupDisplayOrder_ContainsEveryLanguageGroupValue_ExactlyOnce=Passed |
| 8 | mstest | `LanguageLookup_ContainsEveryLanguageEnumValue` | ✅ | LanguageLookup_ContainsEveryLanguageEnumValue=Passed |

### ✅ `language-alphabetical` — Spoken languages are listed alphabetically by display name in the settings language picker.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `DisplayOrder_SpokenLanguages_AreSortedAlphabeticallyByDisplayName` | ✅ | DisplayOrder_SpokenLanguages_AreSortedAlphabeticallyByDisplayName=Passed |

### ✅ `unknown-language-throws` — An unknown/unsupported language identifier fails fast instead of returning silent-wrong candidates.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetCharacters_UnknownLanguage_ThrowsKeyNotFoundException` | ✅ | GetCharacters_UnknownLanguage_ThrowsKeyNotFoundException=Passed |

### ✅ `glyph-cur-e-euro` — Currency-only language maps 'e' to exactly the euro sign.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | glyph | `glyph-cur-e-euro` | ✅ | expected=[€] actual=[€] |

### ✅ `glyph-all-a-contains-common` — With all languages enabled, 'a' candidates include the common Latin accents (a-grave/acute/circumflex/diaeresis/tilde).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | glyph | `glyph-all-a-contains-common` | ✅ | all present in [à,á,â,ä,å,æ,α,ά,ã,שׂ,שׁ,ְ,ą,ā,ǎ,ɑ,ɑ̄,ɑ́,ɑ̌,ɑ̀,ª,ă,ả,ạ,ằ,ẳ,ẵ,ắ,ặ,ầ,ẩ,ẫ,ấ,ậ,ἀ,ἁ,ὰ,ᾶ,ᾱ,ᾰ,ἂ,ἃ,ἄ,ἅ,ἆ,ἇ,ᾳ,ᾀ,ᾁ,ᾴ,ᾲ,ᾷ,ᾄ,ᾅ,ᾂ,ᾃ,ᾆ,ᾇ,ɒ,ɐ,ȧ,ǽ,∀,ᵃ,ₐ] |

### ✅ `lifecycle-single-instance` — A second Quick Accent instance detects the 'QuickAccent' mutex and exits, leaving exactly one resident owner of the keyboard hook.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | precondition | `first instance resident` | ✅ | first_pid=24284 alive=True |
| 2 | assert | `second instance self-exits (mutex)` | ✅ | second_pid=19572 exited=True code=0 |
| 3 | assert | `first instance still resident` | ✅ | first alive=True |

### ✅ `lifecycle-clean-exit` — Disabling Quick Accent signals POWERACCENT_EXIT_EVENT and the process shuts down cleanly (uninstalls the global hook).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | precondition | `instance resident` | ✅ | alive=True |
| 2 | signal | `POWERACCENT_EXIT_EVENT` | ✅ | OpenEvent+SetEvent ok=True |
| 3 | assert | `process exits on event` | ✅ | exit_code=0 exited=True |

## P2 checks

### ✅ `positioning-dpi1` — At 100% DPI the overlay anchors (TopLeft..BottomRight/Center) land at the expected screen coordinates.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetRawCoordinatesFromPosition_AtDpi1_PlacesEachAnchor` | ✅ | GetRawCoordinatesFromPosition_AtDpi1_PlacesEachAnchor (Right,1696,514)=Passed, GetRawCoordinatesFromPosition_AtDpi1_PlacesEachAnchor (Top,860,24)=Passed, Get... |

### ✅ `positioning-dpi-scaling` — At 150% DPI the overlay footprint scales so it stays on-screen at high-DPI.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetRawCoordinatesFromPosition_AtDpi150Percent_ScalesWindowFootprint` | ✅ | GetRawCoordinatesFromPosition_AtDpi150Percent_ScalesWindowFootprint (TopLeft,24,24)=Passed, GetRawCoordinatesFromPosition_AtDpi150Percent_ScalesWindowFootpri... |

### ✅ `positioning-multimonitor` — The overlay honors the active monitor's origin (including offset and negative-origin monitors).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetRawCoordinatesFromPosition_OnOffsetMonitor_HonorsScreenOrigin` | ✅ | GetRawCoordinatesFromPosition_OnOffsetMonitor_HonorsScreenOrigin (BottomRight,4056,1312)=Passed, GetRawCoordinatesFromPosition_OnOffsetMonitor_HonorsScreenOr... |
| 2 | mstest | `GetRawCoordinatesFromPosition_OnNegativeOriginMonitor_OffsetsFromScreenEdge` | ✅ | GetRawCoordinatesFromPosition_OnNegativeOriginMonitor_OffsetsFromScreenEdge=Passed |

### ✅ `caret-placement` — In caret-follow mode the overlay centers above the caret, clamps at screen edges, and flips below when there's no room above.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `GetRawCoordinatesFromCaret_WithRoom_CentersAboveCaret` | ✅ | GetRawCoordinatesFromCaret_WithRoom_CentersAboveCaret=Passed |
| 2 | mstest | `GetRawCoordinatesFromCaret_NearLeftEdge_ClampsToScreenLeft` | ✅ | GetRawCoordinatesFromCaret_NearLeftEdge_ClampsToScreenLeft=Passed |
| 3 | mstest | `GetRawCoordinatesFromCaret_NearRightEdge_ClampsToScreenRight` | ✅ | GetRawCoordinatesFromCaret_NearRightEdge_ClampsToScreenRight=Passed |
| 4 | mstest | `GetRawCoordinatesFromCaret_NoRoomAbove_FlipsBelowCaret` | ✅ | GetRawCoordinatesFromCaret_NoRoomAbove_FlipsBelowCaret=Passed |
