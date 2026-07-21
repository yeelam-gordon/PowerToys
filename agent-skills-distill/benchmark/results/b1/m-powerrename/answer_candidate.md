# Bug Localization Answer: PowerRename - Unicode characters and non-breaking spaces not being matched

## CULPRIT_FILES
1. `src/modules/powerrename/lib/PowerRenameRegEx.cpp` (Replace method, lines ~415-560)

## CULPRIT_FUNCTIONS
- `CPowerRenameRegEx::Replace` (method around lines 400-600)

## FIX
The Replace function uses source filenames directly without Unicode normalization (NFC) or non-breaking space (NBSP) sanitization. The fix requires implementing a `SanitizeAndNormalize` function that applies NFC normalization (via Win32 `NormalizeString(NormalizationC, ...)`) to both the source filename and the search/replace terms before comparison. This ensures that NFD-composed filenames (e.g., from macOS) and filenames containing non-breaking spaces match correctly.

## CITED_FIX_PR
#43972

## CONFIDENCE
high

## USED_SKILL
The PowerRename knowledge skill's regression playbook "Unicode / NBSP mismatch (NFD vs NFC)" identified this as a dominant issue pattern where search terms appear identical but don't match due to missing normalization. The catalog cited issues #43971, #42653, and fix PR #43972 with hardening in #44944. By examining PowerRenameRegEx.cpp's Replace method (lines 415-560), I confirmed that `sourceToUse`, `searchTerm`, and `replaceTerm` are used directly without calling any normalization function.
