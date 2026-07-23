# FancyZones Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table.

## Report
- **Symptom:**
- **Repro / inputs:**
- **OS build / Win10 vs Win11:**
- **Monitor count / DPI / virtual desktops:**
- **Relevant settings:** override snap? move-to-last-zone? span across monitors? shift/ctrl drag?

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Crash/hang on display or monitor change | `ZonesOverlay.cpp` dtor; `WorkArea.cpp::~WorkArea`; `OnThreadExecutor.cpp` dtor | Teardown race | Shutdown races |
| Crash toggling "Span zones across monitors" mid-drag | `FancyZones.cpp::UpdateWorkAreas` + `WorkAreaConfiguration::Clear` | Dangling `WorkArea*` | Shutdown races |
| Overlays stay on screen after closing a window mid-drag | `FancyZones.cpp` `WM_PRIV_WINDOWDESTROYED`; `WindowMouseSnap::Abort` | Stuck drag | Stuck drag |
| Number keys swallowed / layout switches unexpectedly | `FancyZones.cpp::OnKeyDown` (dragging + digit) | Stuck drag / key steal | Stuck drag |
| Shift key stops working while typing | `FancyZones.cpp::OnKeyDown` (Shift swallow) | Over-broad swallow | Stuck drag |
| Win+arrow snaps to native half-screen / extra hotkeys | `FancyZones.cpp::ShouldProcessSnapHotkey`; `Settings.overrideSnapHotkeys`; `WindowKeyboardSnap.cpp` | Override snap | Override snap |
| All app windows pile into one zone on restore | `AppZoneHistory.cpp::GetAppLastZoneIndexSet` | Per-app history key | Last known zone |
| App opens blank/black on multi-monitor restore | `AppZoneHistory.cpp`; `FancyZones.cpp` new-window handling | Multi-monitor restore | Last known zone |
| Different layouts per Win11 desktop stop working | `AppliedLayouts.cpp`; `VirtualDesktop.cpp`; `AppZoneHistory::SyncVirtualDesktops` | VD id resolution | VD / JSON |
| `applied-layouts.json` access denied | `AppliedLayouts.cpp` JSON write | Concurrent/locked write | VD / JSON |
| Editor won't open / wrong monitors | `EditorParameters.cpp`; `MonitorUtils.cpp::IdentifyMonitors`; editor `MainWindow.xaml.cs` | Editor handoff | Module Map |
| Spacing / highlight distance looks wrong | editor `Models/`, `Utils/`; native spacing consumer | Layout math | Review Rules |
| CLI `{GUID}` fails in PowerShell | `FancyZonesCLI/CommandLine/Commands/` | Script-block parse | CLI GUID |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
3. Reproduce with the reporter's monitor/DPI/virtual-desktop and settings.
4. Add/extend a test where a harness exists; otherwise document manual validation.
