---
name: powertoys-powertoysrun-knowledge
description: 'PowerToys Run (launcher) module knowledge: feature->file/function map of the query pipeline, async result ranking, plugin architecture (AssemblyLoadContext isolation, global vs action-keyword plugins, IDelayedExecutionPlugin), Win+Space activation hotkey, Windows Search indexer, and recurring regression playbooks (shell-plugin quote breakout / command injection, DWM composition crash 0x80263001, fuzzy-matcher empty-input crash, calculator log/pow/culture parsing). Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/launcher — query/results, StringMatcher fuzzy scoring, plugin loading, hotkey/activation, cancellation/threading, Shell/WindowWalker/Calculator/VSCodeWorkspaces/Indexer plugins, settings. Keywords: PowerToys Run, launcher, Wox, plugin, fuzzy match, result ranking, cancellation token, activation hotkey, command injection, DWM composition, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Run (Launcher) Knowledge

Grounded engineering knowledge for the PowerToys **Run** module (`src/modules/launcher/`) — a
keyboard-driven application launcher (a fork of Wox) that debounces a query, fans it out across
loaded plugins on background threads, fuzzy-matches and ranks results, and executes the selected
action. Use it to localize code fast, avoid known regression traps, and enforce the conventions the
maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/launcher/` (core host or a plugin) and needing prior art.
- Fixing/triaging a Run bug: results missing/mis-ranked, query lag/stale results, activation hotkey
  not working, crash on startup, a plugin failing to load, calculator wrong result, shell command misfire.
- Reviewing a Run PR and checking it against maintainer conventions and regression traps.
- Writing or modifying a **plugin** (IPlugin / IDelayedExecutionPlugin / IContextMenu / ISettingProvider).
- Touching the query pipeline, cancellation/threading, fuzzy scoring, result ranking, or hotkey registration.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

### Core host (`PowerLauncher`, `Wox.Infrastructure`, `Wox.Plugin`)

| Sub-feature | Implementation (file · symbol) |
|---|---|
| App bootstrap / single instance / crash reporting | `PowerLauncher/App.xaml.cs`; ``PowerLauncher/Helper/SingleInstance`1.cs``; `Helper/ErrorReporting.cs` |
| Recoverable DWM composition crash guard | `PowerLauncher/Helper/ExceptionHelper.cs` `IsRecoverableDwmCompositionException` (`DWM_E_COMPOSITIONDISABLED = 0x80263001`); consumed in `ErrorReporting.cs` |
| Main window / search box / results view | `PowerLauncher/MainWindow.xaml.cs`, `CustomSearchBox.cs`, `ResultList.xaml.cs` |
| Query orchestration, debounce, cancellation | `PowerLauncher/ViewModel/MainViewModel.cs` `Query`, `QueryResults(bool? delayedExecution)`; `_updateSource`/`_updateToken` (`CancellationTokenSource`) |
| Activation hotkey (global hotkey + centralized keyboard hook) | `MainViewModel.cs` `RegisterHotkey`, `OnHotkey`; `NativeEventWaiter.WaitForEventLoop(Constants.PowerLauncherSharedEvent()...)`; `_usingGlobalHotKey`, `NativeMethods.RegisterHotKey/UnregisterHotKey` |
| Result list model, incremental update, final sort | `MainViewModel.cs` `UpdateResultView`; `ViewModel/ResultsViewModel.cs` `Results.Sort(queryTuning)`; `Helper/ResultCollection.cs` |
| Result ranking formula | `Wox.Plugin/Result.cs` `Score` + `Metadata.WeightBoost + Score + (SelectedCount * selectedItemMultiplier)` |
| Fuzzy string matching / scoring | `Wox.Infrastructure/StringMatcher.cs` `FuzzySearch`/`FuzzyMatch`; `MatchResult.cs`, `MatchOption.cs`, `Alphabet.cs` |
| Selection-history boost (MRU ranking) | `Wox.Plugin/UserSelectedRecord.cs` (`SelectedCount`); `PowerLauncher/Storage/QueryHistory.cs` |
| Settings load / round-trip | `PowerLauncher/Settings.cs`, `SettingsReader.cs`; `Wox.Infrastructure/UserSettings/PowerToysRunSettings.cs`; VM `settings-ui/.../ViewModels/PowerLauncherViewModel.cs` |
| Native module shim / default enabled state | `Microsoft.Launcher/dllmain.cpp` (must match `EnabledModules.cs`) |

### Plugin model

