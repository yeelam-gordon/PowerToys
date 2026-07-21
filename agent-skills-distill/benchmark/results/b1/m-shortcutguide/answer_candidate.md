CULPRIT_FILES: ShortcutGuide.Ui/ShortcutGuideXAML/MainWindow.xaml.cs

CULPRIT_FUNCTIONS: WindowSelector_SelectionChanged, SetWindowPosition

FIX: The code has a reentrant activation chain where `App.TaskBarWindow.Activate()` triggers `Window_Activated → BringToFront → TaskbarWindow.Activated` that momentarily leaves `App.TaskBarWindow.AppWindow` null. When `SetWindowPosition()` tries to access it (line 250) without null checking, it throws. The exception escapes and closes the overlay. Fix requires: (1) null-guard `App.TaskBarWindow?.AppWindow` in SetWindowPosition before dereferencing, and (2) wrap WindowSelector_SelectionChanged and SetWindowPosition in try/catch that logs instead of crashing.

CITED_FIX_PR: #48481

CONFIDENCE: high

USED_SKILL: The skill's regression catalog identified the exact crash pattern (issue #48448) with PR #48481, documenting the reentrant activation bug and the need for null guards plus defensive exception handling in navigation.
