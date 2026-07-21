CULPRIT_FILES: src/modules/cmdpal/Microsoft.CmdPal.UI/ExtViews/ListItemsView.xaml.cs
CULPRIT_FUNCTIONS: Items_ItemClick
FIX: The Items_ItemClick handler executes item actions (lines 183, 189) without checking if the list is visible/expanded. In compact mode when the list is collapsed (showing only the search box), item clicks should not execute actions. The fix should add a visibility or collapsed state check before executing InvokeItemCommand to prevent actions from firing when the list is not expanded.
CITED_FIX_PR: none
CONFIDENCE: medium
