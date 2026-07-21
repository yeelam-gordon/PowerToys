CULPRIT_FILES: src/modules/NewPlus/NewShellExtensionContextMenu/template_item.cpp

CULPRIT_FUNCTIONS: remove_starting_digits_from_filename

FIX: Add a guard to prevent stripping numeric-only filenames that would result in empty strings. The current implementation strips all leading digits and then all leading spaces/dots, but fails when the entire filename is just numbers (e.g., "001231" or "001231.txt"). Add logic to check if stripping would leave an empty string or would remove the extension from a file whose stem is only digits, and preserve the original name in those cases. Also ensure ALL consecutive spaces/dots are stripped after the digit run, not just one.

CITED_FIX_PR: #45439

CONFIDENCE: high

USED_SKILL: The skill's Regression Playbook for "Leading-digit stripping" (PR #45439) states the issue: "folders or files whose stem is only digits (001231, 001231.txt) get mangled; 01..Name / 01 . Name keep a stray leading ./space." The guardrail requires "keep the numeric-only-stem guard AND skip ALL consecutive ./space after the digit run."
