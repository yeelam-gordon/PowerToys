# AlwaysOnTop Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / inputs:**
- **OS / build (Win10 vs Win11, 22H2/25H2):**
- **Target app (custom titlebar? UWP? elevated?):**
- **Feature involved (pin / border / opacity / system menu):**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Other app's title-bar right-click menu broken/glitched | `AlwaysOnTop.cpp` `UpdateSystemMenuItem`, `HandleWinHookEvent` | System-menu foreign mutation | System-menu integration |
| `TrackPopupMenu` ERROR_INVALID_MENU_HANDLE with PT running | `UpdateSystemMenuItem`; `SubscribeToEvents` hooks | System-menu | System-menu integration |
| Toggle item duplicated / wrong command | `UpdateSystemMenuItem`, `IsAlwaysOnTopMenuCommand` | Command-ID collision | Command-ID collision |
| Settings change not applied while running | `Settings.cpp` `LoadSettings`/`InitFileWatcher`; `settings()` | File-watcher race | Settings live-apply |
| Opacity +/- keys do nothing (numpad) | `dllmain.cpp` `parse_hotkey`/`get_hotkeys`; `Settings` opacity hotkeys | Hotkey VK mismatch | Opacity hotkeys |
| Opacity shortcut conflicts on localized keyboard | `get_hotkeys`, `Settings.h` opacity hotkeys | Hotkey conflict | Opacity hotkeys |
| Opacity not restored after unpin / window stays translucent | `AlwaysOnTop.cpp` `RestoreWindowAlpha`/`ApplyWindowAlpha` | Layered-window restore | Transparency restore |
| Crash on/around border refresh | `WindowBorder.cpp` `UpdateBorderPosition` | Null FrameDrawer | Border null-deref |
| Doesn't work on admin/elevated app | pin path (`SetWindowPos`/hooks) | UIPI / elevation | Elevated windows |
| Border missing on some virtual desktop | `AlwaysOnTop.cpp` `AssignBorder`/`RefreshBorders`; `VirtualDesktopUtils` | Desktop tracking | Module Map |
| Rounded corners square on Win10 | `WindowCornersUtil.cpp` `CornersRadius` | DWM Win11-only API | (Gotchas) |
| Default border color differs from Settings UI | `Settings.h` vs `AlwaysOnTopProperties.cs` | C++/C# drift | Review Rules |
| Pin applies to random windows | `dllmain.cpp` `on_hotkey`; `RegisterLLKH` events | Hotkey/event routing | Module Map |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
3. Reproduce with the reporter's inputs (note OS build, target app titlebar type, elevation).
4. Add/extend a settings-serialization or behavior test before fixing where applicable.
