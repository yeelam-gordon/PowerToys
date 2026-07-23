# CmdPal Regression Catalog (fuller list)

Progressive-disclosure companion to SKILL.md. Distilled from a lean, recent slice of history (July
2026 merged PRs + review threads, plus the current bug list). **All PR/issue numbers are real and
verified via the GitHub API.** Open issues are marked *(open)* and are **not yet root-caused** —
treat them as areas, not diagnoses.

## Key decisions & conventions (from merged PRs / review threads)

- **Deferred accessor to break DI cycles.** `BuiltInsCommandProvider` must not depend on
  `IRootPageService` directly (cycle). Use `IRootPageAccessor` / `DeferredRootPageAccessor`
  (lazy `Func<>`). Reviewer also noted: if the accessor is registered in DI, let the container
  inject it rather than re-wrapping the factory. — PR #49095 (r3538570672). Files:
  `Microsoft.CmdPal.UI.ViewModels/IRootPageAccessor.cs`, `Microsoft.CmdPal.UI/DeferredRootPageAccessor.cs`
  (DI seam: `IRootPageService` → `PowerToysRootPageService`).
- **Dock summon requires a page command.** `DockControl.InvokeItem` only opens the palette when
  `IsPageCommand` is true; an invokable command returning `CommandResult.GoHome()` won't. A dock
  "home/open" item must therefore be a page command. — PR #49095, #49089.
- **Routing keys must be culture-invariant.** Localized page titles leaked into `Crumb.Data` /
  `Navigate()` keys, breaking Settings breadcrumbs in non-English locales. Use constants for tags. —
  PR #49253, #45855.
- **Extension SDK evolves additively.** New toast options were added via `IToastArgs2` (based on
  `IToastArgs`), backward compatible both ways; toolkit `ToastArgs` implements it; a sample was
  added. Out-of-proc WinRT contract — never mutate an existing interface. — PR #49260.
- **Compact mode defaults off.** `SettingsModel.CompactMode` intended default is `false`
  (shipped `true` by mistake once). — PR #49186.
- **Toast position is user-configurable** and can follow the OS setting (read from registry);
  transition effect changes with position via `PositionWindow(ToastPosition)`. — PR #49262.
- **Prefer `[GeneratedRegex]`** for allow-list patterns (readability) when extending app discovery. —
  PR #49241.
- **User32 frame hacks need re-assertion.** DWM repaints the non-client frame on focus change;
  compact mode calls `RedrawWindow` repeatedly to keep it transparent. — PR #49184.

## Regression classes (merged fixes)

### Compact / collapsed mode (the dominant cluster)
| Issue/PR | What broke | Fix location |
|---|---|---|
| #49113 → PR #49182 | actions executed on hidden selected item while collapsed | `Pages/ShellPage.xaml.cs` |
| #49116 | search box vanished on back-nav (content → list) in compact | `Pages/ShellPage.xaml.cs` |
| PR #49177 | added Down/Tab to expand collapsed palette | `Pages/ShellPage.xaml.cs` |
| PR #49184 | HWND frame reappeared after focus loss | `MainWindow.xaml.cs`, `NativeMethods.txt` |
| PR #49186 | compact wrongly defaulted on | `SettingsModel.cs` |
| #49312 → PR #49313 | separator visible when collapsed | `ShellPage.xaml`, `Helpers/BindTransformers.cs` |
| #49283 *(open)* | content dialogs + collapsed mode conflict | ShellPage / dialog host — verify |
| #49172 *(dup, closed)* | gap between palette and top of screen | — |

