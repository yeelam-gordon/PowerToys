CULPRIT_FILES: src/modules/AdvancedPaste/AdvancedPasteModuleInterface/dllmain.cpp
CULPRIT_FUNCTIONS: send_copy_selection
FIX: Release activation hotkey modifiers (Ctrl/Win/Shift/Alt) via try_inject_modifier_key_up before sending Ctrl+C, then restore them via try_inject_modifier_key_restore. Also verify clipboard sequence number changed after Ctrl+C to confirm copy succeeded (treat unchanged sequence as failure and log warning).
CITED_FIX_PR: #46486
CONFIDENCE: high
USED_SKILL: The skill's "Auto-copy fails on Electron/Chromium apps" playbook directly identified the root cause (Ctrl+C without releasing modifiers + no success verification) and cited the fix PR. The module map guided me to dllmain.cpp::send_copy_selection.