| Concept | Implementation (file · symbol) |
|---|---|
| Plugin host / registry (static) | `PowerLauncher/Plugin/PluginManager.cs` `InitializePlugins`, `QueryForPlugin`, `GetPluginsForInterface<T>` |
| Parallel plugin init (fault-isolated) | `PluginManager.InitializePlugins` → `Parallel.ForEach(AllPlugins, ...)` with per-plugin `try/catch` |
| Assembly isolation per plugin | `Wox.Plugin/PluginLoadContext.cs` : `AssemblyLoadContext`; `PowerLauncher/Plugin/PluginConfig.cs`; `PluginsLoader` (historical — was `PowerLauncher/Plugin/PluginsLoader.cs`, removed in #10515; plugin loading now in `PluginManager`) |
| Plugin contracts | `Wox.Plugin/IPlugin.cs`, `IDelayedExecutionPlugin.cs`, `IContextMenu.cs`, `ISettingProvider.cs`, `Wox.Plugin/Interfaces/IReloadable.cs` |
| Global vs action-keyword plugins | `PluginManager.GlobalPlugins` / `NonGlobalPlugins`; `PluginMetadata.cs` (`ActionKeyword`, `IsGlobal`) |
| Query parsing (keyword split) | `PowerLauncher/Plugin/QueryBuilder.cs`; `Wox.Plugin/Query.cs` |
| Public API surface for plugins | `PowerLauncher/PublicAPIInstance.cs`; `Wox.Plugin/IPublicAPI.cs` |

### Key bundled plugins (`Plugins/`)

| Plugin | Root · notable file | Purpose / gotcha area |
|---|---|---|
| Shell (`>`) | `Microsoft.Plugin.Shell/Main.cs` `EscapeCmdArgument`/`EscapePowerShellArgument`, `PrepareProcessStartInfo` | Runs cmd/PowerShell/WT; **per-shell quote escaping** (command-injection surface) |
| Program | `Microsoft.Plugin.Program/Programs/Win32Program.cs`, `UWP.cs` | Win32 + UWP app enumeration/launch |
| Indexer | `Microsoft.Plugin.Indexer/` | Windows Search (OLE DB / Search SQL) file results |
| Folder | `Microsoft.Plugin.Folder/` | Filesystem path browsing |
| WindowWalker | `Microsoft.Plugin.WindowWalker/Components/FuzzyMatching.cs` `FindBestFuzzyMatch` | Switch open windows; DP fuzzy match |
| Calculator | `Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs` `Interpret` | Mages engine; regex log10/ln normalization; culture-aware |
| Uri / WebSearch | `Microsoft.Plugin.Uri/`, `Community...WebSearch/` | URL detection / browser launch |
| VSCodeWorkspaces | `Community...VSCodeWorkspaces/VSCodeHelper/VSCodeInstances.cs`, `WorkspacesHelper/VSCodeWorkspacesApi.cs` | Recent-workspace discovery (storage.json / UNC) |
| Registry / System / WindowsSettings / WindowsTerminal / TimeDate / History / OneNote / Service | respective plugin roots | Utility result providers |

**Query pipeline (critical order):** `MainViewModel.QueryResults` cancels the prior
`_updateSource`, builds the `Query` (`QueryBuilder`), then runs **fast** plugins
(`PluginManager.QueryForPlugin`) on a background `Task`, pushing incremental results via
`UpdateResultView(..., _updateToken)`; **delayed-execution** plugins
(`IDelayedExecutionPlugin`) run second. Results are sorted by the ranking formula, optionally
deferred to a single final sort when `SearchWaitForSlowResults` tuning is on. Every stage is gated
on `_updateToken` so a newer keystroke discards stale work.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Shell-plugin quote breakout (command injection)
- **Symptom:** a Run "Shell" command containing `"` can terminate the quoted wrapper and inject
  extra commands (e.g. `foo" & calc.exe`).
- **Where:** `Microsoft.Plugin.Shell/Main.cs` `EscapeCmdArgument` / `EscapePowerShellArgument`, embedded in `PrepareProcessStartInfo`.
- **Root cause:** one-size escaping — C-runtime backslash-escaping (`\"`) is **not** honored by
  cmd.exe (needs doubled `""`) and differs again for PowerShell, so `\"` can still close the wrapper.
- **Guardrail:** escaping must be **shell-specific** (cmd → `""`, PowerShell → `\"`), or avoid the
  quoted wrapper entirely via `ProcessStartInfo.ArgumentList` / PowerShell `-EncodedCommand`. Never
  reuse a CreateProcess/C-runtime arg escaper for a cmd/PowerShell command string. See
  [command-line quoting](https://learn.microsoft.com/en-us/cpp/cpp/main-function-command-line-args).
  Evidence: [PR #45554](https://github.com/microsoft/PowerToys/pull/45554) (review thread on `Main.cs`).

### DWM composition crash on launch (0x80263001)
- **Symptom:** Run "ran into an issue" / `[FATAL] From UI thread's exception`; `COMException
  (0x80263001) desktop composition disabled`, often after RDP / display change / theme switch.
- **Where:** WPF acrylic/blur bring-up; guarded in `PowerLauncher/Helper/ExceptionHelper.cs`
  `IsRecoverableDwmCompositionException`, consumed by `Helper/ErrorReporting.cs`.
- **Root cause:** DWM composition can be transiently unavailable; the `COMException` bubbles to the
  UI-thread handler and looks fatal.
- **Guardrail:** classify `DWM_E_COMPOSITIONDISABLED` (and DWM-composition stack patterns) as
  **recoverable** — swallow/retry instead of showing the crash UI. Evidence: issues
  [#48357](https://github.com/microsoft/PowerToys/issues/48357),
  [#49064](https://github.com/microsoft/PowerToys/issues/49064),
  [#49290](https://github.com/microsoft/PowerToys/issues/49290),
  [#49130](https://github.com/microsoft/PowerToys/issues/49130).

### Fuzzy matcher crash / behavior change on empty or edge input
- **Symptom:** rewriting the matcher regressed empty-`searchText` handling — `sLower[0]` can throw
  `IndexOutOfRangeException`; scores must stay identical to the previous algorithm.
- **Where:** `Microsoft.Plugin.WindowWalker/Components/FuzzyMatching.cs` `FindBestFuzzyMatch`
  (also applies to core `Wox.Infrastructure/StringMatcher.cs`).
- **Root cause:** the optimal-span DP rewrite indexed the first char without guarding empty input.
- **Guardrail:** guard empty/whitespace query before indexing; add tests asserting **score parity**
  with the prior algorithm across cases. Evidence:
  [PR #44551](https://github.com/microsoft/PowerToys/pull/44551) (automated review flagged
  `sLower[0]` throw; tests in `Microsoft.Plugin.WindowWalker.UnitTests`).

### Calculator: log/pow/culture mis-parse
- **Symptom:** `log(100)` resolved as natural log; `pow(x,y)` args swapped; results mis-formatted or
  a valid expression errors with wrong decimal/grouping separators.
- **Where:** `Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs` `Interpret` — the Mages
  engine treats `log` as `ln`, so regex rewrites `log`→`log10` with a negative lookahead so
  `log10`/`log2` are left intact; culture handling via `CultureInfo`.
- **Root cause:** Mages semantics differ from user expectation; culture-sensitive number formatting.
- **Guardrail:** keep the `log`/whitespace/`(` normalization regexes and their lookaheads correct;
  format results with the passed `CultureInfo` (never invariant-only). Complex numbers are Mages-capable
  but **not** wired through the front-end interpreter — return the "not supported" path, don't crash.
  Evidence: [PR #47767](https://github.com/microsoft/PowerToys/pull/47767),
  [PR #47506](https://github.com/microsoft/PowerToys/pull/47506); issues
  [#48264](https://github.com/microsoft/PowerToys/issues/48264),
  [#48247](https://github.com/microsoft/PowerToys/issues/48247).

### VS Code Workspaces: storage lookup / UNC paths
- **Symptom:** recent workspaces missing, or UNC (`\\server\share`) workspaces not opening.
- **Where:** `Community...VSCodeWorkspaces/VSCodeHelper/VSCodeInstances.cs` (instance/storage
  discovery), `WorkspacesHelper/VSCodeWorkspacesApi.cs` (parse + URI handling).
- **Root cause:** VS Code moved recent lists between `storage.json` locations across versions;
  UNC file URIs need distinct handling from local paths.
- **Guardrail:** resolve the shared-storage path per VS Code variant/version and handle UNC URIs
  explicitly. Evidence: [PR #47505](https://github.com/microsoft/PowerToys/pull/47505),
  [PR #48922](https://github.com/microsoft/PowerToys/pull/48922).

### Default-enabled state drift
- **Symptom:** a plugin/module ships enabled that should be disabled by default (or vice versa).
- **Where:** `Microsoft.Launcher/dllmain.cpp` default flags must mirror `EnabledModules.cs`.
- **Guardrail:** when changing default enablement, update **both** the native `dllmain.cpp` defaults
  and the managed `EnabledModules.cs` in lockstep. Evidence:
  [PR #47144](https://github.com/microsoft/PowerToys/pull/47144).

## Review Rules

Enforce these when reviewing or authoring Run changes:

- **Gate every query stage on the cancellation token.** New work in `QueryResults`/`UpdateResultView`
  must honor `_updateToken`; unchecked continuations produce stale/duplicated results and races
  (`MainViewModel.cs`). See [cancellation](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads).
- **Keep result ranking in one place.** Ordering comes from `Result` `Metadata.WeightBoost + Score +
  SelectedCount*multiplier` and `Results.Sort(queryTuning)` — don't add ad-hoc per-plugin re-sorts.
- **Isolate plugin faults.** Plugin init/query runs under `Parallel.ForEach` with per-plugin
  `try/catch`; a new code path must not let one plugin's exception abort the whole host
  (`PluginManager.cs`). Plugins load in their own `AssemblyLoadContext` (`PluginLoadContext.cs`) — do
  not assume shared/loaded assemblies.
- **Never build a shell command string with generic quote escaping.** Use shell-specific escaping or
  `ArgumentList`; a CreateProcess-style escaper in a cmd/PowerShell context reintroduces injection
  (#45554). Name/scope any escape helper to its exact parser.
- **Long/blocking work stays off the UI thread**, and UI updates marshal back via the Dispatcher
  (`Application.Current.Dispatcher.InvokeAsync`); the query fan-out already runs on `Task`s.
- **Fuzzy-match changes must prove score parity** and guard empty/edge input; add cases in the
  plugin's `*.UnitTests` (#44551).
- **Culture-correctness for user-facing parsing/formatting** (calculator, dates) — thread the caller's
  `CultureInfo`; don't hardcode separators (#47767).
- **Settings must round-trip** through `PowerToysRunSettings`; default enablement stays in sync
  between `dllmain.cpp` and `EnabledModules.cs` (#47144).
- **Ship a test with every fix.** Suites live in `Plugins/**/*.UnitTest(s)` and `Wox.Test`.

## Gotchas

- **Never** reuse a CreateProcess/C-runtime argument escaper (`\"`) for cmd.exe or PowerShell command
  strings — cmd needs doubled `""` and PowerShell differs; wrong escaper = command injection (#45554).
- **Never** treat `COMException 0x80263001` (DWM composition disabled) as fatal — route it through
  `IsRecoverableDwmCompositionException`; it's transient (RDP/display/theme changes) (#48357, #49064).
- **Never** index `searchText[0]` / `sLower[0]` in a matcher without guarding empty input — the DP
  rewrite crashed on it (#44551).
- **Never** ignore `_updateToken` in query continuations — a newer keystroke must discard older
  results, or the list flickers/shows stale entries.
- **`log` in the calculator means `ln` in Mages** — the engine rewrites `log`→`log10` by regex; a
  naive change silently returns natural log (#47767).
- **Global plugins run on every keystroke; action-keyword plugins only when their keyword leads** —
  a plugin registered global does redundant work and can pollute results (`PluginMetadata.IsGlobal`).
- **Default-enabled flags live in two files** — `dllmain.cpp` and `EnabledModules.cs`; changing one
  only is a compat bug (#47144).
- **Plugins load in isolated `AssemblyLoadContext`s** — don't rely on a type being loadable just
  because the host or another plugin loaded it (`PluginLoadContext.cs`).

## Using This Skill in PR Review (Anti-Anchoring)

**Read the diff cold first.** Do not skim this file's playbooks and then hunt the diff for those
themes — that anchors you on recurring concerns and lowers your catch rate on the PR's actual issues.

1. Read the diff and form your own list of concerns from what actually changed.
2. **Then** cross-check the touched files against the Module Map, Regression Playbooks, and Review
   Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat this file as a checklist for the touched area, not a script for the whole review.

When localizing a bug, if the symptom doesn't map cleanly to a row above, reason from the symptom
and verify in source — a thin/absent map entry can anchor you onto a confident, wrong file (this
module is plugin-heavy; confirm which plugin owns the behavior before editing the core host).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Run PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/launcher/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/launcher)
- [Windows command-line quoting](https://learn.microsoft.com/en-us/cpp/cpp/main-function-command-line-args) · [Cancellation in managed threads](https://learn.microsoft.com/en-us/dotnet/standard/threading/cancellation-in-managed-threads) · [AssemblyLoadContext](https://learn.microsoft.com/en-us/dotnet/core/dependency-loading/understanding-assemblyloadcontext)
