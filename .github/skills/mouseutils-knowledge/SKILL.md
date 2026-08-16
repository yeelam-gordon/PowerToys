---
name: mouseutils-knowledge
description: 'PowerToys Mouse Utilities module knowledge for its four sub-utilities — Find My Mouse (spotlight/sonar), Mouse Highlighter (click highlight / ripple / spotlight), Mouse Jump (screen-preview teleport), and Mouse Pointer Crosshairs (+ gliding cursor). Covers per-utility feature->file/function maps, activation gestures/hotkeys, overlay rendering (WinAppSDK Composition + WH_MOUSE_LL / raw input), multi-monitor/DPI virtual-screen math, settings defaults kept in sync between C++ and the C# Settings UI, and recurring regression playbooks (leftover overlay square, overlay freezes when foreground app hangs, opacity->alpha migration, cleared shortcuts, localization). Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/MouseUtils.'
license: Complete terms in LICENSE.txt
---

# PowerToys Mouse Utilities Knowledge

Grounded engineering knowledge for the PowerToys **Mouse Utilities** module (`src/modules/MouseUtils/`),
a container for four independent overlay utilities that help locate/emphasize the mouse pointer:

- **Find My Mouse** — full-screen dim + spotlight ("sonar") around the cursor, triggered by a gesture.
- **Mouse Highlighter** — colored circles on click, plus Ripple and Spotlight click modes.
- **Mouse Jump** — a shrunken multi-monitor screenshot; click to teleport the cursor there.
- **Mouse Pointer Crosshairs** — full-length crosshair lines through the cursor, plus a "gliding cursor".

Each ships as its **own executable/module interface** with its **own settings key and hotkeys**. They
share patterns (WinAppSDK Composition overlays, global low-level hooks, virtual-screen sizing) but do
**not** share code, so a change in one rarely affects another. `CursorWrap` also lives under this
directory but is a separate utility and out of scope here.

## When to Use This Skill

- Planning or implementing a change under `src/modules/MouseUtils/` and needing prior art.
- Fixing/triaging a Mouse Utilities bug: overlay not appearing/disappearing, leftover square on
  dismiss, crosshair frozen/detached from cursor, spotlight opacity wrong (black screen), a shortcut
  that can't be cleared, gliding cursor not moving, activation gesture misfiring, wrong colors.
- Reviewing a Mouse Utilities PR against maintainer conventions and regression traps.
- Touching activation gestures/hotkeys, the Composition overlay render path, the `WH_MOUSE_LL` /
  raw-input plumbing, multi-monitor/DPI virtual-screen math, or the settings parse/defaults that must
  mirror the C# Settings UI.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

### Find My Mouse (`FindMyMouse/`) — C++ / WinAppSDK Composition
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Module interface, enable/disable, settings load, hotkey parse | `FindMyMouse/dllmain.cpp` `FindMyMouse` class, `init_settings`, `parse_settings`, `GetHotkeyEx`/`OnHotkeyEx` (returns `std::optional<HotkeyEx>`; `m_hotkey`) |
| Legacy `overlay_opacity` % → color alpha migration | `dllmain.cpp` `LegacyOpacityToAlpha` (applied to background/spotlight color A channel) |
| Activation state machine + raw input snoop | `FindMyMouse.cpp` `SuperSonar<D>` `OnSonarKeyboardInput` (double-Ctrl), `OnSonarMouseInput`/`DetectShake` (shake), `StartSonar`/`StopSonar`, `UpdateMouseSnooping` (`RegisterRawInputDevices` `RIDEV_INPUTSINK`) |
| Activation methods enum | `FindMyMouse.h` `FindMyMouseActivationMethod` {`DoubleLeftControlKey`=0 (default), `DoubleRightControlKey`, `ShakeMouse`, `Shortcut`} |
| Spotlight overlay render (DWM redirection, DPI) | `FindMyMouse.cpp` `CompositionSpotlight : SuperSonar` `OnCompositionCreate`, `SetSonarVisibility`, `AfterMoveSonar` (uses `XamlRoot().RasterizationScale()`) |
| Game-mode / excluded-app gate | `FindMyMouse.cpp` `StartSonar` (`detect_game_mode`), `IsForegroundAppExcluded` (`check_excluded_app`) |
| Settings struct + defaults | `FindMyMouse.h` `FindMyMouseSettings` (radius 100, anim 500ms, shake factor 400%, etc.) |