### Dock
| Issue/PR | What | Location / status |
|---|---|---|
| #49089 → PR #49095 | Open-Command-Palette dock button dead | `Dock/DockControl.xaml.cs` |
| #49168 → PR #49171 | Dock rebuilt on *every* settings change (record held `ImmutableList` = reference equality) | fix: `EquatableList<T>` backing fields + `_settings == args.DockSettings` guards (`Settings/DockSettings.cs`, `Settings/EquatableList`1.cs`, Dock/MainWindow handlers) |
| #49309 (PR, merged) | clock band showed unwanted OpenLink icon after primary command added | `src/modules/cmdpal/ext/Microsoft.CmdPal.Ext.TimeDate/NowDockBand.cs` |
| #49295 *(open)* | reports display 1 as display 2 | `Services/MonitorService.cs` — verify |
| #49264 *(open)* | palette opened from Dock has offset | Dock summon geometry — verify |
| #49205 *(open)* | pin-to-dock pins to a hidden dock | Dock pin flow — verify |
| #49086 *(open, dup)* | dock disappears after monitor power-off | Dock/monitor lifecycle — verify |
| #49281 *(open, dup)* | dock unstable, frequent crashes | Dock — verify |
| #49082 *(open)* | CPU usage shown as 100% | PerfMon band calc — verify |
| #49078 *(closed, dup)* | wrong position docked bottom | Dock geometry |

### Apps / discovery
| Issue/PR | What | Location |
|---|---|---|
| #49236 → PR #49241 | Origin/EA/UPlay/Xbox `.url` shortcuts not recognized as apps | `ext/...Apps/Programs/Win32Program.cs` |
| #49212 *(open)* | no longer runs commands on `%PATH%` | `ext/...Shell/` — verify |
| #49201 *(closed)* | missing results | — |
| #49129 *(open)* | polyphonic Pinyin mismatch (zhongqi vs chongqi) | Apps/indexer matching — verify (i18n) |

### PerformanceMonitor
| Issue | What | Status |
|---|---|---|
| #49154 *(open)* | enabling PerfMon immediately crashes CmdPal | `ext/...PerformanceMonitor/` |
| #49163 *(open)* | battery indicator unavailable when perfmon soft-disabled | verify |
| #49159 → PR #49162 | soft-disabled PerfMon single-metric Dock bands vanished | fix: emit a disabled placeholder band per metric (`BandMetrics`, `PerformanceWidgetsPage.GetBandId`, `PerformanceMonitorDisabledPage`) |
| #49306 *(open)* | can't detect hardware | verify |
| #49071 *(open)* | Bytes vs Binary bytes same calc, different label | units formatting |

### Other
| Issue/PR | What | Location |
|---|---|---|
| #45855 → PR #49253 | breadcrumbs broken in non-English locales | `Settings/SettingsWindow.xaml.cs` |
| PR #49260 | toast icon + action button (additive SDK) | `ToastWindow.*`, `extensionsdk/*.idl` |
| PR #49262 | toast window position setting | `ToastWindow.xaml.cs`, `SettingsModel.cs` |
| PR #49266 | Settings/Help in search-bar context menu | `Controls/SearchBar.xaml(.cs)` |
| #49310 *(open)* | crash opening shortcut editor + changing system theme | theme + editor — verify |
| #49270 *(open)* | crash from ListView | list virtualization — verify |
| #49183 *(open)* | Indexer peek not working | `ext/...Indexer/` |
| #49232 *(open)* | hotkeys stopped working | `CmdPalKeyboardService/` — verify |

## Excluded as noise (not distilled)
- StyleCop banter ("gods I hate stylecop"), `/azp run` CI chatter, pipeline bot comments,
  "I'm digging it" reactions — no durable lesson (review_comments/conversation JSON).
- One StyleCop nit *was* kept as a convention (don't init to default) because it recurs across PRs.
- Pure duplicates (#49078, #49172, #49086, #49281 flagged `duplicate`) are listed only to map the
  Dock cluster's breadth, not as separate root causes.

## Confidence
- **High:** compact-mode cluster, Dock page-command semantics, DI deferred-accessor, i18n routing
  key, additive SDK rule, Apps allow-list — each backed by a merged PR and/or line-level review.
- **Medium/low (areas, not fixes):** open Dock geometry/lifecycle, PerfMon states, ListView/theme
  crashes — many open, un-root-caused reports; confirm in source before acting.
