CULPRIT_FILES: src/modules/LightSwitch/LightSwitchService/LightSwitchService.cpp, src/modules/LightSwitch/LightSwitchService/LightSwitchStateManager.cpp
CULPRIT_FUNCTIONS: LightSwitchService worker thread initialization, LightSwitchStateManager::SyncInitialThemeState(), LightSwitchStateManager::OnTick()
FIX: The startup logic calls SyncInitialThemeState() followed immediately by OnTick(nowMinutes), but OnTick() may re-evaluate and override the initial sync. Fix by ensuring OnTick() respects the initial synchronized state or delay the first OnTick() evaluation.
CITED_FIX_PR: none
CONFIDENCE: medium
