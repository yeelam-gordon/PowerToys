CULPRIT_FILES: src/modules/cmdpal/Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs
CULPRIT_FUNCTIONS: ShellPage_OnKeyDown
FIX: Gate the ActivateSelectedListItemMessage (sent on Enter key) and ActivateSecondaryCommandMessage (sent on Ctrl+Enter) on whether the palette is expanded or collapsed. When compact mode is collapsed, these actions should not fire since the list is hidden and no item is visibly selected.
CITED_FIX_PR: #49182
CONFIDENCE: high
USED_SKILL: The skill's "Compact/collapsed mode interaction bugs" playbook directly identified the root cause (actions fire on hidden selected items while collapsed; input handling not gated on collapsed state) and cited the fix PR. The module map pointed me to ShellPage.xaml.cs where the OnKeyDown handler lacks collapsed-state checks.
