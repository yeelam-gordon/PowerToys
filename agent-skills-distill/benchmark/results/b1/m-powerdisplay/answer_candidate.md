# Bug Localization Answer: PowerDisplay - False-positive crash detection on cooperative shutdown

## CULPRIT_FILES
1. `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs` (entire class)

## CULPRIT_FUNCTIONS
- `CrashDetectionScope.Begin` (static method)
- `CrashDetectionScope.Dispose` (method)

## FIX
The crash detection system writes a `discovery.lock` file during risky DDC/CI operations and deletes it on normal completion. However, it lacks a safety-net for cooperative shutdown via `Environment.Exit(0)`. The fix requires subscribing to `AppDomain.CurrentDomain.ProcessExit` during `Begin()` and adding a handler that deletes the lock file, ensuring that a graceful shutdown doesn't leave an orphan lock that would trigger false-positive auto-disable on next startup.

## CITED_FIX_PR
#48173

## CONFIDENCE
high

## USED_SKILL
The PowerDisplay knowledge skill's regression playbook "False-positive crash detection on cooperative shutdown" (referenced in SKILL.md line mentioning PR #48173 and issue #48169) identified the exact problem: the missing `AppDomain.ProcessExit` safety-net. The skill documented that the lock must survive crashes/BSOD/TerminateProcess but should be deleted on cooperative `Environment.Exit`. Examining CrashDetectionScope.cs confirmed no ProcessExit handler exists.
