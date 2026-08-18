# Mouse Utilities — Regression Catalog & Settings Reference

Progressive-disclosure companion to SKILL.md. Grounded in `src/modules/MouseUtils/` source and the
module's real GitHub issues/PRs. Sub-utilities are independent; note which one each item concerns.

## Regression catalog

### R1 — Leftover overlay square / taskbar transparency glitch
- **Utilities:** Find My Mouse, Mouse Highlighter, Mouse Pointer Crosshairs (all full-screen overlays).
- **Where:** `FindMyMouse.cpp` `StartSonar`; `MouseHighlighter.cpp` `Draw` / `BringToFront`; crosshairs
  positioning. All use `SetWindowPos(HWND_TOPMOST, SM_XVIRTUALSCREEN+1, SM_YVIRTUALSCREEN+1,
  SM_CXVIRTUALSCREEN-2, SM_CYVIRTUALSCREEN-2, 0)`.
- **Root cause:** a transparent layered topmost window that fills the exact virtual screen makes DWM
  glitch taskbar transparency; the 1px inset avoids it.
- **Guardrail:** never remove the inset; replicate it on any new overlay-positioning call.
- **Evidence:** [#44755](https://github.com/microsoft/PowerToys/issues/44755).

### R2 — Overlay freezes / detaches when foreground app is unresponsive
- **Utility:** Mouse Pointer Crosshairs (same risk class in Highlighter / Find My Mouse input path).
- **Where:** `InclusiveCrosshairs.cpp` `MouseHookProc` (`WH_MOUSE_LL`) → `UpdateCrosshairsPosition`.
- **Root cause:** `WH_MOUSE_LL` is global and serialized through message queues; a hung app stalls the
  hook chain, so cursor-position updates stop and the crosshair "freezes"/detaches or vanishes.
- **Guardrail:** keep hook procs non-blocking; recover on next event; consider an independent
  poll/timer for position. Platform limitation — document in triage.
- **Evidence:** [#48442](https://github.com/microsoft/PowerToys/issues/48442),
  [#48360](https://github.com/microsoft/PowerToys/issues/48360).

### R3 — Opacity migrated into color alpha; missing slider → black screen
- **Utility:** Find My Mouse.
- **Where:** `FindMyMouse/dllmain.cpp` `parse_settings`, `LegacyOpacityToAlpha`
  (`(pct*255+50)/100`); defaults `FindMyMouse.h` `FromArgb(128, …)`.
- **Root cause:** legacy `overlay_opacity` percentage folded into the A channel of background/spotlight
  colors; missing migration or full-alpha read → opaque overlay.
- **Guardrail:** preserve migration; default colors keep partial alpha; test with a settings file that
  lacks the new keys.
- **Evidence:** [#45321](https://github.com/microsoft/PowerToys/issues/45321).

### R4 — Cleared shortcut silently restored to default
- **Utility:** Mouse Pointer Crosshairs (activation + gliding); pattern applies to any hotkey parse.
- **Where:** `MousePointerCrosshairs/dllmain.cpp` "set default hotkeys if not configured"
  (`m_activationHotkey.key == 0` / `m_glidingHotkey.key == 0`).
- **Root cause:** empty hotkey treated as "unset" → forced default; user cannot clear it.
- **Guardrail:** distinguish "never set" from "explicitly cleared"; mirror in C# Settings UI.
- **Evidence:** [#48158](https://github.com/microsoft/PowerToys/issues/48158).

### R5 — Global hook / capture overlay breaks VDI camera
- **Utility:** Mouse Pointer Crosshairs (and Highlighter).
- **Where:** global `WH_MOUSE_LL` + layered topmost overlay under RDP/VDI.
- **Guardrail:** install hooks only while active; keep overlay `WS_EX_TRANSPARENT`; test under VDI.
- **Evidence:** [#47242](https://github.com/microsoft/PowerToys/issues/47242).

### R6 — Localized / activation-gesture text incorrect
- **Utilities:** Find My Mouse, Mouse Pointer Crosshairs (gliding cursor).
- **Where:** utility `resource.h` + Settings-UI `.resw`.
- **Guardrail:** update both places; describe the gesture precisely (keys, press vs hold).
- **Evidence:** [#45598](https://github.com/microsoft/PowerToys/issues/45598),
  [#46223](https://github.com/microsoft/PowerToys/issues/46223).

### R7 — Ripple / fade highlight emits a double ripple on a single click (Mouse Highlighter)
- **Utility:** Mouse Highlighter (ripple / fade highlight mode).
- **Symptom:** one quick click renders **two** ripples (press ripple + release pulse) instead of one;
  or held-ring / drag-trail misbehaves while a button is held.
- **Where:** `MouseHighlighter.cpp` `MouseHookProc` `m_rippleMode` branches; hold-detection timers
  `m_leftHoldTimer`/`m_rightHoldTimer` (`HOLD_RIPPLE_TIMER_LEFT`/`_RIGHT`,
  `HOLD_RIPPLE_THRESHOLD_MS = 180`); `EmitSingleRipple` vs `SpawnRippleHoldDot`/`FadeRippleHoldDot`.
  Settings parsed in `dllmain.cpp` (`ripple_mode`/`ripple_size`/`ripple_intensity`/
  `ripple_duration_ms`/`ripple_show_drag_trail`/`ripple_show_release_pulse`); defaults in
  `MouseHighlighter.h`.
- **Root cause:** spawning the held indicator on button-down *and* a release pulse on button-up makes a
  quick click draw two ripples. The fix arms a hold-detection timer on button-down: the persistent ring
  is shown only after `HOLD_RIPPLE_THRESHOLD_MS` (180 ms); a release before then cancels the timer and
  emits exactly one self-contained ripple (`EmitSingleRipple`).
- **Guardrail:** keep the quick-click vs press-and-hold split (single click ⇒ single effect); route any
  new ripple trigger through the hold-timer path; keep the six `ripple_*` keys in `dllmain.cpp` in sync
  with `MouseHighlighter.h` defaults and the C# Settings UI. Maintainer UX review (niels9001) on the
  same PR: collapse mode-specific customization when switching modes; use a checkbox (not a toggle) for
  booleans inside a `SettingsExpander`.
- **Fix / evidence:** [PR #48232](https://github.com/microsoft/PowerToys/pull/48232) ("Ripple effect for Mouse Highlighter").

## Settings defaults (verify against C# Settings UI on any change)

### Find My Mouse (`FindMyMouse.h`)
| Setting | Default |
|---|---|
| activationMethod | `DoubleLeftControlKey` |
| includeWinKey | false |
| doNotActivateOnGameMode | true |
| backgroundColor | ARGB(128,0,0,0) |
| spotlightColor | ARGB(128,255,255,255) |
| spotlightRadius | 100 |
| animationDurationMs | 500 |
| spotlightInitialZoom | 9 |
| shakeMinimumDistance | 1000 |
| shakeIntervalMs | 1000 |
| shakeFactor | 400 (%) |
| Shortcut (when method=Shortcut) | Shift+Win+F |

### Mouse Highlighter (`MouseHighlighter.h`)
| Setting | Default |
|---|---|
| leftButtonColor | ARGB(166,255,255,0) |
| rightButtonColor | ARGB(166,0,0,255) |
| alwaysColor | ARGB(0,255,0,0) (alpha 0 = off) |
| radius | 30 |
| fadeDelayMs / fadeDurationMs | 400 / 400 |
| autoActivate | false |
| rippleSize / rippleIntensity / rippleDurationMs | 60 / 0.7 / 480 |
| rippleShowDragTrail / rippleShowReleasePulse | true / true |

### Mouse Pointer Crosshairs (`InclusiveCrosshairs.h`)
| Setting | Default |
|---|---|
| crosshairsColor | ARGB(255,255,0,0) |
| crosshairsBorderColor | ARGB(255,255,255,255) |
| crosshairsOpacity | 75 |
| crosshairsRadius | 20 |
| crosshairsThickness | 5 |
| crosshairsBorderSize | 1 |
| crosshairsAutoHide | false |
| crosshairsIsFixedLengthEnabled / crosshairsFixedLength | false / 1 |
| crosshairsOrientation | Both (0) |
| autoActivate | false |
| activation hotkey / gliding hotkey | Win+Alt+P / Win+Alt+. |

### Mouse Jump
| Setting | Default |
|---|---|
| activation hotkey | Win+Shift+D (`MouseJump/dllmain.cpp`) |
| previewType | Bezelled (Compact / Bezelled / Custom) (`MouseJumpUI/Helpers/SettingsHelper.cs`) |

## Notes on excluded noise
The MouseUtils PR history in the raw dump is dominated by cross-cutting build/test PRs (VS 2026
support #44304, MTP migration #37651, CppWinRT bump #45420, `$(RepoRoot)` paths #44639) and by
CursorWrap (a separate utility). Those touch MouseUtils `.vcxproj`/`.csproj` files but carry no
durable engineering lesson specific to the four overlay utilities and are intentionally omitted here,
except the reusable build-hygiene rule (PlatformToolset / `$(RepoRoot)`, PR #44639) surfaced in
Review Rules. `check-spelling` / `/azp run` chatter and "LGTM"-type comments were dropped.
