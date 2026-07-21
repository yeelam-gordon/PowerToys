CULPRIT_FILES: RegistryPreviewUI/MainWindow.Utilities.cs

CULPRIT_FUNCTIONS: CheckForKnownGoodBranches

FIX: The validation function only checks for long-form HKEY root names (e.g., HKEY_LOCAL_MACHINE) but does not accept the standard abbreviations (HKLM, HKCU, HKCR, HKU, HKCC) that reg.exe and regedit support. The function needs to add checks for abbreviated forms in both bare ([HKLM]) and sub-path ([HKLM\...]) formats to properly validate all valid registry keys.

CITED_FIX_PR: #31552

CONFIDENCE: high

USED_SKILL: The skill's regression catalog identified issue #31562 (PR #31552) documenting that HKEY abbreviations were rejected as invalid, and the Module Map pointed directly to CheckForKnownGoodBranches as the validation function.
