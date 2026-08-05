# CmdPal Bug Triage — Symptom → Likely File/Function

Use the Module Map in SKILL.md to confirm. These are **starting hypotheses**, not answers —
several areas below (Dock, PerfMon) have open, un-root-caused reports, so verify in source.

| Symptom | Start here (project · file · symbol) | Evidence |
|---|---|---|
| Dock "Open Command Palette" / pinned item doesn't summon palette | `Dock/DockControl.xaml.cs` `InvokeItem` → `IsPageCommand` | #49089 / PR #49095 |
| Item action fires while palette is collapsed (compact) | `Pages/ShellPage.xaml.cs` `ShellPage_OnPreviewKeyDown`, action-invoke path | #49113 / PR #49182 |
| Search box disappears navigating back to a list | `Pages/ShellPage.xaml.cs` `UpdateCompactModeForCurrentPage` / back-nav | #49116 |
| No way to expand a collapsed compact palette | `Pages/ShellPage.xaml.cs` `ShellPage_OnPreviewKeyDown` (Down/Tab) | PR #49177 |
| Separator/border visible when collapsed | `Pages/ShellPage.xaml` + `Helpers/BindTransformers.cs` | #49312 / PR #49313 |
| Window frame reappears after clicking another window (compact) | `MainWindow.xaml.cs` frame paint + `RedrawWindow` (`NativeMethods.txt`) | PR #49184 |
| Compact mode on when it should be off | `Microsoft.CmdPal.UI.ViewModels/SettingsModel.cs` `CompactMode` default | PR #49186 |
| Breadcrumbs / settings nav broken in non-English UI | `Settings/SettingsWindow.xaml.cs` `Navigate`, `Crumb.Data` | #45855 / PR #49253 |
| Toast in wrong position / no icon or action button | `ToastWindow.xaml.cs` `PositionWindow`/`PositionBottomCenter`; `ToastViewModel.cs` | PR #49262, #49260 |
| App/store shortcut missing from results | `ext/Microsoft.CmdPal.Ext.Apps/Programs/Win32Program.cs` `InternetShortcutURLPrefixes` | #49236 / PR #49241 |
| Dock wrong monitor / position / disappears | `Dock/DockWindow*.cs`, `DockWindowManager.cs`, `Services/MonitorService.cs` (open — verify) | #49295, #49264, #49086 |
| Dock reloads on every settings change | `Dock/` settings-changed handlers; compare with merged fix | #49168 (closed) / PR #49171 |
| PerfMon crash on enable / battery band missing | `ext/Microsoft.CmdPal.Ext.PerformanceMonitor/` (open — verify) | #49154, #49163 |
| Soft-disabled single-metric band disappears | `ext/Microsoft.CmdPal.Ext.PerformanceMonitor/` disabled placeholder path | #49159 (closed) / PR #49162 |
| A built-in feature missing entirely | check it's registered in `Microsoft.CmdPal.UI/App.xaml.cs` `AddBuiltInCommands` | — |
| Third-party extension broke after an update | `extensionsdk/Microsoft.CommandPalette.Extensions/*.idl` (non-additive change?) | PR #49260 |

**If the symptom doesn't map cleanly:** reason from the symptom and confirm in source. Do not
force-fit an open Dock/PerfMon report onto a specific file just because it's listed here.
