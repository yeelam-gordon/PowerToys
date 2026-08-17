# PowerToys Run PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces.

## General (any Run PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] A unit test accompanies each behavior change (`Plugins/**/*.UnitTest(s)`, `Wox.Test`).
- [ ] Correct plugin identified as owner of the behavior (plugin vs core host).

## Query pipeline / threading (`MainViewModel.cs`, `PluginManager.cs`)
- [ ] Every result push / continuation gates on `_updateToken`; prior `_currentQuerySession` is
      cancelled and disposed safely.
- [ ] Long/blocking work runs off the UI thread; UI mutations marshal via the Dispatcher.
- [ ] Fast vs `IDelayedExecutionPlugin` ordering preserved; no stale-result flicker.
- [ ] No ad-hoc re-sort — ranking stays `WeightBoost + Score + SelectedCount*multiplier` + `Results.Sort(queryTuning)`.

## Plugin model (`PluginManager.cs`, `PluginLoadContext.cs`, `PluginMetadata.cs`)
- [ ] Plugin init/query keeps per-plugin `try/catch`; one plugin can't abort the host.
- [ ] No assumption a type is loaded across plugins — each has its own `AssemblyLoadContext`.
- [ ] `IsGlobal` vs `ActionKeyword` correct — global plugins run every keystroke (avoid needless work).
- [ ] New plugin contract usage matches `IPlugin`/`IDelayedExecutionPlugin`/`IContextMenu`/`ISettingProvider`.

## Fuzzy match / ranking (`StringMatcher.cs`, `Result.cs`, plugin matchers)
- [ ] Empty/whitespace query guarded before indexing (`sLower[0]` crash class, #44551).
- [ ] Score parity vs previous algorithm asserted in tests.

## Shell / process-launching plugins (`Microsoft.Plugin.Shell/Main.cs`, Uri, WindowsTerminal)
- [ ] Quote escaping is **shell-specific** (cmd `""`, PowerShell `\"`) or uses `ArgumentList` — no
      generic C-runtime `\"` escaper reused for cmd/PowerShell (command injection, #45554).
- [ ] Escape helper named/scoped to its exact parser; not a "general sanitizer".
- [ ] `Environment.ExpandEnvironmentVariables` / run-as paths reviewed for injection.

## Calculator (`CalculateEngine.cs`)
- [ ] `log`→`log10` regex + negative lookahead (`log10`/`log2`) intact (#47767).
- [ ] Results parsed/formatted with the passed `CultureInfo`; no hardcoded separators.
- [ ] Unsupported inputs (e.g. complex numbers) return the graceful path, not a crash.

## Crash resilience (`ExceptionHelper.cs`, `ErrorReporting.cs`, `App.xaml.cs`)
- [ ] `COMException 0x80263001` / DWM composition patterns classified as recoverable (#48357).
- [ ] UI-thread exception handler doesn't show crash UI for transient/recoverable errors.

## Settings / compatibility
- [ ] Settings round-trip through `PowerToysRunSettings`.
- [ ] Default-enabled state changed in **both** `dllmain.cpp` and `EnabledModules.cs` (#47144).

## Plugin-specific data discovery (VSCodeWorkspaces, Program, Indexer, Folder)
- [ ] External data paths resolved per app variant/version; UNC / long paths handled (#47505, #48922).
- [ ] Windows Search / filesystem queries degrade gracefully when unavailable. Treat #48449 as
      ambiguous cross-product evidence until reproduced in PowerToys Run.
