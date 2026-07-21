# PowerToys Run (Launcher) Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each
claim in source before acting. Symptoms map to `src/modules/launcher/`.

## Key Decisions (context for the playbooks)

- **Debounced, cancellable query fan-out.** `MainViewModel.QueryResults(bool? delayedExecution)`
  cancels the previous `_updateSource` (`CancellationTokenSource`), builds the `Query`
  (`QueryBuilder`), runs **fast** plugins via `PluginManager.QueryForPlugin`, then
  **`IDelayedExecutionPlugin`** ones second. All result pushes go through
  `UpdateResultView(..., _updateToken)` so stale keystrokes are dropped. Correctness invariant.
- **Single ranking formula.** `Wox.Plugin/Result.cs` ranks by
  `Metadata.WeightBoost + Score + (SelectedCount * selectedItemMultiplier)`, where `Score` comes
  from `StringMatcher.FuzzySearch` and `SelectedCount` from `UserSelectedRecord` (selection history
  boost). Final ordering via `Results.Sort(queryTuning)`; optional single final sort when
  `SearchWaitForSlowResults` tuning is enabled.
- **Plugin host is static + fault-isolated.** `PluginManager.InitializePlugins` loads plugins with
  `Parallel.ForEach` and per-plugin `try/catch`; each plugin's assembly loads in its own
  `PluginLoadContext : AssemblyLoadContext`. One bad plugin must not crash the host or block others.
- **Global vs action-keyword plugins.** `PluginManager.GlobalPlugins` run on every query;
  `NonGlobalPlugins` run only when their `ActionKeyword` prefixes the query (`PluginMetadata.IsGlobal`).