### Mouse Highlighter (`MouseHighlighter/`) — C++ / WinAppSDK Compositor
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Module interface, settings, hotkey | `MouseHighlighter/dllmain.cpp` `MouseHighlighter` class, `get_key`, `parse` |
| Global click capture | `MouseHighlighter.cpp` `MouseHookProc` / `SetWindowsHookEx(WH_MOUSE_LL, …)` |
| Overlay window + DispatcherQueue/Compositor/DesktopWindowTarget | `MouseHighlighter.cpp` `CreateHighlighter`, `MyRegisterClass` (ex-style `WS_EX_TRANSPARENT\|WS_EX_LAYERED\|WS_EX_NOREDIRECTIONBITMAP\|WS_EX_TOOLWINDOW`) |
| Click-fade mode (left/right colored dots) | `AddDrawingPoint`, `StartDrawingPointFading`, `UpdateDrawingPointPosition` |
| Spotlight mode (radial mask, press animation) | `UpdateSpotlightMask`, `SpotlightAnimatePress`, `SpotlightAnimateRelease` |
| Ripple mode (hold ring + glow, release pulse, drag trail) | `SpawnRippleHoldDot`, `FadeRippleHoldDot`, `EmitSingleRipple` |
| Settings struct + defaults | `MouseHighlighter.h` (`radius` 30, delay/duration 400ms, ripple size 60/intensity 0.7/480ms; `alwaysColor` default **alpha 0**) |

### Mouse Jump (`MouseJump/`, `MouseJumpUI/`, `MouseJump.Common/`) — C++ launcher + C#/.NET WinForms
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Native module interface, hotkey, launch UI process | `MouseJump/dllmain.cpp` `MouseJump` class (`m_hotkey` default **Win+Shift+D**), `ShellExecuteExW("PowerToys.MouseJumpUI.exe")` |
| Preview form: show, click-to-teleport, Esc dismiss | `MouseJumpUI/MainForm.cs` `OnKeyDown`(Escape), click → `MouseHelper.SetCursorPosition`, `OnDeactivate` |
| Entry point / settings watch | `MouseJumpUI/Program.cs`, `MouseJumpUI/Helpers/SettingsHelper.cs` (`PreviewType` Compact/Bezelled/Custom), `ThrottledActionInvoker` |
| Layout / drawing / screen geometry | `MouseJump.Common/Helpers/` (`LayoutHelper`, `DrawingHelper`, `ScreenHelper`, `MouseHelper`, `StyleHelper`) |
| Screenshot capture services | `MouseJump.Common/Imaging/DesktopImageRegionCopyService.cs`, `StaticImageRegionCopyService.cs` (`IImageRegionCopyService`) |
| Unit tests (this is the only utility with them) | `MouseJump.Common.UnitTests/` (`LayoutHelperTests`, `DrawingHelperTests`, `MouseHelperTests`, `RectangleInfoTests`) |

### Mouse Pointer Crosshairs (`MousePointerCrosshairs/`) — C++ / WinAppSDK Composition
| Sub-feature | Implementation (file · symbol) |
|---|---|
| Module interface, **two** hotkeys, settings parse | `MousePointerCrosshairs/dllmain.cpp` `get_hotkeys`/`on_hotkey` (id 0 crosshairs = **Win+Alt+P**, id 1 gliding = **Win+Alt+.**) |
| Gliding cursor (auto-move + click), Escape cancel | `dllmain.cpp` `HandleGlidingHotkey`, `CancelGliding`, `PositionCursorX/Y` worker threads (`SendInput`), `LowLevelKeyboardProc`/`SetWindowsHookEx(WH_KEYBOARD_LL)` |
| Overlay render + mouse tracking | `InclusiveCrosshairs.cpp` `CreateInclusiveCrosshairs` (Compositor/DesktopWindowTarget), `MouseHookProc`/`WH_MOUSE_LL`, `UpdateCrosshairsPosition` |
| Apply settings live, auto-hide, orientation | `InclusiveCrosshairs.cpp` `ApplySettings`, `SetAutoHideTimer`, `SwitchActivationMode` |
| Settings struct + defaults | `InclusiveCrosshairs.h` `InclusiveCrosshairsSettings` (opacity 75, thickness 5, radius 20, `CrosshairsOrientation` Both/Vertical/Horizontal, fixed-length off) |

