# Bug Localization Answer: PreviewPane - Markdown preview crash on UTF-8 files >2MB and <1.5M characters

## CULPRIT_FILES
1. `src/modules/previewpane/MarkdownPreviewHandler/MarkdownPreviewHandlerControl.cs` (line 186)

## CULPRIT_FUNCTIONS
- `MarkdownPreviewHandlerControl.DoPreview` (method around lines 180-196)

## CULPRIT_FUNCTIONS
- The size check logic in the DoPreview method

## FIX
The bug is in line 186 where the check `if (markdownHTML.Length > 1_500_000)` uses string character length (UTF-16 code units) instead of UTF-8 byte count. WebView2's `NavigateToString` limit is ~1.5 MB measured in UTF-8 bytes. Multi-byte Unicode characters (e.g., CJK) cause the byte count to exceed the limit while the character count stays under. The fix changes the check to `if (System.Text.Encoding.UTF8.GetByteCount(markdownHTML) > 1_500_000)`.

## CITED_FIX_PR
#47391

## CONFIDENCE
high

## USED_SKILL
The PreviewPane knowledge skill's regression playbook "WebView2 `NavigateToString` byte-vs-character limit (crash on large/CJK files)" identified the exact symptom: crash on ~2 MB files with <1.5M characters. The skill cited PR #47391 and documented the root cause: the guard used `string.Length` (UTF-16 units) but the API limit is UTF-8 bytes. Examining MarkdownPreviewHandlerControl.cs line 186 confirmed the buggy character-count check.
