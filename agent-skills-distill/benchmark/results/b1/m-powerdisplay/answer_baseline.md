CULPRIT_FILES: src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashRecovery.cs, src/modules/powerdisplay/PowerDisplay/PowerDisplayXAML/App.xaml.cs
CULPRIT_FUNCTIONS: DetectOrphanAndDisable (CrashRecovery.cs), OnLaunched (App.xaml.cs)
FIX: The crash detection mechanism treats any orphan discovery.lock file as evidence of a crash. On normal/cooperative shutdown, the lock should be removed, but if it isn't (e.g., due to timing or rapid shutdown), the next startup incorrectly triggers auto-disable. The fix needs to distinguish between actual crashes and clean shutdowns, possibly by writing a shutdown marker before removing the lock or checking process exit status.
CITED_FIX_PR: none
CONFIDENCE: high