- **Activation via native shared event, not a WPF hotkey.** `MainViewModel.RegisterHotkey` waits on
  `Constants.PowerLauncherSharedEvent()` (and a centralized keyboard-hook event) through
  `NativeEventWaiter.WaitForEventLoop`; falls back to Win32 `RegisterHotKey` (`_usingGlobalHotKey`).
  Win+Space is shared with Command Palette, causing activation contention when both are enabled (#48691).
- **Calculator uses the Mages engine.** `CalculateEngine.Interpret` regex-normalizes `log`→`log10`
  (Mages treats `log` as `ln`) with a negative lookahead protecting `log10`/`log2`, and formats with
  the caller's `CultureInfo`. Complex numbers are Mages-capable but not wired through the front-end.
- **DWM composition exceptions are recoverable.** `ExceptionHelper.IsRecoverableDwmCompositionException`
  classifies `DWM_E_COMPOSITIONDISABLED (0x80263001)` and DWM stack patterns so `ErrorReporting`
  doesn't show the crash UI for a transient composition drop.

## Regression Table

| Class | Symptom | Where (file · symbol) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| Command injection | `"` in a Shell command breaks out of the wrapper | `Microsoft.Plugin.Shell/Main.cs` `EscapeCmdArgument`/`EscapePowerShellArgument` | Generic C-runtime `\"` escaping not honored by cmd/PowerShell | Shell-specific escaping (cmd `""`, PS `\"`) or `ArgumentList`/`-EncodedCommand` | [PR #45554](https://github.com/microsoft/PowerToys/pull/45554) |
| Crash (recoverable) | "ran into an issue" / `COMException 0x80263001` on launch | `PowerLauncher/Helper/ExceptionHelper.cs`; `ErrorReporting.cs` | DWM composition transiently disabled bubbles to UI thread | Classify as recoverable; don't show crash UI | [#48357](https://github.com/microsoft/PowerToys/issues/48357), [#49064](https://github.com/microsoft/PowerToys/issues/49064), [#49290](https://github.com/microsoft/PowerToys/issues/49290), [#49130](https://github.com/microsoft/PowerToys/issues/49130) |
| Crash / behavior | Matcher rewrite throws on empty input; scores must match old algo | `Microsoft.Plugin.WindowWalker/Components/FuzzyMatching.cs` `FindBestFuzzyMatch` | DP rewrite indexed `sLower[0]` without empty guard | Guard empty/whitespace; assert score parity in tests | [PR #44551](https://github.com/microsoft/PowerToys/pull/44551) |
| Calculator | `log(100)`=ln; `pow(x,y)` swapped; mis-format | `Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs` `Interpret` | Mages semantics + culture formatting | Keep `log`→`log10` regex + lookahead; use `CultureInfo` | [PR #47767](https://github.com/microsoft/PowerToys/pull/47767), [PR #47506](https://github.com/microsoft/PowerToys/pull/47506), [#48264](https://github.com/microsoft/PowerToys/issues/48264), [#48247](https://github.com/microsoft/PowerToys/issues/48247) |
| Plugin (VS Code) | Recent workspaces missing / UNC not opening | `Community...VSCodeWorkspaces/VSCodeHelper/VSCodeInstances.cs`, `WorkspacesHelper/VSCodeWorkspacesApi.cs` | storage.json path moved; UNC URI mishandled | Resolve per-variant storage path; handle UNC explicitly | [PR #47505](https://github.com/microsoft/PowerToys/pull/47505), [PR #48922](https://github.com/microsoft/PowerToys/pull/48922) |
| Stale results | List flickers / shows old-query entries | `MainViewModel.cs` `QueryResults`/`UpdateResultView` | Continuation ignored `_updateToken` | Gate every push on `_updateToken`; cancel prior source | (invariant; see Key Decisions) |
| Ranking | Expected result ranked too low | `Wox.Plugin/Result.cs` (`Score`, `SelectedCount`), `StringMatcher.cs` | Ad-hoc per-plugin re-sort or wrong `Score` | Keep single ranking formula + `Results.Sort(queryTuning)` | (invariant) |
| Plugin loading | "Plugin Loading Error"; one plugin breaks host | `PluginManager.cs` `InitializePlugins`; `PluginLoadContext.cs` | Missing fault isolation / assembly assumption | `Parallel.ForEach` + per-plugin `try/catch`; isolated `AssemblyLoadContext` | [#48380](https://github.com/microsoft/PowerToys/issues/48380) |
| Activation | Win+Space contention Run ↔ Command Palette | `MainViewModel.cs` `RegisterHotkey`; `Constants.PowerLauncherSharedEvent` | Shared hotkey between two launchers | Coordinate hotkey ownership when both enabled | [#48691](https://github.com/microsoft/PowerToys/issues/48691) |
| Compat | Module ships wrong default-enabled state | `Microsoft.Launcher/dllmain.cpp` vs `EnabledModules.cs` | Defaults drift between native + managed | Update both in lockstep | [PR #47144](https://github.com/microsoft/PowerToys/pull/47144) |
| Search results | Files not returning expected results | `Microsoft.Plugin.Indexer/` (Windows Search query) | Windows Search index / query scope | Verify Search SQL / index availability | [#48449](https://github.com/microsoft/PowerToys/issues/48449) |

## Common Practices (enforced in review)

- **Cancellation-first.** Any query-path continuation checks `_updateToken`; a new keystroke cancels
  `_updateSource`. Never push results for a superseded query.
- **Threading.** Query fan-out runs on background `Task`s; UI mutations marshal through the
  Dispatcher (`Application.Current.Dispatcher.InvokeAsync`). Keep file/registry I/O off the UI thread.
- **Plugin isolation.** Init/query wrapped in `try/catch` per plugin; plugins in their own
  `AssemblyLoadContext`. Log-and-continue on plugin failure, never abort the host.
- **Culture correctness.** Thread `CultureInfo` through user-facing parse/format (calculator, dates);
  don't hardcode decimal/grouping separators.
- **Security.** Treat every plugin that shells out (`Shell`, `Uri`, `WindowsTerminal`) as an
  injection surface; escape per target parser or pass args as a list, never concatenate into a
  quoted string with generic escaping.
- **Testing.** Fixes ship with tests in `Plugins/**/*.UnitTest(s)` (e.g.
  `Microsoft.PowerToys.Run.Plugin.Calculator.UnitTest`, `Microsoft.Plugin.WindowWalker.UnitTests`)
  and core `Wox.Test`. Fuzzy/ranking changes assert score parity.

---
*Corpus: 12 merged PRs, 74 review comments, 30 bug issues + source verification against
`src/modules/launcher`. Excluded as noise: .NET 10 upgrade (#41280), check-spelling refresh
(#47119), MSTEST0017 assertion-order (#46712), unused-namespace cleanup (#46221), PowerShell
build-script reliability (#46729).*
