# Mouse Utilities — Bug Triage (symptom → likely file/function)

Use the Module Map in SKILL.md as hypotheses to confirm in source. First identify which of the four
sub-utilities the report is about (settings key / screenshot / described gesture), then localize.

| Symptom | Sub-utility | Start here (file · symbol) |
|---|---|---|
| Leftover square / taskbar transparency flicker on show/dismiss | any overlay | `SetWindowPos` sizing — `FindMyMouse.cpp` `StartSonar`, `MouseHighlighter.cpp` `Draw`/`BringToFront`, crosshairs equivalent (verify 1px inset) |
| Crosshair frozen / detached / disappears when a window hangs | Crosshairs | `InclusiveCrosshairs.cpp` `MouseHookProc` (`WH_MOUSE_LL`) → `UpdateCrosshairsPosition` |
| Spotlight/dim is solid black; opacity slider "missing" | Find My Mouse | `FindMyMouse/dllmain.cpp` `parse_settings`, `LegacyOpacityToAlpha`; defaults `FindMyMouse.h` |
| Activation gesture misfires (double-Ctrl / shake) | Find My Mouse | `FindMyMouse.cpp` `OnSonarKeyboardInput`, `OnSonarMouseInput`/`DetectShake`, `KeyboardInputCanActivate` |
| Doesn't activate in a game / specific app | Find My Mouse | `StartSonar` (`detect_game_mode`), `IsForegroundAppExcluded` (`check_excluded_app`) |
| Highlighter dot wrong color / missing on click | Highlighter | `MouseHighlighter.cpp` `AddDrawingPoint`, `m_leftClickColor`/`m_rightClickColor`/`m_alwaysColor` |
| Ripple looks wrong (double ripple, no release pulse) | Highlighter | `SpawnRippleHoldDot`, `FadeRippleHoldDot`, `EmitSingleRipple`; defaults `MouseHighlighter.h` |
| Spotlight mode mask wrong size / edge | Highlighter | `UpdateSpotlightMask`, `SpotlightAnimatePress/Release` |
| Mouse Jump preview wrong / teleport to wrong point | Mouse Jump | `MouseJumpUI/MainForm.cs` click→`MouseHelper.SetCursorPosition`; `MouseJump.Common/Helpers/LayoutHelper.cs`, `ScreenHelper.cs` |
| Mouse Jump preview style/type wrong | Mouse Jump | `MouseJumpUI/Helpers/SettingsHelper.cs` (`PreviewType` Compact/Bezelled/Custom) |
| Mouse Jump won't launch on hotkey | Mouse Jump | `MouseJump/dllmain.cpp` `m_hotkey`, `ShellExecuteExW("PowerToys.MouseJumpUI.exe")` |
| Crosshair color / thickness / opacity not applied live | Crosshairs | `InclusiveCrosshairs.cpp` `ApplySettings`; parse in `MousePointerCrosshairs/dllmain.cpp` |
| Gliding cursor doesn't move / can't cancel | Crosshairs | `dllmain.cpp` `HandleGlidingHotkey`, `PositionCursorX/Y`, `CancelGliding`, `WH_KEYBOARD_LL` |
| A shortcut can't be cleared | Crosshairs (or any) | `dllmain.cpp` hotkey parse + "set default hotkeys if not configured" block |
| Overlay breaks camera/capture under RDP/VDI | Crosshairs/Highlighter | global `WH_MOUSE_LL` + layered topmost overlay window styles |
| Wrong/confusing activation label or translation | any | utility `resource.h` + Settings-UI `.resw` |

## Cross-cutting checks
- Confirm the utility is even active (own settings key / enable flag) before blaming render code.
- On high-DPI or multi-monitor, verify `RasterizationScale()` / `SM_*VIRTUALSCREEN` math.
- If the symptom is a freeze, suspect the global low-level hook chain before the render path.
