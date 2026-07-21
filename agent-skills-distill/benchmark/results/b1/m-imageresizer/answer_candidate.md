CULPRIT_FILES: src/modules/imageresizer/ui/Properties/Settings.cs

CULPRIT_FUNCTIONS: Settings class - missing InitializeWatcher method and FileSystemWatcher instance

FIX: Add a FileSystemWatcher to monitor changes to settings.json, implement InitializeWatcher() to set up the watcher with debouncing, and call Reload() on the UI dispatcher when file changes are detected. The watcher should be created and initialized when the Settings.Default singleton is accessed, monitoring the settings.json file path with appropriate filters.

CITED_FIX_PR: #45266

CONFIDENCE: high

USED_SKILL: The skill's Module Map explicitly lists "Live settings reload (debounced FileSystemWatcher → UI dispatcher)" as implemented via Settings.cs InitializeWatcher, Reload, ReloadCore (PR #45266). The current code has Reload() and ReloadCore() but no FileSystemWatcher initialization, confirming the missing feature.
