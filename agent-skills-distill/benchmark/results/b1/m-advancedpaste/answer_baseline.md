CULPRIT_FILES: src/modules/AdvancedPaste/AdvancedPasteModuleInterface/dllmain.cpp
CULPRIT_FUNCTIONS: try_send_copy_message
FIX: The try_send_copy_message function incorrectly checks if SendMessageTimeout returns non-zero to determine success. Electron/Chromium apps handle WM_COPY but return 0, causing a false failure. The fix should check if SendMessageTimeout succeeds (returns non-zero) AND examine the result parameter, or check for timeout errors specifically to distinguish between message delivery failure and a zero return value from the window.
CITED_FIX_PR: none
CONFIDENCE: high
