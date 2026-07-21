CULPRIT_FILES: src/modules/alwaysontop/AlwaysOnTop/AlwaysOnTop.cpp
CULPRIT_FUNCTIONS: AlwaysOnTop::UpdateSystemMenuItem
FIX: The function checks if the menu item exists before inserting but doesn't properly handle edge cases where duplicates might exist. The fix should remove any existing items with the command ID before inserting a new one, or ensure that the check and insert are atomic. Specifically, before calling InsertMenuItemW, call RemoveMenu to ensure no duplicates remain from previous insertions.
CITED_FIX_PR: none
CONFIDENCE: high
