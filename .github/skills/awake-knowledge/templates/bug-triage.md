# Awake Bug Triage: Symptom → Likely File/Function

Use the Module Map in SKILL.md as **hypotheses to confirm in source**, not ground truth. If the
symptom doesn't map cleanly below, reason from the symptom and verify in code.

| Symptom | First place to look | Notes / evidence |
|---|---|---|
| System still sleeps in indefinite mode (laptop) | `Manager.ComputeAwakeState`; check "keep display on" | Likely Modern Standby S0 platform limit, not a code bug (#44458, #44286). |
| Keep-awake lost after sleep/resume | `TrayHelper.WndProc` `WM_POWERBROADCAST` → `Manager.ReapplyAwakeState` | State dropped across power transition; must re-arm (#44642, PR #44795). |
| Timed mode ends before the set duration | `Manager.SetTimedKeepAwake` conversion; `Program.ProcessSettings` timed branch | Truncated partial minutes; round up (PR #43785). |
| Tray countdown drifts over time | `Manager.SetTimedKeepAwake` Rx pipeline | Use absolute `targetExpiryTime` (PR #41684). |
| Expirable mode with a past date behaves oddly | `Manager.SetExpirableKeepAwake`; `Program.ProcessSettings` EXPIRABLE branch | Non-future target not armed; config bumped +5 min (#46349). |
| No output for `--help` / bad CLI arg | `Program.Main` console attach + early exit; `AllocateLocalConsole`; `Bridge.FreeConsole` | WinExe console attach; FreeConsole before AllocConsole (PR #41774). |
| `--pid` accepts invalid/nonexistent PID | `Program.pidOption.AddValidator`; `ProcessExists` | Validate at parse time (PR #41774). |
| Wrong/stale tray icon | `Manager.SetModeShellIcon`; `TrayHelper.SetShellIcon`; `TaskbarCreated` handling | Missing update after mode change or Explorer restart (#46079). |
| Tray menu pops in wrong place / won't dismiss | `TrayHelper.ShowContextMenu` | `SetForegroundWindow` + `TrackPopupMenuEx` flags (PR #41009). |
| Tray time not in 24h / wrong locale | `Manager.SetModeShellIcon` time formatting; culture | Culture-dependent formatting (#47359, #48259). |
| Display stays off though "keep screen on" set | `Manager.SetDisplay`; `ComputeAwakeState` display flag | TIMED special-case preserves timer (#47023). |
| Custom tray time does nothing | `TrayHelper.WndProc` `WM_COMMAND` default (TC_TIME range); `CustomTrayTimes` | Index vs. `CustomTrayTimes` count bounds. |
| Second instance won't start / already running | `Program.Main` `LockMutex` (named mutex) | Single-instance guard. |
