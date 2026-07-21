CULPRIT_FILES: src/settings-ui/Settings.UI/SettingsXAML/Controls/TitleBar/TitleBar.xaml

CULPRIT_FUNCTIONS: TitleBar template (PART_TitleText TextBlock)

FIX: The TitleBar XAML template sets MinWidth="48" on the Title and Subtitle TextBlocks without checking if the text is empty. When the Title property is empty or null, this creates a 48-pixel wide empty space that causes issues with the drag region. The fix is to add a Visibility converter that collapses the TextBlock when the text is empty or set MinWidth="0".

CITED_FIX_PR: none

CONFIDENCE: high
