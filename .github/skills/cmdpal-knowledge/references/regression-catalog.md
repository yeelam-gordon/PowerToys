# CmdPal Regression Evidence Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split:** `SKILL.md` owns reusable localization, playbooks, guardrails, review rules, and pitfalls.
> This catalog records the evidence behind them: chronology, source anchors, reviewer decisions,
> unresolved symptom clusters, and confidence caveats.

The reviewed history slice is July 2026 merged PRs and review threads plus the contemporaneous issue
list. Open reports below are observations, not established root causes.

## Merged-fix and decision chronology

| Issue / PR | Evidence and decision | Exact source anchor | Review / caveat |
|---|---|---|---|
| [#49089](https://github.com/microsoft/PowerToys/issues/49089) → [#49095](https://github.com/microsoft/PowerToys/pull/49095) | Fixed the dead Dock **Open Command Palette** item. `DockControl.InvokeItem` summons only page commands; `GoHome()` alone is insufficient. The same change broke a root-page DI cycle with a deferred accessor. | `Microsoft.CmdPal.UI/Dock/DockControl.xaml.cs`; `Microsoft.CmdPal.UI.ViewModels/Commands/BuiltInsCommandProvider.cs`; `Microsoft.CmdPal.UI.ViewModels/IRootPageAccessor.cs`; `Microsoft.CmdPal.UI/DeferredRootPageAccessor.cs`; DI seam `IRootPageService.cs` → `PowerToysRootPageService.cs` | Reviewer decision [r3538570672](https://github.com/microsoft/PowerToys/pull/49095#discussion_r3538570672): when the accessor is registered in DI, inject it rather than wrapping the factory again. |
| [#49113](https://github.com/microsoft/PowerToys/issues/49113) → [#49182](https://github.com/microsoft/PowerToys/pull/49182) | Prevented actions from executing against a hidden selected item while the palette was collapsed. | `Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs` | Part of the compact/collapsed state-machine cluster; verify list/content and expanded/collapsed states separately. |
| [#49116](https://github.com/microsoft/PowerToys/issues/49116) | Recorded the search box disappearing on compact-mode back navigation from content to list. | `Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs` | Fix provenance was not captured in this history slice. |
| [#49177](https://github.com/microsoft/PowerToys/pull/49177) | Added Down/Tab expansion from collapsed mode. | `Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs` | Keyboard-path evidence; does not establish pointer-path correctness. |
| [#49184](https://github.com/microsoft/PowerToys/pull/49184) | Reasserted the transparent HWND frame after focus changes with `RedrawWindow`. | `Microsoft.CmdPal.UI/MainWindow.xaml.cs`; `Microsoft.CmdPal.UI/NativeMethods.txt` | DWM can repaint non-client state after the initial frame change. |
| [#49186](https://github.com/microsoft/PowerToys/pull/49186) | Corrected `SettingsModel.CompactMode` default from `true` to `false`. | `Microsoft.CmdPal.UI.ViewModels/SettingsModel.cs` | Reviewer also rejected explicit initialization to a type default: [r3539014488](https://github.com/microsoft/PowerToys/pull/49186#discussion_r3539014488). Local branches may lag this merged state. |
| [#49168](https://github.com/microsoft/PowerToys/issues/49168) → [#49171](https://github.com/microsoft/PowerToys/pull/49171) | Stopped Dock/host rebuilds on unrelated settings changes. Record fields backed by `ImmutableList` compared by reference; the fix used `EquatableList<T>` and `_settings == args.DockSettings` guards. | `Microsoft.CmdPal.UI.ViewModels/Settings/DockSettings.cs`; ``Microsoft.CmdPal.UI.ViewModels/Settings/EquatableList`1.cs``; Dock and `MainWindow` settings-change handlers | The `SettingsModel` “LOAD BEARING” subscriber warning remains relevant when adding live-reacting settings. |
| [#49159](https://github.com/microsoft/PowerToys/issues/49159) → [#49162](https://github.com/microsoft/PowerToys/pull/49162) | Preserved soft-disabled single-metric PerformanceMonitor Dock bands by emitting disabled placeholders with stable metric IDs. | `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PerformanceMonitor/PerformanceMonitorCommandsProvider.cs::BandMetrics`; `PerformanceWidgetsPage.GetBandId`; `PerformanceMonitorDisabledPage` | Battery remains a separate unresolved state; see [#49163](https://github.com/microsoft/PowerToys/issues/49163). |
| [#49236](https://github.com/microsoft/PowerToys/issues/49236) → [#49241](https://github.com/microsoft/PowerToys/pull/49241) | Added Origin/EA/UPlay/Xbox internet shortcuts to app discovery. | `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Apps/Programs/Win32Program.cs`; Apps unit tests | Review preference: use `[GeneratedRegex]` for the allow-list pattern. |
| [#45855](https://github.com/microsoft/PowerToys/issues/45855) → [#49253](https://github.com/microsoft/PowerToys/pull/49253) | Replaced localized page titles used as breadcrumb/navigation keys with invariant tags. | `Microsoft.CmdPal.UI/Settings/SettingsWindow.xaml.cs` (`Crumb.Data`, `Navigate`) | User-visible titles and routing identifiers are separate contracts. |
| [#49260](https://github.com/microsoft/PowerToys/pull/49260) | Added toast icon/action options through `IToastArgs2`, with toolkit implementation and sample. | `Microsoft.CmdPal.UI/ToastWindow.*`; `extensionsdk/Microsoft.CommandPalette.Extensions/*.idl`; toolkit `ToastArgs` | Reviewer/compatibility decision: evolve the out-of-proc WinRT SDK additively; do not mutate `IToastArgs`. |
| [#49262](https://github.com/microsoft/PowerToys/pull/49262) | Added configurable toast position, including the OS setting, and position-specific transitions. | `Microsoft.CmdPal.UI/ToastWindow.xaml.cs` (`PositionWindow(ToastPosition)`); `SettingsModel.cs`; OS registry read | Registry and animation behavior are platform-dependent. |
| [#49266](https://github.com/microsoft/PowerToys/pull/49266) | Added Settings/Help to the search-bar context menu. | `Microsoft.CmdPal.UI/Controls/SearchBar.xaml`; `SearchBar.xaml.cs` | Feature record; no regression root cause recorded. |
| [#49312](https://github.com/microsoft/PowerToys/issues/49312) → [#49313](https://github.com/microsoft/PowerToys/pull/49313) | Hid the separator while collapsed. | `Microsoft.CmdPal.UI/Pages/ShellPage.xaml`; `Microsoft.CmdPal.UI/Helpers/BindTransformers.cs` | Another independent compact-mode visual state. |
| [#49309](https://github.com/microsoft/PowerToys/pull/49309) | Removed the unwanted OpenLink icon after the clock band gained a primary command. | `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.TimeDate/NowDockBand.cs` | Dock-band presentation evidence only. |

## Open symptom clusters

### Compact / dialogs

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#49283](https://github.com/microsoft/PowerToys/issues/49283) | Content dialogs conflict with collapsed mode. | `Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs`; dialog host | Open; root cause unverified. |
| [#49172](https://github.com/microsoft/PowerToys/issues/49172) | Gap between palette and top of screen. | Compact window geometry | Closed duplicate; retain only as cluster breadth. |

### Dock / monitor / lifecycle

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#49295](https://github.com/microsoft/PowerToys/issues/49295) | Display 1 reported as display 2. | `Microsoft.CmdPal.UI/Services/MonitorService.cs` | Open; numbering versus identity not root-caused. |
| [#49264](https://github.com/microsoft/PowerToys/issues/49264) | Palette opened from Dock is offset. | Dock summon geometry | Open. |
| [#49205](https://github.com/microsoft/PowerToys/issues/49205) | Pin-to-dock can target a hidden dock. | Dock pin flow | Open. |
| [#49086](https://github.com/microsoft/PowerToys/issues/49086) | Dock disappears after monitor power-off. | Dock/monitor lifecycle | Open duplicate; useful as lifecycle evidence. |
| [#49281](https://github.com/microsoft/PowerToys/issues/49281) | Dock instability and frequent crashes. | Dock host | Open duplicate; no single cause established. |
| [#49082](https://github.com/microsoft/PowerToys/issues/49082) | CPU band reports 100%. | PerformanceMonitor band calculation | Open. |
| [#49078](https://github.com/microsoft/PowerToys/issues/49078) | Wrong bottom-docked position. | Dock geometry | Closed duplicate. |

### Apps / indexing

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#49212](https://github.com/microsoft/PowerToys/issues/49212) | Commands on `%PATH%` no longer run. | `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Shell/` | Open; source path is an investigation lead. |
| [#49129](https://github.com/microsoft/PowerToys/issues/49129) | Polyphonic Pinyin mismatch (`zhongqi` versus `chongqi`). | Apps/indexer matching | Open; locale-specific. |
| [#49201](https://github.com/microsoft/PowerToys/issues/49201) | Missing results. | Search/index pipeline | Closed; insufficient evidence for a distinct class. |

### PerformanceMonitor

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#49154](https://github.com/microsoft/PowerToys/issues/49154) | Enabling PerformanceMonitor immediately crashes CmdPal. | `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.PerformanceMonitor/` | Open. |
| [#49163](https://github.com/microsoft/PowerToys/issues/49163) | Battery indicator unavailable when PerfMon is soft-disabled. | Disabled-page/band production | Open; battery may move to a separate extension. |
| [#49306](https://github.com/microsoft/PowerToys/issues/49306) | Hardware cannot be detected. | PerformanceMonitor hardware discovery | Open. |
| [#49071](https://github.com/microsoft/PowerToys/issues/49071) | Bytes and binary-bytes labels use the same calculation. | Units formatting | Open. |

### Other crashes and input

| Report | Observed symptom | Investigation anchor | Caveat |
|---|---|---|---|
| [#49310](https://github.com/microsoft/PowerToys/issues/49310) | Crash when opening shortcut editor and changing system theme. | Theme handling + shortcut editor | Closed completed July 15, 2026; this ledger does not retain the fixing PR/root cause. |
| [#49270](https://github.com/microsoft/PowerToys/issues/49270) | ListView crash. | List virtualization | Open; broad anchor. |
| [#49183](https://github.com/microsoft/PowerToys/issues/49183) | Indexer Peek does not work. | `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.Indexer/` | Open. |
| [#49232](https://github.com/microsoft/PowerToys/issues/49232) | Hotkeys stopped working. | `CmdPalKeyboardService/` | Open; verify before attributing to keyboard service. |

## Evidence boundaries

- **High confidence:** compact-mode fixes, Dock page-command semantics, deferred DI accessor, invariant
  routing keys, additive SDK evolution, Apps allow-list, and Dock settings equality. Each has a merged
  PR and a source or line-level review anchor.
- **Medium/low confidence:** open Dock geometry/lifecycle, PerformanceMonitor state failures,
  ListView/theme crashes, and broad search failures. They are symptom clusters, not fixes.
- Pipeline bot chatter, reactions, and non-actionable StyleCop banter were excluded. Duplicate issues
  remain only where they establish cluster breadth.
