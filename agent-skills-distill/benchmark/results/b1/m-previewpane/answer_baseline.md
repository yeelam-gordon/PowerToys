CULPRIT_FILES: src/modules/previewpane/MarkdownPreviewHandler/MarkdownPreviewHandlerControl.cs
CULPRIT_FUNCTIONS: DoPreview
FIX: File.ReadAllText(filePath) at line 119 auto-detects encoding by reading the file's BOM or defaulting to UTF-8. For files >2MB in bytes but <1.5M characters (multi-byte UTF-8 sequences), the method may fail or allocate excessive memory trying to decode the entire file. The fix should either check file size before reading, use streaming for large files, or explicitly specify encoding with a size limit to prevent crashes on oversized UTF-8 files.
CITED_FIX_PR: none
CONFIDENCE: medium
