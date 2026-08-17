---
name: colorpicker-knowledge
description: 'PowerToys Color Picker (module dir src/modules/colorPicker; C# WPF app ColorPickerUI + C++ Runner module ColorPicker) knowledge: feature->file/function map, recurring regression playbooks (GDI screen pixel capture wrong on HDR/wide-gamut displays, zoom window capturing the picker window itself, dual activation paths — Runner centralized hotkey via shared event vs standalone GlobalKeyboardHook, color-format specifier strings incl. Decimal %Dv BGR order, multi-monitor/per-monitor-DPI positioning & refresh-rate polling, static Bitmap/Graphics reuse & memory), maintainer review rules, and Pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/colorPicker — pixel color capture, zoom, color formats/editor & history, activation shortcut, settings, GPO.'
license: Complete terms in LICENSE.txt
---

# PowerToys Color Picker Knowledge

Grounded engineering knowledge for the PowerToys **Color Picker** module — a system-wide color
picker. On the activation shortcut it shows a small picker that follows the cursor, samples the
screen pixel under the mouse in real time, lets the mouse wheel zoom into a magnified pixel grid,
copies the color in the user's chosen format, and can open an editor with color history and
multiple format representations. The module has **two projects**: the C# WPF app **ColorPickerUI**
(`ColorPickerUI/`) that does all the UI/capture, and the C++ **Runner module** (`ColorPicker/`,
`dllmain.cpp`) that registers the centralized hotkey and launches/terminates the app. Use this to
localize code fast, avoid known regression traps, and enforce the conventions maintainers already
established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/colorPicker/` and needing prior art.
- Fixing/triaging a Color Picker bug: picked color is wrong (especially on HDR/wide-gamut or
  scaled displays), zoom view shows the picker's own window/corner, wrong color-format string,
  activation shortcut not firing / firing when partly held / "flashing" in the settings editor,
  arrow-key nudge not working, picker opens on the wrong monitor, memory/GDI growth.
- Reviewing a Color Picker PR against maintainer conventions and regression traps.
- Touching the pixel-capture loop, the zoom/magnifier, the color-format conversion/editor, the
  activation plumbing, or the Runner-side C++ module.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Runner-side hotkey parse (default **Win+Shift+C**), GPO gate, launch/terminate app, shared events | `ColorPicker/dllmain.cpp` `parse_hotkey`, `on_hotkey`, `get_hotkeys`, `enable`/`disable`; events `SHOW_COLOR_PICKER_SHARED_EVENT`, `TERMINATE_COLOR_PICKER_SHARED_EVENT` |
| App bootstrap, GPO check, logger init, STA `Main`, exception cursor-restore | `ColorPickerUI/Program.cs` `Main` (`GetConfiguredColorPickerEnabledValue`) |
| Single-instance mutex, Runner-PID arg, UI-culture, Runner-vs-standalone branch | `ColorPickerUI/App.xaml.cs` `OnStartup` (mutex `Local\PowerToys_ColorPicker_InstanceMutex`), `IsRunningDetachedFromPowerToys` |
| Wait on Runner shared events (show/terminate/telemetry); wires mouse + keyboard | `ColorPickerUI/ViewModels/MainViewModel.cs` ctor (`NativeEventWaiter.WaitForEventLoop`) |
| **Standalone** activation: own low-level hook, parses `ActivationShortcut` string, Esc/Space/Enter/arrows | `ColorPickerUI/Keyboard/KeyboardMonitor.cs` `Hook_KeyboardPressed`, `SetActivationKeys`, `CheckMoveNeeded`; `Keyboard/GlobalKeyboardHook.cs` |
| Screen **pixel color capture** (per-frame timer at display refresh rate) | `ColorPickerUI/Mouse/MouseInfoProvider.cs` `GetPixelColor` (GDI `Graphics.CopyFromScreen` → `Bitmap.GetPixel`), `Timer_Tick`, `GetMainDisplayRefreshRate` |
| Mouse hooks (primary down = pick, wheel = zoom, secondary/middle) | `ColorPickerUI/Mouse/MouseHook.cs`; `MouseInfoProvider` hook wiring |
| Zoom / magnifier: level state, capture region, DPI-aware positioning | `ColorPickerUI/Helpers/ZoomWindowHelper.cs` `Zoom`, `SetZoomImage`, `ShowZoomWindow` (static `_bmp`/`_graphics`) |
| **Exclude picker window from screen capture** during zoom | `ColorPickerUI/Helpers/WindowCaptureExclusionHelper.cs` `Exclude`/`Include` (`SetWindowDisplayAffinity` `WDA_EXCLUDEFROMCAPTURE`) |
| Session/app show-hide state, editor open, Esc/Enter routing, cursor move, top-most | `ColorPickerUI/Helpers/AppStateHandler.cs` `StartUserSession`, `EndUserSession`, `SetTopMost`, `MoveCursor`, `GetMainWindowHandle` |
| Color-format **string specifiers** (`%Rex`, `%Dv`=Decimal BGR, HSL/HSB/CMYK/HSV/Lab/HSI/…) | `src/common/ManagedCommon/ColorFormatHelper.cs` `GetStringRepresentation`, `GetDefaultFormat`, format-char map (**shared**, not in module) |
| Format wrapper + color-name localization (`%Na`) | `ColorPickerUI/Helpers/ColorRepresentationHelper.cs` `GetStringRepresentation`, `ReplaceName` |
| Editor window: color history, per-format representations, export, delete | `ColorPickerUI/ViewModels/ColorEditorViewModel.cs` (`ColorsHistory`, `ColorRepresentations`); `ColorEditorWindow.xaml.cs` |
| Copy to clipboard | `ColorPickerUI/Helpers/ClipboardHelper.cs` |
| Monitor enumeration, DPI, primary refresh rate | `ColorPickerUI/Helpers/MonitorResolutionHelper.cs` `AllMonitors`, `GetCurrentMonitorDpi`, `IsPrimary` |
| Cursor swap while picking | `ColorPickerUI/Mouse/CursorManager.cs` (`ChangeCursor` setting) |
| Settings (default `ActivationShortcut = Win+Shift+C`, `CopiedColorRepresentation`, `ActivationAction`) | `ColorPickerUI/Settings/UserSettings.cs`, `Settings/IUserSettings.cs` |
| Telemetry (session, settings) | `ColorPickerUI/Telemetry/ColorPickerSession.cs`, `Telemetry/ColorPickerSettings.cs`; `SessionEventHelper.cs` |
| Unit tests / UI tests | `ColorPickerUI.UnitTests/Helpers/ColorFormatConversionTest.cs`, `ColorConverterTest.cs`; `ColorPicker.UITests/ColorPickerEndToEndTests.cs` |

**Activation has two independent mechanisms** (confirm which one a bug hits):
1. **Runner-launched (normal):** Runner's centralized keyboard hook fires the hotkey parsed by
   `dllmain.cpp::parse_hotkey` (default **Win+Shift+C**) → sets `SHOW_COLOR_PICKER_SHARED_EVENT`
   → `MainViewModel` `NativeEventWaiter` → `AppStateHandler.StartUserSession`. `KeyboardMonitor`
   is **not** run permanently; a **lightweight** hook is started only for the duration of an
   active session to catch Esc/Space/Enter/arrows.
2. **Standalone / detached** (`IsRunningDetachedFromPowerToys()` — no Runner PID arg): `MainViewModel`
   calls `keyboardMonitor.Start()`, installing a permanent `GlobalKeyboardHook` that parses the
   `ActivationShortcut` string and matches sorted key-name lists.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Picked color is wrong on HDR / wide-gamut / high-bit-depth displays
- **Symptom:** the reported color doesn't match the pixel under the cursor; consistently off on
  HDR-enabled or wide-gamut monitors; "shows colors that are not present at the pointer".
- **Where:** `MouseInfoProvider.GetPixelColor` — GDI `Graphics.CopyFromScreen(...SourceCopy)` into a
  1×1 `Bitmap(Format32bppArgb)` then `GetPixel(0,0)`.
- **Root cause:** GDI screen copy samples the **composited sRGB 8-bit-per-channel** desktop; on HDR
  (scRGB/10-bit) or wide-gamut displays the true pixel value is tone-mapped/clamped before GDI sees
  it, so the sampled RGB differs from what's on screen. This is a display-pipeline limitation, not a
  format bug.
- **Guardrail:** don't assume `CopyFromScreen` returns the display's native color on HDR; when
  touching capture, preserve `Format32bppArgb`/`SourceCopy` semantics and treat HDR as a known gap —
  verify against an HDR monitor before claiming a capture change "fixes" color accuracy. Evidence:
  issues [#44329](https://github.com/microsoft/PowerToys/issues/44329),
  [#41107](https://github.com/microsoft/PowerToys/issues/41107),
  [#45446](https://github.com/microsoft/PowerToys/issues/45446),
  [#43170](https://github.com/microsoft/PowerToys/issues/43170).

### Zoom (magnifier) view includes the Color Picker's own window
- **Symptom:** when zooming, a corner/edge of the picker UI appears inside the magnified image.
- **Where:** `ZoomWindowHelper.SetZoomImage` (calls `_graphics.CopyFromScreen`), guarded by
  `WindowCaptureExclusionHelper.Exclude`/`Include`.
- **Root cause:** the zoom grabs a screen rectangle centered on the cursor **while the picker window
  is on screen**, so `CopyFromScreen` captures the picker itself.
- **Guardrail:** set `WDA_EXCLUDEFROMCAPTURE` (`SetWindowDisplayAffinity`) on the picker HWND
  *before* the capture and restore `WDA_NONE` in a `finally` — never leave the window excluded (it
  would stop the picker from being captured afterward). Exclusion requires Win10 2004+
  (build 19041); handle the unsupported/failed case without crashing. Evidence:
  [PR #48762](https://github.com/microsoft/PowerToys/pull/48762).

### Activation shortcut: not firing, firing on partial hold, or "flashing" in the editor
- **Symptom:** custom shortcut doesn't launch; the combo triggers while only some keys are held; the
  Settings shortcut editor flashes/activates the module while you hold the keys to record it; arrow
  nudge stops working.
- **Where:** **two paths** — Runner centralized hotkey `dllmain.cpp::parse_hotkey`/`on_hotkey`/
  `get_hotkeys`, and standalone `KeyboardMonitor.Hook_KeyboardPressed` (sorts pressed keys, compares
  to sorted `_activationKeys`, uses `_activationShortcutPressed` latch to avoid re-fire).
- **Root cause:** the two mechanisms are maintained separately; the standalone hook matches on string
  key-name lists that must be re-derived when `ActivationShortcut` changes (`SetActivationKeys`); the
  low-level hook is global, so it can observe keystrokes meant for the Settings editor.
- **Guardrail:** keep both paths behaviorally in sync; when `ActivationShortcut` is empty install
  **no** match (`_activationKeys` stays empty and `ArraysAreSame` returns false for empty lists);
  preserve the `_activationShortcutPressed` latch so held keys fire once. Evidence: issues
  [#43791](https://github.com/microsoft/PowerToys/issues/43791) /
  [#43250](https://github.com/microsoft/PowerToys/issues/43250) (editor flashing),
  [#44963](https://github.com/microsoft/PowerToys/issues/44963),
  [#44404](https://github.com/microsoft/PowerToys/issues/44404),
  [#48822](https://github.com/microsoft/PowerToys/issues/48822),
  [#41806](https://github.com/microsoft/PowerToys/issues/41806) (shared low-level-hook modifier bug).

### Wrong color-format string (esp. Decimal / axis confusion)
- **Symptom:** a format outputs an unexpected number; Decimal shows a channel value instead of the
  packed integer, or R/B look swapped; Lab/NCol axis wrong.
- **Where:** `src/common/ManagedCommon/ColorFormatHelper.cs` `GetStringRepresentation` /
  `GetDefaultFormat` (format-specifier map), wrapped by
  `ColorRepresentationHelper.GetStringRepresentation` for name localization.
- **Root cause:** the Decimal format `%Dv` is **BGR-packed** (`R + G*256 + B*65536`), so Red (255,0,0)
  → `255` and Blue (0,0,255) → `16711680` — the opposite of the naive RGB assumption. Similar
  axis/name confusion exists for CIELAB (`b*` vs `a*`) and Natural Color (`NCol`).
- **Guardrail:** verify format changes against `ColorFormatConversionTest.cs` with **known colors per
  axis**; name the test after what it actually asserts (e.g. blue → negative `b*`, not "negative a*").
  Don't hardcode expected values without confirming the specifier's channel order. Evidence:
  [PR #46679](https://github.com/microsoft/PowerToys/pull/46679) (swapped Decimal expected values;
  CIELAB test renamed to match the asserted axis).

### Multi-monitor / per-monitor-DPI positioning & refresh-rate polling
- **Symptom:** picker/zoom opens on the wrong monitor or offset from the cursor on mixed-DPI setups;
  zoom feels sluggish or misplaced; capture cadence wrong on high-refresh displays.
- **Where:** `MonitorResolutionHelper.AllMonitors`/`GetCurrentMonitorDpi`/`IsPrimary`;
  `ZoomWindowHelper.ShowZoomWindow` (iterative `PointFromScreen` convergence to center the zoom on
  the cursor across DPI); `MouseInfoProvider.GetMainDisplayRefreshRate` (polls **primary** monitor
  via `EnumDisplaySettingsW`).
- **Root cause:** WPF window coords are DIPs while cursor/screen bounds are physical pixels; per-monitor
  DPI differs, so the zoom window must converge its position via `PointFromScreen` rather than assume
  a single scale. The capture timer interval is derived from the **primary** display refresh rate only.
- **Guardrail:** don't replace the `ShowZoomWindow` convergence loop with a single-scale calc; keep
  DPI conversions at the boundary; remember refresh-rate polling keys off the primary monitor.
  Test on a mixed-DPI multi-monitor setup. Evidence: issues
  [#39194](https://github.com/microsoft/PowerToys/issues/39194) (open on active monitor),
  [#45446](https://github.com/microsoft/PowerToys/issues/45446).

### Memory / GDI resource growth
- **Symptom:** ColorPickerUI virtual/working-set memory climbs over a session.
- **Where:** `ZoomWindowHelper` static `_bmp`/`_graphics` (created once, reused);
  `MouseInfoProvider.GetPixelColor` (allocates a 1×1 `Bitmap`+`Graphics` **every timer tick**, at the
  display refresh rate).
- **Root cause:** the per-frame capture allocates and disposes GDI objects at up to the monitor's
  refresh rate while the picker is shown; the zoom's static bitmap is intentionally long-lived.
- **Guardrail:** keep the per-tick GDI objects inside `using` blocks (they are — don't "optimize" them
  into shared fields without care); the timer runs only between `AppShown` and `AppClosed/AppHidden`
  (`MouseInfoProvider` starts/stops it) — don't leave it running when the picker is hidden. Evidence:
  issues [#47892](https://github.com/microsoft/PowerToys/issues/47892),
  [#48299](https://github.com/microsoft/PowerToys/issues/48299).

## Review Rules

Enforce these when reviewing or authoring Color Picker changes:

- **Don't claim capture changes "fix" HDR color.** `GetPixelColor` reads the composited sRGB desktop
  via GDI; HDR/wide-gamut accuracy is a display-pipeline limit — verify on real HDR hardware (#44329,
  #41107).
- **Always restore capture exclusion in a `finally`.** After `WindowCaptureExclusionHelper.Exclude`,
  the picker HWND must return to `WDA_NONE`; a leaked exclusion makes the picker uncapturable
  ([PR #48762](https://github.com/microsoft/PowerToys/pull/48762)).
- **Keep the two activation paths in sync.** A change to the standalone `KeyboardMonitor`/
  `GlobalKeyboardHook` or the Runner hotkey (`dllmain.cpp`) must be mirrored/considered in the other
  (#43250, #44963).
- **Empty `ActivationShortcut` must arm nothing.** `SetActivationKeys` leaves `_activationKeys` empty
  and `ArraysAreSame` returns false for two empty lists — don't "simplify" that away.
- **Confirm color-format channel order before asserting.** `%Dv` Decimal is BGR-packed; name tests
  after the asserted axis ([PR #46679](https://github.com/microsoft/PowerToys/pull/46679)). Format
  specifiers live in shared `ManagedCommon/ColorFormatHelper.cs`, not the module.
- **Route color-name output through localization.** Use `ColorRepresentationHelper.ReplaceName` /
  `%Na`; don't emit raw English color names.
- **Honor the GPO gate at entry.** `Program.Main` exits when
  `GetConfiguredColorPickerEnabledValue() == Disabled`; the Runner module must respect enablement too.
- **Don't break the single-instance mutex.** `Local\PowerToys_ColorPicker_InstanceMutex`; a second
  launch exits — don't rename or bypass it (`App.xaml.cs`).
- **Preserve DPI convergence in zoom.** Keep the `PointFromScreen` iteration in
  `ZoomWindowHelper.ShowZoomWindow`; it exists for mixed-DPI correctness (#39194).
- **Test-only UIA hooks need `AutomationProperties.AccessibilityView="Raw"`.** Hidden `TextBlock`s
  added for UI tests must stay out of the accessibility content view
  ([PR #48467](https://github.com/microsoft/PowerToys/pull/48467)).
- **No bare relative paths / don't re-add `PlatformToolset` per-vcxproj.** Use `$(RepoRoot)`; the
  PlatformToolset is unified centrally — if you must set it locally, comment why and file a follow-up
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)). Keep C#
  `TreatWarningsAsErrors=true` (repo default; #48467).

## Pitfalls

- **`CopyFromScreen` gives sRGB, not HDR.** The single most common "wrong color" report on HDR/wide-gamut
  displays is inherent to GDI capture in `MouseInfoProvider.GetPixelColor`, not a conversion bug (#44329).
- **Two activation code paths.** Runner centralized hotkey (shared event) vs standalone
  `GlobalKeyboardHook`; plus a *third*, lightweight per-session hook for Esc/Space/Enter/arrows. Fixing
  one and forgetting the others is the classic shortcut regression (#43250, #44963).
- **Default hotkey is Win+Shift+C** — `dllmain.cpp::parse_hotkey` falls back to it when no key is set.
- **`%Dv` Decimal is BGR-packed** (`R + G*256 + B*65536`): Red→255, Blue→16711680. Do not assume RGB
  order for the Decimal format (#46679).
- **Never leave the picker window excluded from capture.** `WindowCaptureExclusionHelper.Exclude`
  without a matching `Include` breaks later screenshots of the picker; the exclusion is Win10-2004+
  only (#48762 / PR).
- **The capture timer ticks at the primary display refresh rate** and allocates GDI per frame — it runs
  only while the picker is shown; don't leave it running or move it off the show/hide lifecycle (#47892).
- **`ColorFormatHelper` is shared code** in `src/common/ManagedCommon/`, not the module — a format change
  there affects other consumers.
- **Single-instance mutex** `Local\PowerToys_ColorPicker_InstanceMutex` — a second launch exits silently.
- **`AppStateHandler.SetTopMost()` toggles `Topmost` false→true** deliberately to force z-order above a
  just-shown zoom window; it looks redundant but isn't.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + notes.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Color Picker PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/colorPicker/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/colorPicker)
- [SetWindowDisplayAffinity / WDA_EXCLUDEFROMCAPTURE](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity) ·
  [Graphics.CopyFromScreen](https://learn.microsoft.com/en-us/dotnet/api/system.drawing.graphics.copyfromscreen) ·
  [Per-Monitor DPI](https://learn.microsoft.com/en-us/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)
