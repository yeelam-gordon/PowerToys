CULPRIT_FILES: src/modules/peek/Peek.UI/Helpers/FileExplorerHelper.cs
CULPRIT_FUNCTIONS: CaretVisible
FIX: The CaretVisible method checks if a focused window's class name contains "Edit" or "Input" to suppress Peek activation during typing. However, it doesn't catch in-place file rename scenarios in Explorer. The fix needs to detect rename-specific window classes or states to prevent Space key from triggering Peek during file rename operations.
CITED_FIX_PR: none
CONFIDENCE: high
