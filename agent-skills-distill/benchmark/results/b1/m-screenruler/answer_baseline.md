CULPRIT_FILES: src/modules/MeasureTool/MeasureToolCore/BGRATextureView.h
CULPRIT_FUNCTIONS: MappedTextureView::MappedTextureView (constructor)
FIX: The constructor casts `resource.pData` directly to `const uint32_t*` without ensuring proper alignment. On architectures with strict alignment requirements (ARM64), accessing unaligned uint32_t pointers causes crashes. Add alignment checks or ensure pData is properly aligned before the cast.
CITED_FIX_PR: none
CONFIDENCE: high
