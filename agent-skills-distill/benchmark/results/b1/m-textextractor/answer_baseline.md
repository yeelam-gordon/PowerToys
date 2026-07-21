CULPRIT_FILES: src/modules/powerocr/PowerOCR/Helpers/ImageMethods.cs
CULPRIT_FUNCTIONS: PadImage, GetRegionAsBitmap
FIX: PadImage sometimes returns the input bitmap unchanged, sometimes creates a new padded bitmap. When called as `bmp = PadImage(bmp)`, the original bitmap is leaked if a new one is created. Refactor to use an out parameter returning bool to indicate if a new bitmap was created, allowing the caller to dispose the original bitmap only when necessary.
CITED_FIX_PR: none
CONFIDENCE: high
