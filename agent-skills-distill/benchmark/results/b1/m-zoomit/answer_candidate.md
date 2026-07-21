CULPRIT_FILES: ZoomIt/Zoomit.cpp

CULPRIT_FUNCTIONS: RegisterAllHotkeys (line 3357), OptionsProc validation (line 5465), MainWndProc WM_CREATE startup (line 7508), WM_USER_RELOAD_SETTINGS handler (line 10180)

FIX: The RECORD_WINDOW_HOTKEY is derived via XOR: `g_RecordToggleMod ^ MOD_ALT`. When the base modifier is exactly Alt-only (MOD_ALT), this XOR produces 0, registering a modifier-less hotkey that captures every bare keypress. The fix is to only register the window hotkey when the computed modifier is non-zero: `if ((g_RecordToggleMod ^ MOD_ALT) != 0)` before calling RegisterHotKey. This guard must be applied at all four registration sites.

CITED_FIX_PR: #47388

CONFIDENCE: high

USED_SKILL: The skill's XOR-derived hotkey regression playbook identified this exact collision pattern with PR #47388, documenting that Alt-only base modifiers XOR'd with MOD_ALT produce VK=0 registrations that capture unmodified keys.
