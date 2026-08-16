# Shortcut Guide — Bug Triage (symptom → likely file/function)

Use the Module Map as **hypotheses to confirm in source**, not ground truth. Root:
`src/modules/ShortcutGuide/`. This is the v0.100+ **WinGet-manifest** architecture, not the
legacy overlay.

| Symptom | Start here | Likely cause / check |
|---|---|---|
| Crashes / closes when clicking between sidebar sections | `MainPaneControl.WindowSelector_SelectionChanged`; `OverlayWindow.OnMainPaneTaskbarVisibilityChanged`, `UpdateTaskbarPaneLayout` | Current architecture has one native overlay; investigate pane events/layout without applying the removed `App.TaskBarWindow` cause (#48448/#48481 is historical). |
| Crashes immediately on launch | `OverlayWindow` ctor `Title`; `Program.cs Main` | empty native title faulting `TitleBar` (#49069); or index/manifest initialization exception |
| Opens empty then closes | Logs and `OverlayWindow.CloseType`; then `Program.cs CopyAndIndexGenerationThread`, `IndexYmlGenerator.cs`, and `MainPaneControl.InitializeNavItemsAsync` | Cause is ambiguous. Check for a non-zero generator exit or YAML parse error before assigning a manifest cause; #49131/#48892 do not establish corrupt YAML. |
| Still shows the **old** Shortcut Guide | packaging/registration; `dllmain.cpp StartProcess` path (`WinUI3Apps\PowerToys.ShortcutGuide.exe`) | stale install / wrong exe launched (#48462) |
| Slow to appear | `Program.cs` background thread; `ManifestInterpreter.GetAllCurrentApplicationIds` (process enumeration) | manifest copy + index generation + process scan on the critical path (#49200) |
| Wrong / duplicated shortcut label | manifest `*.en-US.yml` (e.g. `+WindowsNT.Shell`) | OS/SKU drift, e.g. Win+Q on Copilot+ PCs (#48427/#48439); ambiguous Home-screen values (#44830) |
| Number-key shortcut renders wrong / blank | manifest `Keys:`; `ShortcutDescriptionToKeysConverter.cs`, `KeyVisual.xaml.cs` | bare digit read as VK code; must be `<N>` (#48757) |
| Special key (Delete/Tab/…) not rendering | manifest `Keys:`; `KeyVisual.xaml.cs` token map | missing `<...>` spec token (#48959/#48960) |
| Win+number **taskbar** shortcuts missing | manifest `<TASKBAR1-9>` section; Win-key `TaskbarIndicators` setting; `TaskbarPaneControl.UpdateTasklistButtons`, `TasklistPositions.cs` | distinguish selected-app section visibility from the settings-driven taskbar-only path; then inspect taskbar button enumeration (#44474) |
| Overlay misplaced with moved/vertical taskbar | `OverlayWindow.RepositionToCursorMonitor`, `ApplyMainPaneAlignment`, `UpdateTaskbarPaneLayout`; `DisplayHelper.cs` | work-area or taskbar-edge layout math (#48435) |
| Shortcut not listed at all for an app | `GetAllCurrentApplicationIds`, `IndexYmlGenerator.cs` | `WindowFilter` mismatch (exact exe or `*` only); `BackgroundProcess` flag |
| Spurious "shortcut conflicts" on Home screen | `PinnedShortcutsHelper.cs`; Settings Home integration | duplicate/ambiguous entries (#44141, #44830) |
| Doesn't launch under GPO | `dllmain.cpp gpo_policy_enabled_configuration`; `Program.cs` GPO check | one of the two gates |

## Triage steps

1. Reproduce and capture the exact close reason. `OverlayWindow` sets `_closeType`
   (`Escape` / `CloseButton` / `ClickOutside` / `Deactivated` / `InitializationFailed` / `Unknown`) and logs errors — check the
   Shortcut Guide logs first.
2. Determine the layer: **manifest data** (YAML), **UI/overlay** (WinUI C#), **index/startup**
   (Program.cs + IndexYmlGenerator), or **activation** (C++ module interface).
3. Confirm the culprit in source before editing; if the symptom doesn't fit a row, reason from
   the log/close reason rather than forcing a map row.
