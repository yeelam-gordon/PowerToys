CULPRIT_FILES: src/modules/peek/Peek.UI/PeekXAML/MainWindow.xaml.cs
CULPRIT_FUNCTIONS: Initialize
FIX: When CurrentItem is null (typing in rename/search), the error handling code shows an error window and calls this.Show() and BringToForeground, which steals focus during file rename operations. The fix removes the error window display and restores the silent early return to avoid interfering with user input.
CITED_FIX_PR: #44995
CONFIDENCE: high
USED_SKILL: Regression catalog identified the Space-during-rename symptom and pinpointed that PR #44703 added the focus-stealing error window in the Initialize method's CurrentItem==null branch.