**Shared virtual-screen HACK (all overlay utilities):** the overlay is sized **1px inset** from the
virtual screen — `SetWindowPos(HWND_TOPMOST, SM_XVIRTUALSCREEN+1, SM_YVIRTUALSCREEN+1, SM_CXVIRTUALSCREEN-2, SM_CYVIRTUALSCREEN-2)`.
The comment is explicit: *"Draw with 1 pixel off. Otherwise, Windows glitches the taskbar transparency
when a transparent window fills the whole screen."* (`FindMyMouse.cpp` `StartSonar`; `MouseHighlighter.cpp`
×3; crosshairs equivalent). Do **not** "clean this up" to a full-screen rect.

## Regression Playbooks

Rule by rule: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Leftover overlay square / taskbar transparency glitch on show/dismiss
- **Symptom:** a random square or taskbar transparency flicker appears when the spotlight/overlay
  shows or dismisses. Evidence: [#44755](https://github.com/microsoft/PowerToys/issues/44755).
- **Where:** every overlay's `SetWindowPos(...HWND_TOPMOST...)` sizing (`FindMyMouse.cpp` `StartSonar`,
  `MouseHighlighter.cpp` `Draw`/`BringToFront`, crosshairs equivalent).
- **Root cause:** a transparent, layered, topmost window covering the **entire** virtual screen makes
  DWM glitch taskbar transparency; the mitigation is the 1px inset.
- **Guardrail:** keep the `+1 / -2` virtual-screen inset on ALL of these calls; if you add a new
  overlay-positioning call, replicate the inset. Never expand to the exact virtual-screen rect.

