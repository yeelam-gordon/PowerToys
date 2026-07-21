CULPRIT_FILES: src/modules/MouseUtils/MouseHighlighter/MouseHighlighter.cpp
CULPRIT_FUNCTIONS: MouseHookProc (WM_LBUTTONDOWN, WM_RBUTTONDOWN, WM_LBUTTONUP, WM_RBUTTONUP cases)
FIX: The hook immediately spawns a drawing point on button-down and triggers fading on button-up, causing a double ripple for quick clicks. The fix adds hold-detection timers (HOLD_RIPPLE_THRESHOLD_MS = 180ms) that arm on button-down and spawn the persistent ripple only after the threshold; quick clicks that release before the threshold emit exactly one self-contained ripple instead.
CITED_FIX_PR: #48232
CONFIDENCE: high
USED_SKILL: R7 regression playbook from mouseutils-knowledge identified the double-ripple root cause, confirmed by verifying MouseHookProc lacks hold-timer logic in the worktree.
