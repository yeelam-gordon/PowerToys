CULPRIT_FILES: src/modules/PowerOCR/PowerOCR/Helpers/ImageMethods.cs
CULPRIT_FUNCTIONS: PadImage, GetRegionAsBitmap (and any other callers like GetWindowBoundsImage)
FIX: PadImage currently returns either the original or a new padded bitmap, causing callers to leak the original when reassigning without disposal. Refactor to a TryPad pattern with bool return and out parameter with [NotNullWhen(true)], returning false when already large enough (no allocation) and true when padding was needed, allowing callers to dispose the original only when a new bitmap was created.
CITED_FIX_PR: #44906
CONFIDENCE: high
USED_SKILL: Regression catalog from textextractor-knowledge explicitly documents PR #44906 refactoring PadImage to bool+out+[NotNullWhen(true)] to fix GDI+ dispose ordering, verified current code leaks bitmaps in GetRegionAsBitmap.
