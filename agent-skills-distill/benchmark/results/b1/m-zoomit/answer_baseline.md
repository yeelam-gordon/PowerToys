CULPRIT_FILES: src/modules/ZoomIt/ZoomIt/Zoomit.cpp
CULPRIT_FUNCTIONS: registerHotkey lambda (lines 3357, 7508, 10180) and RegisterHotKey calls (lines 3357, 5465, 7508, 10180)
FIX: When registering RECORD_WINDOW_HOTKEY, the code uses XOR (^) to toggle MOD_ALT: `(g_RecordToggleMod ^ MOD_ALT)`. If Alt is the only modifier (g_RecordToggleMod = MOD_ALT), XOR produces 0, removing the modifier entirely. The fix is to use OR (|) instead of XOR, or check if modifiers exist before XORing, so Alt is properly added rather than toggled off.
CITED_FIX_PR: none
CONFIDENCE: high
