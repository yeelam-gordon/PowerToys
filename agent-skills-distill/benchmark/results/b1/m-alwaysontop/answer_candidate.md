CULPRIT_FILES: src/modules/alwaysontop/AlwaysOnTop/AlwaysOnTop.cpp
CULPRIT_FUNCTIONS: UpdateSystemMenuItem
FIX: Add an IsAlwaysOnTopMenuCommand function that verifies ownership via dwItemData (0x414F5450 tag) before updating or removing the system menu item. When a menu item at the fixed command ID (0xEFE0) already exists, check if it's ours via the tag; if not ours, skip and log to avoid collisions with other apps' menu items.
CITED_FIX_PR: #45845
CONFIDENCE: high
USED_SKILL: The skill's "System-menu command-ID collision / duplicate item" playbook precisely identified the root cause (reusing a fixed ID without ownership verification) and cited the fix PR. The module map pointed me to UpdateSystemMenuItem in AlwaysOnTop.cpp.