### Crosshair freezes / detaches from cursor when a window hangs
- **Symptom:** crosshair stops following the cursor (freezes/detaches) when the underlying window is
  unresponsive/loading; crosshair sometimes disappears. Evidence:
  [#48442](https://github.com/microsoft/PowerToys/issues/48442),
  [#48360](https://github.com/microsoft/PowerToys/issues/48360).
- **Where:** `InclusiveCrosshairs.cpp` `MouseHookProc` (`WH_MOUSE_LL`) → `UpdateCrosshairsPosition`;
  same class of risk in Highlighter's `WH_MOUSE_LL` and Find My Mouse raw-input timer.
- **Root cause:** `WH_MOUSE_LL` is a **global, serialized** hook; if any app on the desktop stalls its
  message pump the low-level hook chain stalls, so position updates stop arriving. Position is driven
  by the hook, not an independent timer.
- **Guardrail:** don't do slow/blocking work inside the hook proc (return via `CallNextHookEx`
  promptly); treat missed updates as expected and recover on the next event. Consider a lightweight
  poll/timer fallback for position if fixing freeze. This is a Windows platform limitation, not a
  simple bug — document it when triaging.

### Mouse Highlighter ripple/fade highlight emits a double ripple on a single click
- **Symptom:** with the Mouse Highlighter **ripple** ("ripple"/fade highlight) mode on, one quick
  click renders **two** ripple effects (a press ripple *and* a release pulse) instead of one; or the
  held ring / drag-trail behaves wrong while a button is held. Feature/fix: **[PR #48232](https://github.com/microsoft/PowerToys/pull/48232)** ("Ripple effect for Mouse Highlighter").
- **Where:** `MouseHighlighter/MouseHighlighter.cpp` — the `m_rippleMode` branches in `MouseHookProc`
  (button-down/up), the hold-detection timers `m_leftHoldTimer` / `m_rightHoldTimer`
  (`HOLD_RIPPLE_TIMER_LEFT`/`_RIGHT`, `HOLD_RIPPLE_THRESHOLD_MS = 180`), and
  `EmitSingleRipple` vs `SpawnRippleHoldDot`/`FadeRippleHoldDot`. Settings are plumbed in
  `MouseHighlighter/dllmain.cpp` (`ripple_mode`, `ripple_size`, `ripple_intensity`,
  `ripple_duration_ms`, `ripple_show_drag_trail`, `ripple_show_release_pulse`) with defaults in
  `MouseHighlighter.h`.
- **Root cause:** a naive implementation spawns the persistent "held indicator" on button-down *and*
  emits a release pulse on button-up, so a quick click draws two ripples. The fix arms a hold-detection
  timer on button-down: the persistent ring (`SpawnRippleHoldDot`) is shown **only** if the button is
  held past `HOLD_RIPPLE_THRESHOLD_MS` (180 ms); a release before the threshold cancels the timer and
  emits exactly **one** self-contained ripple via `EmitSingleRipple`.
- **Guardrail:** keep the quick-click vs press-and-hold split — never spawn the held ring *and* a
  release pulse for the same sub-threshold click; route any new ripple trigger through the hold-timer
  path. Keep the six `ripple_*` keys parsed in `dllmain.cpp` in sync with `MouseHighlighter.h` defaults
  and the C# Settings UI (single click ⇒ single effect; see [PR #48232](https://github.com/microsoft/PowerToys/pull/48232)).

### Find My Mouse opacity slider missing → solid black screen
- **Symptom:** after an update, the opacity slider disappears and the overlay dims to full black /
  solid spotlight instead of a soft dim. Evidence:
  [#45321](https://github.com/microsoft/PowerToys/issues/45321).
- **Where:** `FindMyMouse/dllmain.cpp` `parse_settings` + `LegacyOpacityToAlpha`; defaults in
  `FindMyMouse.h` (`FromArgb(128, …)`).
- **Root cause:** opacity moved from a standalone `overlay_opacity` percentage into the **alpha
  channel** of the background/spotlight colors. If legacy opacity isn't migrated (or a color is read
  with full alpha), the dim becomes opaque.
- **Guardrail:** when touching color/opacity parsing, preserve the legacy→alpha migration path
  (`LegacyOpacityToAlpha` with rounding `*255+50/100`), and keep default colors at partial alpha
  (128). Verify against a settings file that lacks the new keys.

### A Mouse Utilities shortcut can't be cleared
- **Symptom:** user cannot remove/clear a shortcut (e.g. the crosshairs "move/gliding cursor"
  shortcut). Evidence: [#48158](https://github.com/microsoft/PowerToys/issues/48158).
- **Where:** `MousePointerCrosshairs/dllmain.cpp` hotkey parse + the "set default hotkeys if not
  configured" block (`m_activationHotkey.key == 0` / `m_glidingHotkey.key == 0` → forced defaults).
- **Root cause:** an empty/cleared hotkey is treated as "not configured" and silently replaced by the
  built-in default, so clearing appears to do nothing.
- **Guardrail:** distinguish "never set" from "explicitly cleared"; don't force a default over a
  user-cleared shortcut. Mirror this in the C# Settings UI validation.

### Overlay hook interferes with capture/VDI (e.g. Teams camera)
- **Symptom:** enabling Mouse Pointer Crosshairs disables another app's camera inside a VDI session.
  Evidence: [#47242](https://github.com/microsoft/PowerToys/issues/47242).
- **Where:** crosshairs/highlighter global `WH_MOUSE_LL` + full-screen layered topmost overlay under
  remoting/VDI.
- **Root cause:** a global hook + always-on-top layered capture window can interact badly with VDI
  screen/camera capture stacks.
- **Guardrail:** scope hooks to when the feature is actually active; ensure the overlay is
  `WS_EX_TRANSPARENT` and doesn't participate in capture; test under RDP/VDI when changing hook or
  window styles.

### Localized instructions / activation labels wrong or confusing
- **Symptom:** gliding-cursor English instructions incorrect / crosshair labels mistranslated
  ("控制鍵" for Find My Mouse). Evidence:
  [#45598](https://github.com/microsoft/PowerToys/issues/45598),
  [#46223](https://github.com/microsoft/PowerToys/issues/46223).
- **Where:** module `resource.h` strings and the C# Settings UI `.resw`.
- **Root cause:** activation-gesture UX text is easy to get wrong across the C++ overlay and the C#
  settings page; see [Globalization/resource-string guidance](https://learn.microsoft.com/en-us/windows/apps/design/globalizing/globalizing-portal).
- **Guardrail:** when adding/renaming an activation gesture, update BOTH the utility's resource string
  and the Settings-UI resource, and describe the gesture precisely (which keys, press vs hold).

## Review Rules

Enforce these when reviewing or authoring Mouse Utilities changes:

- **Preserve the 1px virtual-screen inset** on every overlay `SetWindowPos`. Removing it reintroduces
  the taskbar-transparency glitch / leftover square ([#44755](https://github.com/microsoft/PowerToys/issues/44755)).
- **Check hook/registration return values and log `GetLastError`.** Crosshairs already does this for
  its keyboard hook (`SetWindowsHookEx(WH_KEYBOARD_LL…)` → `Logger::error(... GetLastError=…)`); apply
  the same to `WH_MOUSE_LL` and `RegisterClassExW` (Copilot flagged a missing `RegisterClassExW` check
  in this module family, [PR #44936](https://github.com/microsoft/PowerToys/pull/44936)).
- **Keep the hook proc non-blocking.** `WH_MOUSE_LL`/`WH_KEYBOARD_LL` are global and serialized;
  slow work stalls all input system-wide and freezes the overlay ([#48442](https://github.com/microsoft/PowerToys/issues/48442)).
- **Keep C++ defaults in sync with the C# Settings UI.** Each `*Settings` struct in
  `FindMyMouse.h` / `MouseHighlighter.h` / `InclusiveCrosshairs.h` must match
  `src/settings-ui/.../ViewModels` + `Settings.UI.Library` defaults; opacity/alpha encoding must match
  on both sides ([#45321](https://github.com/microsoft/PowerToys/issues/45321)).
- **Don't overwrite an explicitly-cleared shortcut with a default.** ([#48158](https://github.com/microsoft/PowerToys/issues/48158)).
- **Use DPI-aware coordinates for Composition overlays.** Find My Mouse divides cursor coords by
  `RasterizationScale()` in `AfterMoveSonar`; new spotlight/mask geometry must scale the same way or
  it drifts on high-DPI / mixed-DPI multi-monitor.
- **Gate on game mode / excluded apps where the pattern exists.** Find My Mouse checks
  `detect_game_mode()` and `IsForegroundAppExcluded()` in `StartSonar`; preserve these when refactoring
  activation.
- **UX conventions for the settings cards** (from maintainer review, [PR #48232](https://github.com/microsoft/PowerToys/pull/48232)):
  a single click should not emit a double effect; collapse/hide mode-specific customization when the
  mode changes; use a **checkbox** (not a toggle switch) for boolean options inside a `SettingsExpander`.
- **Build hygiene:** don't reintroduce per-project `PlatformToolset`; use `$(RepoRoot)` not relative
  `..\..\` in vcxproj. If a toolset override is truly required, comment **why** and file a follow-up
  ([PR #44639 review](https://github.com/microsoft/PowerToys/pull/44639)).

## Pitfalls

- **`WH_MOUSE_LL` drives overlay position, and it is global + serialized.** A hung foreground app
  stalls the hook and freezes/detaches the crosshair or highlighter ([#48442](https://github.com/microsoft/PowerToys/issues/48442),
  [#48360](https://github.com/microsoft/PowerToys/issues/48360)). Never block inside the hook.
- **Never fill the exact virtual-screen rect** — always the 1px inset, or DWM glitches taskbar
  transparency ([#44755](https://github.com/microsoft/PowerToys/issues/44755)).
- **Find My Mouse opacity lives in the color alpha channel**, migrated from the legacy
  `overlay_opacity` percentage via `LegacyOpacityToAlpha`. Reading a color with full alpha turns the
  dim into a black screen ([#45321](https://github.com/microsoft/PowerToys/issues/45321)).
- **Mouse Highlighter's `alwaysColor` default alpha is 0** (`FromArgb(0,255,0,0)`) — invisible until
  the user opts in; don't assume it renders by default.
- **The four utilities are independent processes/modules** with separate settings keys and hotkeys —
  a fix in one does not carry to the others; verify each separately.
- **Gliding cursor moves the REAL cursor** via `SendInput` on `PositionCursorX/Y` worker threads and
  can auto-**LeftClick**; Escape cancel depends on the `WH_KEYBOARD_LL` hook installing successfully.
- **Composition overlays require a DispatcherQueue + STA apartment** on their thread
  (`CreateDispatcherQueueController` / `init_apartment`) before creating the `Compositor` /
  `DesktopWindowTarget`; skipping it fails silently.
- **Find My Mouse's default activation is a gesture, not a hotkey** (`DoubleLeftControlKey`); the
  configurable shortcut (default Shift+Win+F) only applies when the method is `Shortcut`.
- **Only Mouse Jump has unit tests** (`MouseJump.Common.UnitTests`). C++ overlay logic is validated by
  `MouseUtils.UITests`; add coverage there for activation/settings changes.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + settings defaults.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Mouse Utilities PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/MouseUtils/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/MouseUtils)
- [Low-level mouse hook `WH_MOUSE_LL`](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelmouseproc) · [Raw Input](https://learn.microsoft.com/en-us/windows/win32/inputdev/raw-input) · [Windows.UI.Composition](https://learn.microsoft.com/en-us/windows/uwp/composition/visual-layer) · [Layered windows](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features#layered-windows) · [Per-monitor DPI](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
