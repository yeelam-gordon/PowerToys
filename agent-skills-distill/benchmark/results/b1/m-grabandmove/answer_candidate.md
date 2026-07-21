CULPRIT_FILES: src/modules/GrabAndMove/GrabAndMove/main.cpp
CULPRIT_FUNCTIONS: KeyboardProc
FIX: When Alt is absorbed and another key is pressed without a drag, the code clears g_altAbsorbed and replays Alt but leaves g_altPressed=true, making the system think Alt is still down. The fix adds `g_altPressed = false;` before the ReplayAbsorbedAlt() call at line 805 to ensure internal state matches reality.
CITED_FIX_PR: #47261
CONFIDENCE: high
USED_SKILL: Regression catalog's "Stuck modifier (absorbed Alt)" entry identified the exact branch at line 803 where g_altPressed must be cleared before replaying the modifier.
