CULPRIT_FILES: src/modules/GrabAndMove/GrabAndMove/main.cpp

CULPRIT_FUNCTIONS: LowLevelKeyboardProc

FIX: When a non-Alt key is pressed while Alt was absorbed without a drag, the code resets g_altAbsorbed and replays Alt but forgets to reset g_altPressed to false. This leaves the Alt key logically "pressed" even after it's been released/replayed, preventing proper state cleanup. The fix is to also set g_altPressed = false when replaying the absorbed Alt key.

CITED_FIX_PR: none

CONFIDENCE: high
