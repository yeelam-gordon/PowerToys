CULPRIT_FILES: src/modules/powerrename/lib/Helpers.cpp
CULPRIT_FUNCTIONS: GetTrimmedFileName, GetTransformedFileName
FIX: The iswspace() function used for trimming whitespace in GetTrimmedFileName (lines 75, 79) does not recognize non-breaking spaces (U+00A0) as whitespace in all locales. Similarly, word boundary detection using iswspace() throughout GetTransformedFileName fails on non-breaking spaces and other Unicode whitespace characters. The fix should explicitly check for U+00A0 and other Unicode whitespace characters or use a more comprehensive Unicode-aware whitespace detection.
CITED_FIX_PR: none
CONFIDENCE: high
