CULPRIT_FILES: src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuideXAML/Pages/ShortcutsPage.xaml.cs
CULPRIT_FUNCTIONS: OnNavigatedTo, OnNavigatedFrom, OnPinnedShortcutsChanged (event subscription/unsubscription pattern)
FIX: The event subscription pattern allows race conditions during rapid navigation. The OnPinnedShortcutsChanged callback can fire when the page is in an inconsistent state (after OnNavigatedFrom but before full disposal). Add null checks in the callback or ensure the event handler only processes events when the page state is valid (_shortcutFile != null).
CITED_FIX_PR: #48448
CONFIDENCE: high
