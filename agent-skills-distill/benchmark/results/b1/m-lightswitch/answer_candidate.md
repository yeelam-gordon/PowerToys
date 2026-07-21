CULPRIT_FILES: src/modules/LightSwitch/LightSwitchService/LightSwitchStateManager.cpp

CULPRIT_FUNCTIONS: SyncInitialThemeState

FIX: SyncInitialThemeState should not only read and cache the current theme state, but also call EvaluateAndApplyIfNeeded() to apply the correct scheduled theme immediately on startup. The current implementation only syncs state without evaluating whether the theme should be changed based on the active schedule.

CITED_FIX_PR: #45304

CONFIDENCE: high

USED_SKILL: The skill's Regression Playbook for "Theme reverts on Windows restart" (issue #45291, PR #45304) describes the exact problem: "startup did not re-evaluate + apply the correct theme against current settings; a single init function was overloaded for two purposes." The guardrail states "on startup, sync cached system/apps/Night-Light state AND call EvaluateAndApplyIfNeeded."
