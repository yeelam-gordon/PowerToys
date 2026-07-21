# PowerToys Run Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
This module is plugin-heavy — first decide whether the behavior is owned by the **core host**
(`PowerLauncher`/`Wox.*`) or a specific **plugin** (`Plugins/...`). If the symptom doesn't map
cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / query typed:**
- **OS / build / Win10 vs Win11:**
- **Which plugin / result type? (calculator, shell `>`, program, folder, window walker, uri...):**
- **Enabled alongside Command Palette? Activation hotkey involved?:**

## Symptom → likely location

| Reported symptom | Start here (file · symbol) | Likely class | Playbook |
|---|---|---|---|
| Crash on launch / "ran into an issue" / `0x80263001` | `PowerLauncher/Helper/ExceptionHelper.cs`; `ErrorReporting.cs` | DWM composition (recoverable) | DWM composition crash |
| Results missing / stale / flicker while typing | `MainViewModel.cs` `QueryResults`/`UpdateResultView` (`_updateToken`) | Cancellation/threading | (Key Decisions) |
| Expected result ranked too low | `Wox.Plugin/Result.cs` (`Score`,`SelectedCount`); `StringMatcher.cs` | Ranking | Ranking (catalog) |
| A plugin doesn't load / "Plugin Loading Error" | `PluginManager.cs` `InitializePlugins`; `PluginLoadContext.cs` | Plugin isolation | Plugin loading |
| Matcher crash / wrong match on odd input | `Microsoft.Plugin.WindowWalker/Components/FuzzyMatching.cs`; `StringMatcher.cs` | Empty/edge input | Fuzzy matcher crash |
| Shell command with `"` misfires / injects | `Microsoft.Plugin.Shell/Main.cs` `EscapeCmdArgument`/`EscapePowerShellArgument` | Command injection | Shell quote breakout |
| Calculator wrong result (`log`, `pow`, decimals) | `Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs` `Interpret` | Mages/culture | Calculator parsing |
| VS Code recent workspaces missing / UNC | `Community...VSCodeWorkspaces/VSCodeHelper/VSCodeInstances.cs`; `WorkspacesHelper/VSCodeWorkspacesApi.cs` | Storage discovery | VS Code Workspaces |
| Files not returning expected results | `Microsoft.Plugin.Indexer/` (Windows Search) | Index/search | (catalog) |
| Program not found / won't launch | `Microsoft.Plugin.Program/Programs/Win32Program.cs`, `UWP.cs` | Enumeration | Module Map |
| Win+Space doesn't activate Run (CmdPal enabled) | `MainViewModel.cs` `RegisterHotkey`; `Constants.PowerLauncherSharedEvent` | Activation contention | (catalog) |
| Module enabled/disabled by default unexpectedly | `Microsoft.Launcher/dllmain.cpp` vs `EnabledModules.cs` | Default-state drift | Default-enabled state |

## Confirmation steps
1. Identify the owning plugin vs core host; open the candidate file/symbol and verify the path.
2. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
3. Reproduce with the reporter's exact query (note action keyword, plugin, culture/locale).
4. Add/extend a test in the plugin's `*.UnitTest(s)` (or `Wox.Test`) before fixing; assert score
   parity for matcher/ranking changes.
