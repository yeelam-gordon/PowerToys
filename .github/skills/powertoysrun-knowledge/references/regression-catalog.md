# PowerToys Run — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split note:** `SKILL.md` owns operational playbooks, review rules, and implementation
> guardrails. This file retains historical evidence, source anchors, architecture decisions,
> unresolved clusters, chronology, and caveats without restating the playbooks.

All anchors are hypotheses to confirm in the current `src/modules/launcher/` tree.

## Change and regression evidence

| Approximate chronology | Evidence | Source anchors | Historical finding or decision |
|---|---|---|---|
| Query architecture | invariant in source | `MainViewModel.QueryResults`; `_currentQuerySession`; `_updateToken`; `QuerySession`; `QueryBuilder`; `PluginManager.QueryForPlugin`; `IDelayedExecutionPlugin`; `UpdateResultView` | Query fan-out is debounced and cancellable; fast plugins precede delayed plugins and stale result pushes are token-gated. |
| Earlier architecture | invariant in source | `Wox.Plugin/Result.cs`; `StringMatcher.FuzzySearch`; `UserSelectedRecord`; `Results.Sort(queryTuning)` | Ranking is centralized as `Metadata.WeightBoost + Score + SelectedCount * selectedItemMultiplier`, with optional final sorting under `SearchWaitForSlowResults`. |
| Earlier architecture | invariant in source | `PluginManager.InitializePlugins`; `PluginLoadContext : AssemblyLoadContext`; `GlobalPlugins`; `NonGlobalPlugins`; `PluginMetadata.IsGlobal` | Plugin initialization is parallel and fault-isolated; each plugin has an assembly load context; action-keyword and global plugins have distinct routing. |
| Earlier architecture | invariant in source | `MainViewModel.RegisterHotkey`; `Constants.PowerLauncherSharedEvent()`; `NativeEventWaiter.WaitForEventLoop`; `_usingGlobalHotKey`; `RegisterHotKey` | Activation primarily uses native shared events/centralized hooks, with Win32 global-hotkey fallback. |
| Earlier architecture | invariant in source | `CalculateEngine.Interpret` | The Mages-backed calculator normalizes `log` to `log10`, preserves `log10`/`log2` through a negative lookahead, and formats with caller culture; complex-number support is not exposed by the front end. |
| Earlier architecture | invariant in source | `ExceptionHelper.IsRecoverableDwmCompositionException`; `ErrorReporting` | `DWM_E_COMPOSITIONDISABLED (0x80263001)` and recognized DWM stack patterns are treated as recoverable. |
| 1 | [PR #44551](https://github.com/microsoft/PowerToys/pull/44551) | `Microsoft.Plugin.WindowWalker/Components/FuzzyMatching.cs::FindBestFuzzyMatch`; WindowWalker unit tests | A dynamic-programming matcher rewrite indexed `sLower[0]` on empty input; review required an empty guard and score-parity coverage. |
| 2 | [PR #45554](https://github.com/microsoft/PowerToys/pull/45554) | `Microsoft.Plugin.Shell/Main.cs::EscapeCmdArgument`; `EscapePowerShellArgument`; `PrepareProcessStartInfo` | Generic C-runtime quote escaping did not model cmd/PowerShell parsing and allowed quote breakout. Review established parser-specific escaping or structured arguments. |
| 3 | [PR #47144](https://github.com/microsoft/PowerToys/pull/47144) | `Microsoft.Launcher/dllmain.cpp`; `EnabledModules.cs` | Native and managed default-enabled values had to change together. |
| 4 | [PR #47505](https://github.com/microsoft/PowerToys/pull/47505) | `Community...VSCodeWorkspaces/VSCodeHelper/VSCodeInstances.cs` | VS Code recent-workspace discovery was updated for moved `storage.json` locations and product variants. |
| 5 | [PR #47506](https://github.com/microsoft/PowerToys/pull/47506), issues [#48264](https://github.com/microsoft/PowerToys/issues/48264), [#48247](https://github.com/microsoft/PowerToys/issues/48247) | `Microsoft.PowerToys.Run.Plugin.Calculator/CalculateEngine.cs::Interpret` | Calculator semantics and formatting exposed Mages/culture mismatches, including `pow` argument behavior and unsupported complex results. |
| 6 | [PR #47767](https://github.com/microsoft/PowerToys/pull/47767) | `CalculateEngine.Interpret` normalization regexes and `CultureInfo` flow | `log` user expectations were reconciled with Mages while protecting `log10` and `log2`; caller culture remained authoritative. |
| 7 | issues [#48357](https://github.com/microsoft/PowerToys/issues/48357), [#49064](https://github.com/microsoft/PowerToys/issues/49064), [#49290](https://github.com/microsoft/PowerToys/issues/49290), [#49130](https://github.com/microsoft/PowerToys/issues/49130) | `PowerLauncher/Helper/ExceptionHelper.cs`; `ErrorReporting.cs` | Repeated launch failures after RDP/display/theme transitions support treating composition-disabled exceptions as transient rather than fatal. |
| 8 | issue [#48380](https://github.com/microsoft/PowerToys/issues/48380) | `PluginManager.InitializePlugins`; `PluginLoadContext.cs` | “Plugin Loading Error” reports reinforce per-plugin exception isolation and rejection of shared-assembly assumptions. |
| 10 | issue [#48691](https://github.com/microsoft/PowerToys/issues/48691) | `MainViewModel.RegisterHotkey`; `Constants.PowerLauncherSharedEvent` | Win+Space ownership can contend between PowerToys Run and Command Palette when both are enabled. |
| 11 | [PR #48922](https://github.com/microsoft/PowerToys/pull/48922) | `Community...VSCodeWorkspaces/WorkspacesHelper/VSCodeWorkspacesApi.cs` | UNC workspace URIs required handling distinct from local file paths. |

## Maintainer decision ledger

| Area | Decision | Evidence retained |
|---|---|---|
| Query freshness | A newer query invalidates all older continuations and result pushes. | `_currentQuerySession`/`_updateToken` flow in `QueryResults`, `QuerySession`, and `UpdateResultView`. |
| Ranking | Keep one host-level ranking formula and one sorting path; plugins contribute scores rather than independently reordering the list. | `Result.cs`, `UserSelectedRecord`, `Results.Sort(queryTuning)`. |
| Plugin resilience | A plugin failure is logged and contained; it must not abort initialization or querying for other plugins. | `Parallel.ForEach` with per-plugin `try/catch`; `PluginLoadContext`. |
| Shell execution | Escaping is defined by the target command parser, not by generic CreateProcess/C-runtime conventions. | PR #45554 review of `Main.cs`. |
| Matcher rewrites | Behavioral optimization must preserve prior scores and define empty/edge input. | PR #44551 and WindowWalker unit tests. |
| Calculator | User-visible parse/format semantics follow the caller's `CultureInfo`; front-end support is narrower than the Mages engine. | PRs #47506 and #47767. |
| Defaults | Native module defaults and managed settings defaults form one compatibility decision. | PR #47144. |
| DWM failures | Known transient composition failures bypass fatal crash UI. | `ExceptionHelper.IsRecoverableDwmCompositionException` and the four linked reports. |

## Unresolved clusters (at distillation time)

- **Activation ownership:** [#48691](https://github.com/microsoft/PowerToys/issues/48691) remains a
  cross-launcher coordination problem, not solely a registration failure.
- **DWM/transient startup failures:** [#48357](https://github.com/microsoft/PowerToys/issues/48357),
  [#49064](https://github.com/microsoft/PowerToys/issues/49064),
  [#49290](https://github.com/microsoft/PowerToys/issues/49290), and
  [#49130](https://github.com/microsoft/PowerToys/issues/49130) form a recurring environment-sensitive
  cluster even with recoverability classification.
- **Plugin load variability:** [#48380](https://github.com/microsoft/PowerToys/issues/48380) may
  involve plugin-local dependencies or load-context assumptions.
- **Ambiguous cross-product report:** [#48449](https://github.com/microsoft/PowerToys/issues/48449)
  names Command Palette in its body but carries a PowerToys Run product label. Retain it as
  conflicting evidence; do not use it as categorical Run or Command Palette attribution without
  reproduction.

## Caveats and corpus boundaries

- “Invariant in source” rows have no single PR and should not be cited as historical causation.
- The VS Code evidence covers both storage discovery and UNC URI handling; they are separate failure
  modes despite sharing one plugin.
- Mages can evaluate more types than the launcher UI accepts; engine capability is not evidence of
  supported front-end behavior.
- Corpus: 12 merged PRs, 74 review comments, 30 bug issues, plus source verification.
- Excluded as mechanical or cross-cutting noise: .NET 10 upgrade
  [#41280](https://github.com/microsoft/PowerToys/pull/41280), check-spelling refresh
  [#47119](https://github.com/microsoft/PowerToys/pull/47119), MSTEST0017 assertion ordering
  [#46712](https://github.com/microsoft/PowerToys/pull/46712), unused namespaces
  [#46221](https://github.com/microsoft/PowerToys/pull/46221), and PowerShell build-script reliability
  [#46729](https://github.com/microsoft/PowerToys/pull/46729).
