# Mouse Utilities — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This file owns historical evidence, chronology, review decisions, settings baselines, exclusions,
and caveats. `SKILL.md` owns the current module map, review rules, and operational guidance.
MouseUtils sub-utilities are independent; every entry names its scope.

## Regression evidence

| ID | Utility | Source/symbol | Finding and decision | Evidence/chronology |
|---|---|---|---|---|
| MU-E1 | Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs | `FindMyMouse.cpp` `StartSonar`; `MouseHighlighter.cpp` `Draw`/`BringToFront`; crosshairs positioning; `SetWindowPos(HWND_TOPMOST, SM_XVIRTUALSCREEN+1, SM_YVIRTUALSCREEN+1, SM_CXVIRTUALSCREEN-2, SM_CYVIRTUALSCREEN-2, 0)` | Exact virtual-screen layered windows were associated with DWM/taskbar transparency glitches; the accepted implementation uses a 1px inset. | [#44755](https://github.com/microsoft/PowerToys/issues/44755) |
| MU-E2 | Mouse Pointer Crosshairs; same risk class in Highlighter/Find My Mouse input paths | `InclusiveCrosshairs.cpp` `MouseHookProc` → `UpdateCrosshairsPosition` | Reports show overlay freezing/detachment when the serialized global hook chain is delayed by a hung app. Independent polling remains an unaccepted design option. | [#48442](https://github.com/microsoft/PowerToys/issues/48442), [#48360](https://github.com/microsoft/PowerToys/issues/48360) |
| MU-E3 | Find My Mouse | `FindMyMouse/dllmain.cpp` `parse_settings`, `LegacyOpacityToAlpha` (`(pct*255+50)/100`); defaults in `FindMyMouse.h` | Legacy `overlay_opacity` moved into color alpha; the retained implementation includes migration and partial-alpha defaults after the opaque-black report. | [#45321](https://github.com/microsoft/PowerToys/issues/45321) |
| MU-E4 | Mouse Pointer Crosshairs | `MousePointerCrosshairs/dllmain.cpp`, default-hotkey block checking `m_activationHotkey.key == 0` / `m_glidingHotkey.key == 0` | The report established that empty and unset were conflated, restoring defaults after an explicit clear. | [#48158](https://github.com/microsoft/PowerToys/issues/48158) |
| MU-E5 | Mouse Pointer Crosshairs, Mouse Highlighter | Global `WH_MOUSE_LL`; layered topmost overlay under RDP/VDI | The report associates hooks/capture overlays with VDI camera interference; the exact interaction remains environment-specific. | [#47242](https://github.com/microsoft/PowerToys/issues/47242) |
| MU-E6 | Find My Mouse, Mouse Pointer Crosshairs gliding cursor | Utility `resource.h`; Settings UI `.resw` | Activation text drifted or was mistranslated across native and Settings UI resource layers. | [#45598](https://github.com/microsoft/PowerToys/issues/45598), [#46223](https://github.com/microsoft/PowerToys/issues/46223) |

### MU-E7 — Mouse Highlighter quick click emitted two ripples

- **Observed:** A quick click could draw a press ripple and release pulse; held ring/drag trail
  behavior also depended on the same state split.
- **Source:** `MouseHighlighter.cpp`, `MouseHookProc` ripple branches;
  `m_leftHoldTimer`/`m_rightHoldTimer`; `HOLD_RIPPLE_TIMER_LEFT`/`_RIGHT`;
  `HOLD_RIPPLE_THRESHOLD_MS = 180`; `EmitSingleRipple`; `SpawnRippleHoldDot`;
  `FadeRippleHoldDot`. Settings: `MouseHighlighter/dllmain.cpp` keys `ripple_mode`,
  `ripple_size`, `ripple_intensity`, `ripple_duration_ms`, `ripple_show_drag_trail`,
  `ripple_show_release_pulse`; defaults in `MouseHighlighter.h`.
- **Finding:** Creating the held indicator on button-down and a release pulse on button-up produced
  two effects for a sub-threshold click.
- **Accepted decision:** The merged implementation arms the 180ms hold timer on down, emits a
  single ripple before the threshold, and uses the held-dot lifecycle after the threshold.
- **Review record:** Maintainers required the six settings to remain synchronized across native and
  Settings UI layers and selected checkbox/expander presentation for the mode-specific booleans.
- **Chronology/evidence:** [PR #48232](https://github.com/microsoft/PowerToys/pull/48232),
  including @niels9001 UX review.

## Settings baseline evidence

Verify both native defaults and C# Settings UI before changing these values.

### Find My Mouse — `FindMyMouse.h`

| Setting | Baseline |
|---|---|
| `activationMethod` | `DoubleLeftControlKey` |
| `includeWinKey` / `doNotActivateOnGameMode` | false / true |
| `backgroundColor` / `spotlightColor` | ARGB(128,0,0,0) / ARGB(128,255,255,255) |
| `spotlightRadius` / `animationDurationMs` / `spotlightInitialZoom` | 100 / 500 / 9 |
| `shakeMinimumDistance` / `shakeIntervalMs` / `shakeFactor` | 1000 / 1000 / 400% |
| Shortcut when method=`Shortcut` | Shift+Win+F |

### Mouse Highlighter — `MouseHighlighter.h`

| Setting | Baseline |
|---|---|
| `leftButtonColor` / `rightButtonColor` | ARGB(166,255,255,0) / ARGB(166,0,0,255) |
| `alwaysColor` | ARGB(0,255,0,0), alpha 0 = off |
| `radius` | 30 |
| `fadeDelayMs` / `fadeDurationMs` | 400 / 400 |
| `autoActivate` | false |
| `rippleSize` / `rippleIntensity` / `rippleDurationMs` | 60 / 0.7 / 480 |
| `rippleShowDragTrail` / `rippleShowReleasePulse` | true / true |

### Mouse Pointer Crosshairs — `InclusiveCrosshairs.h`

| Setting | Baseline |
|---|---|
| `crosshairsColor` / `crosshairsBorderColor` | ARGB(255,255,0,0) / ARGB(255,255,255,255) |
| `crosshairsOpacity` / `crosshairsRadius` / `crosshairsThickness` | 75 / 20 / 5 |
| `crosshairsBorderSize` | 1 |
| `crosshairsAutoHide` | false |
| `crosshairsIsFixedLengthEnabled` / `crosshairsFixedLength` | false / 1 |
| `crosshairsOrientation` | Both (0) |
| `autoActivate` | false |
| Activation / gliding hotkey | Win+Alt+P / Win+Alt+. |

### Mouse Jump

| Setting | Baseline/source |
|---|---|
| Activation hotkey | Win+Shift+D — `MouseJump/dllmain.cpp` |
| `previewType` | Bezelled; choices Compact / Bezelled / Custom — `MouseJumpUI/Helpers/SettingsHelper.cs` |

## Exclusion decisions

- Exclude cross-cutting VS 2026 #44304, MTP #37651, CppWinRT #45420, and routine project-file
  churn unless it yields a MouseUtils-specific decision.
- Exclude CursorWrap because it is a separate utility.
- Retain only the reusable build-hygiene decision from
  [PR #44639](https://github.com/microsoft/PowerToys/pull/44639): avoid per-project
  `PlatformToolset` and use `$(RepoRoot)`.
- Exclude check-spelling, `/azp run`, and approval-only discussion.

## Caveats

- Open issues may reflect platform behavior, remoting stacks, or app hangs rather than a locally
  fixable defect.
- Settings baselines are historical evidence; verify native and managed definitions together.
- A change in one MouseUtils executable does not automatically protect the other utilities.
