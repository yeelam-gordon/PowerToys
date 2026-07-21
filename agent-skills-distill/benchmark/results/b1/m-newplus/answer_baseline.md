CULPRIT_FILES: src/modules/NewPlus/NewShellExtensionContextMenu/template_item.cpp
CULPRIT_FUNCTIONS: remove_starting_digits_from_filename()
FIX: When a filename contains only numbers (e.g., "123"), find_first_not_of() returns npos, which equals the string size, causing erase(0, size) to remove the entire filename. Fix by checking if find_first_not_of() returns npos before erasing, and if so, leave the filename unchanged or handle numeric-only filenames specially.
CITED_FIX_PR: none
CONFIDENCE: high
