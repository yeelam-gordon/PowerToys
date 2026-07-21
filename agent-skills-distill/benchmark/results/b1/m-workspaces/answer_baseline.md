CULPRIT_FILES: src/modules/workspaces/WorkspacesCsharpLibrary/DrawHelper.cs
CULPRIT_FUNCTIONS: SaveBitmap
FIX: EncoderParameters object is created but never disposed, causing a resource leak. EncoderParameters implements IDisposable and must be wrapped in a using statement to ensure proper cleanup when saving workspace snapshot bitmaps. This prevents overlay drawing issues due to resource exhaustion.
CITED_FIX_PR: none
CONFIDENCE: high
