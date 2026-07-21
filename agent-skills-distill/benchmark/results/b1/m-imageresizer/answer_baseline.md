CULPRIT_FILES: src/modules/imageresizer/ui/Properties/Settings.cs
CULPRIT_FUNCTIONS: Reload(), Settings constructor
FIX: Add a FileSystemWatcher to monitor the settings JSON file for changes and automatically call Reload() when the file is modified. The Reload() method already exists but is never invoked automatically when settings change externally.
CITED_FIX_PR: none
CONFIDENCE: high
