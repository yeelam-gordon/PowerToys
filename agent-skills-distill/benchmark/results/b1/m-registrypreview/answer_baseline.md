CULPRIT_FILES: src/modules/registrypreview/RegistryPreviewUI/MainWindow.Utilities.cs
CULPRIT_FUNCTIONS: ParseRegistryFile (REG_DWORD validation section)
FIX: The DWORD validation accepts any hex string parseable as uint without checking length. DWORD values in .reg files must be exactly 8 hex characters. Add validation to check `value.Length == 8` before parsing to reject malformed DWORD values like "1" or "fffffffff".
CITED_FIX_PR: none
CONFIDENCE: high
