---
name: powertoys-cropandlock-knowledge
description: 'PowerToys CropAndLock module knowledge: three crop modes (Reparent = live SetParent child window, Thumbnail = DWM live clone, Screenshot = frozen PrintWindow bitmap), feature->file/function map, recurring regression playbooks (reparent restore/offset, multi-monitor coordinate union, DWM/DPI, PrintWindow black-image on GPU/protected windows, theme title bar, hotkey activation & conflict detection, single-instance mutex, GPO gating, default-enabled parity), maintainer review rules, and Pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/CropAndLock. Keywords: CropAndLock, crop and lock, reparent, thumbnail, DWM, PrintWindow, SetParent, DPI, multi-monitor, hotkey, GPO, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys CropAndLock Knowledge

Grounded engineering knowledge for the PowerToys **CropAndLock** module — a utility that crops a
chosen region of another application's window into a small always-on-top window that stays "locked"
to that region. It offers **three modes** (each a separate `CropAndLockWindow` subclass):

- **Reparent** — `SetParent`s the *live* target window into a cropped host window; the target keeps
  running and is restored on close. Interactive but invasive.
- **Thumbnail** — a **DWM live clone** (`DwmRegisterThumbnail`) of the crop region; read-only mirror,
  non-invasive, updates live.
- **Screenshot** — a **frozen** GDI snapshot (`PrintWindow` + `BitBlt`); never updates, cheapest,
  survives target close.

CropAndLock runs as a **separate `PowerToys.CropAndLock.exe` process** launched by the Runner; the
in-proc module DLL only owns settings/hotkeys and signals the exe through named events.

Use this file to localize code fast, avoid known regression traps, and enforce maintainer conventions.

## When to Use This Skill

- Planning or implementing a change under `src/modules/CropAndLock/` and needing prior art.
- Fixing/triaging a CropAndLock bug: cropped window offset/position wrong on close, multi-monitor
  selection broken, black or partial capture, white/wrong title-bar theme, hotkey not firing,
  window won't restore, standalone-launch failure.
- Reviewing a CropAndLock PR against maintainer conventions and regression traps.
- Adding a new crop mode, a new activation hotkey, or touching DWM/DPI/reparent geometry.

## Module Map (feature -> file/function)

Two projects: `CropAndLock/` (the standalone exe) and `CropAndLockModuleInterface/` (the Runner DLL).
Treat rows as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Runner-side module: enable/disable, settings, GPO gate | `CropAndLockModuleInterface/dllmain.cpp` `CropAndLockModuleInterface::enable/disable/get_config/set_config` |
| Hotkey registration (3 hotkeys) & activation | `dllmain.cpp` `get_hotkeys` (returns 3: reparent, thumbnail, screenshot), `on_hotkey(hotkeyId)` → `SetEvent` |
| Hotkey parse from settings JSON | `dllmain.cpp` `parse_hotkey` (keys `reparent-hotkey`/`thumbnail-hotkey`/`screenshot-hotkey`) |
| Launch/quit the exe (SEE lifecycle) | `dllmain.cpp` `Enable()` (`ShellExecuteExW` PowerToys.CropAndLock.exe + PID), `Disable()` (exit event) |
| Default-enabled state (must match settings) | `dllmain.cpp` `is_enabled_by_default()` → `false` |
| GPO policy value | `dllmain.cpp` `gpo_policy_enabled_configuration()` → `powertoys_gpo::getConfiguredCropAndLockEnabledValue` (`common/utils/gpo.h`) |
| exe entrypoint, COM/DPI init, message pump | `CropAndLock/main.cpp` `wWinMain` (`init_apartment(single_threaded)`, `SetProcessDpiAwarenessContext(PER_MONITOR_AWARE_V2)`) |
| Single-instance guard + "not standalone" check | `main.cpp` instance mutex `Local\\PowerToys_CropAndLock_InstanceMutex`; requires PID cmdline arg |
| Event listener thread (reparent/thumbnail/screenshot/exit) | `main.cpp` `m_event_triggers_thread` `MsgWaitForMultipleObjects`; named events in `common/interop/shared_constants.h` |
| Mode dispatch & cropped-window lifetime | `main.cpp` `ProcessCommand(CropAndLockType)`, `windowCroppedCallback`, `croppedWindows` vector, `removeWindowCallback` |
| Crop mode enum | `CropAndLock/SettingsWindow.h` `enum class CropAndLockType { Reparent, Thumbnail, Screenshot }` |
| Common cropped-window interface | `CropAndLock/CropAndLockWindow.h` (`Handle`/`CropAndLock`/`OnClosed`) |
| Region-selection overlay (shade + rubber-band) | `CropAndLock/OverlayWindow.cpp` `SetupOverlay`, `OnLeftButtonDown/Up`, `OnMouseMove`, ESC to cancel |
| All-monitors union / origin shift for overlay | `CropAndLock/DisplaysUtil.h` `ComputeAllDisplaysUnion`; used by `OverlayWindow` |
| Client-area → screen-space rect helper | `CropAndLock/WindowRectUtil.h` `ClientAreaInScreenSpace` |
| **Reparent** mode (live SetParent) | `CropAndLock/ReparentCropAndLockWindow.cpp` `CropAndLock`, `SaveOriginalState`, `RestoreOriginalState`, `DisconnectTarget` |
| Reparent host child window | `CropAndLock/ChildWindow.cpp` |
| Reparent focus forwarding to target | `ReparentCropAndLockWindow.cpp` `MessageHandler` (`WM_MOUSEACTIVATE`/`WM_ACTIVATE` → `SetForegroundWindow(m_currentTarget)`) |
| **Thumbnail** mode (DWM live clone) | `CropAndLock/ThumbnailCropAndLockWindow.cpp` `CropAndLock` (`DwmRegisterThumbnail`/`DwmUpdateThumbnailProperties`), `ComputeDestRect` aspect-fit |
| **Screenshot** mode (frozen bitmap) | `CropAndLock/ScreenshotCropAndLockWindow.cpp` `CropAndLock` (`PrintWindow` PW_RENDERFULLCONTENT + `BitBlt`), `WM_PAINT` `StretchBlt` |
| DWM extended-frame bounds (thumbnail/screenshot geometry) | `ThumbnailCropAndLockWindow.cpp` / `ScreenshotCropAndLockWindow.cpp` `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)` |
| Dark/light theming of cropped windows | `main.cpp` `theme_listener` + `handleTheme` → `ThemeHelpers::SetImmersiveDarkMode` |
| Telemetry (activation/create/settings) | `CropAndLock/trace.cpp` `Trace::CropAndLock::*` |
| Module constants / keys | `CropAndLock/ModuleConstants.h`, `CropAndLockModuleInterface/dllmain.cpp` `ModulePath` |

**Coordinate pipeline (critical):** the overlay works in a *union-of-all-displays* space
(`ComputeAllDisplaysUnion`) and shifts the origin to the top-left-most point; each mode then
re-derives the crop rect relative to its own reference. Reparent uses `GetWindowRect`; Thumbnail and
Screenshot use `DwmGetWindowAttribute(DWMWA_EXTENDED_FRAME_BOUNDS)`. All three add the
client-vs-window diff via `ClientAreaInScreenSpace`. Mixing these reference frames is the root of
most offset bugs.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Reparent restore / offset on close
- **Symptom:** after closing a cropped (reparent) window the original window reappears at the wrong
  position/size or in the wrong maximized state.
- **Where:** `ReparentCropAndLockWindow.cpp` `SaveOriginalState` / `RestoreOriginalState` /
  `DisconnectTarget`.
- **Root cause:** reparenting mutates the target's `GWL_STYLE` (adds `WS_CHILD`), placement and
  parent; restore must reverse *all* of it in order (position → `SetParent(nullptr)` → placement →
  ex-style/style with `WS_CHILD` cleared). Missing/observed-out-of-order restore leaves a bad offset.
- **Guardrail:** any change to reparent geometry must round-trip Save→Restore for both normal and
  **maximized** targets; verify `WS_CHILD` is cleared and `WINDOWPLACEMENT` restored. Evidence:
  issues [#34813](https://github.com/microsoft/PowerToys/issues/34813) (wrong offset on close),
  [#45666](https://github.com/microsoft/PowerToys/issues/45666),
  [#42495](https://github.com/microsoft/PowerToys/issues/42495) (first drag of cropped window fails).

### Multi-monitor / negative-coordinate selection
- **Symptom:** crop overlay or selection is wrong (or crashes) with multiple monitors, especially
  when a monitor sits left/above the primary (negative coordinates) or a target is maximized.
- **Where:** `OverlayWindow.cpp` `SetupOverlay` (nine-grid shade insets clamped with
  `std::max(...,0.0f)`), `DisplaysUtil.h` `ComputeAllDisplaysUnion`.
- **Root cause:** overlay spans the *union* of all displays and must shift every rect into that
  origin; raw per-monitor rects or unclamped insets produce off-screen/inverted selections.
- **Guardrail:** keep all overlay math in union space; clamp insets non-negative; test a
  left-of-primary monitor and a maximized window. Evidence:
  [#36485](https://github.com/microsoft/PowerToys/issues/36485) (broken for multiple screens).

### Screenshot mode: black / partial image
- **Symptom:** Screenshot mode yields a black or incomplete image for hardware-accelerated, GPU, or
  protected windows (browsers, some overlays, ZoomIt annotations).
- **Where:** `ScreenshotCropAndLockWindow.cpp` `CropAndLock` — `PrintWindow(..., PW_RENDERFULLCONTENT)`.
- **Root cause:** `PrintWindow` cannot capture content that isn't rendered through GDI (DWM-composited
  / hardware-accelerated / DRM-protected surfaces); the call is wrapped in `winrt::check_bool`, so a
  failure throws instead of degrading with a message.
- **Guardrail:** treat `PrintWindow` failure as expected for GPU/protected windows; surface a
  meaningful error and consider a Thumbnail-mode fallback rather than throwing. Evidence:
  [#48850](https://github.com/microsoft/PowerToys/issues/48850) (black image after update),
  [#42744](https://github.com/microsoft/PowerToys/issues/42744) (white border in Brave); the exact
  failure mode was flagged in review of [PR #40720](https://github.com/microsoft/PowerToys/pull/40720).

### GDI resource leaks in Screenshot capture
- **Symptom:** DC/bitmap handle leaks under repeated screenshots (each `GetDC(nullptr)`,
  `CreateCompatibleDC`, `CreateCompatibleBitmap`, `SelectObject` must be paired/restored).
- **Where:** `ScreenshotCropAndLockWindow.cpp` `CropAndLock` and `WM_PAINT`.
- **Root cause:** GDI objects created without matching `ReleaseDC`/`DeleteDC`/`DeleteObject`/restored
  `SelectObject`, and no RAII — a mid-function throw (e.g. failed `PrintWindow`) leaks.
- **Guardrail:** pair every acquire with a release (or use a scoped wrapper); ensure cleanup on the
  throwing path. Evidence: review of [PR #40720](https://github.com/microsoft/PowerToys/pull/40720)
  (multiple GDI-leak / RAII review comments on the screenshot path).

### Cropped window doesn't update (mode expectation mismatch)
- **Symptom:** users report the cropped window "stopped updating" or lost live behavior.
- **Where:** mode selection in `main.cpp` `ProcessCommand`; `ScreenshotCropAndLockWindow` (frozen) vs
  `ThumbnailCropAndLockWindow` / `ReparentCropAndLockWindow` (live).
- **Root cause:** Screenshot mode is **by design** a frozen snapshot; Thumbnail/Reparent update live.
  Fullscreen or DWM-occluded targets can also stop a Thumbnail clone from refreshing.
- **Guardrail:** confirm which mode the report is about before "fixing"; don't make Screenshot live.
  Evidence: [#38104](https://github.com/microsoft/PowerToys/issues/38104) (fullscreen doesn't update),
  [#45666](https://github.com/microsoft/PowerToys/issues/45666).

### Theme: white/wrong cropped-window title bar
- **Symptom:** cropped-window title bar renders white instead of following the system dark theme.
- **Where:** `main.cpp` `handleTheme` → `ThemeHelpers::SetImmersiveDarkMode(window->Handle(), isDark)`;
  invoked on `theme_listener` change *and* right after each window is created.
- **Root cause:** immersive dark mode must be (re)applied to every cropped window handle, including
  newly created ones, on theme change.
- **Guardrail:** call `handleTheme()` after creating any new cropped window and on every theme event.
  Evidence: [#35562](https://github.com/microsoft/PowerToys/issues/35562); fixed by
  [PR #38044](https://github.com/microsoft/PowerToys/pull/38044).

### Hotkey activation / conflicts
- **Symptom:** a CropAndLock hotkey doesn't fire, fires in the wrong context (VM/Hyper-V window), or
  a new hotkey collides with another module's shortcut.
- **Where:** `dllmain.cpp` `on_hotkey`/`get_hotkeys` (3 hotkeys, order-sensitive), `SetEvent` to the
  exe; Settings-side shortcut **conflict detection**.
- **Root cause:** `on_hotkey` maps `hotkeyId` 0/1/2 to reparent/thumbnail/screenshot **by index** —
  `get_hotkeys` order must match; new hotkeys must be registered with settings conflict detection.
- **Guardrail:** keep `get_hotkeys`/`on_hotkey` indices in lockstep; register any new hotkey with
  [shortcut conflict detection](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/core/settings/settings-implementation.md#shortcut-conflict-detection)
  (required during [PR #40720](https://github.com/microsoft/PowerToys/pull/40720)). Evidence:
  [#42558](https://github.com/microsoft/PowerToys/issues/42558) (no-op in Hyper-V VM window),
  [#41806](https://github.com/microsoft/PowerToys/issues/41806) (modifier keys released with multiple hooks).

### Default-enabled parity (first-launch flicker / DSC gap)
- **Symptom:** on a clean install the module briefly enables then disables itself; DSC/compliance
  reports a mismatch.
- **Where:** `dllmain.cpp` `is_enabled_by_default()` vs `settings-ui` `EnabledModules.cs`.
- **Root cause:** inheriting the base `is_enabled_by_default() = true` while `EnabledModules.cs`
  declares the module `false` — the two disagree.
- **Guardrail:** the C++ `is_enabled_by_default()` override MUST match the C# `EnabledModules.cs`
  default. Evidence: [PR #47144](https://github.com/microsoft/PowerToys/pull/47144).

## Review Rules

Enforce these when reviewing or authoring CropAndLock changes:

- **Keep `get_hotkeys` and `on_hotkey` indices in lockstep.** `on_hotkey` dispatches by numeric
  `hotkeyId` (0=reparent,1=thumbnail,2=screenshot); reordering one without the other misfires
  (`dllmain.cpp`).
- **Register every new hotkey with settings conflict detection** ([conflict detection doc](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/core/settings/settings-implementation.md#shortcut-conflict-detection)) —
  required in [PR #40720](https://github.com/microsoft/PowerToys/pull/40720).
- **Never make the exe run standalone.** `main.cpp` requires a Runner PID arg and a single-instance
  mutex; keep the "can't run as standalone" guard and the `ProcessWaiter` parent-exit teardown.
- **Do all overlay geometry in all-displays-union space** and clamp shade insets non-negative
  (`OverlayWindow::SetupOverlay`, `DisplaysUtil.h`); test negative-origin monitors.
- **Match the DPI/reference frame per mode.** Reparent uses `GetWindowRect` +
  `AdjustWindowRectExForDpi`; Thumbnail/Screenshot use `DWMWA_EXTENDED_FRAME_BOUNDS`. Don't mix.
- **Reparent restore must fully reverse the mutation** — position, `SetParent(nullptr)`,
  `WINDOWPLACEMENT`, and clearing `WS_CHILD` from the saved style, in order.
- **Pair every GDI acquire with a release on all paths** (Screenshot mode); prefer RAII so a throwing
  `PrintWindow`/`check_bool` doesn't leak.
- **Treat `PrintWindow` failure as expected** for GPU/protected windows — degrade with a message, don't
  hard-throw (Screenshot mode).
- **Apply immersive dark mode to every cropped window** on creation and on theme change
  (`handleTheme`).
- **Keep `is_enabled_by_default()` == `EnabledModules.cs` default** ([PR #47144](https://github.com/microsoft/PowerToys/pull/47144)).
- **No bare relative paths in project files** — use `$(RepoRoot)`; don't reorder `Microsoft.Cpp.*.props`
  imports ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).

## Pitfalls

- **Never** assume the crop window is live — **Screenshot mode is a frozen bitmap by design**; only
  Reparent and Thumbnail update. Don't "fix" Screenshot to update (#38104).
- **Never** reorder `get_hotkeys` without updating `on_hotkey`'s `hotkeyId` branches — they are matched
  by position (dllmain.cpp).
- **Never** compute overlay/selection rects in raw per-monitor coordinates — use the all-displays union
  and shift origin, or multi-monitor and negative-origin layouts break (#36485).
- **Never** capture GPU/hardware-accelerated/protected windows and expect a valid bitmap — `PrintWindow`
  returns black/partial (browsers, ZoomIt annotations) (#48850, #42744).
- **Never** leave a reparent restore partial — a missed `WS_CHILD` clear or `WINDOWPLACEMENT` restore
  leaves the user's window offset/broken (#34813).
- **Reparenting across DPI contexts has documented side effects** — see the `SetParent` remarks note in
  `main.cpp`; the process runs `PER_MONITOR_AWARE_V2` deliberately.
- **The exe cannot run on its own** — launched by the Runner with the Runner PID; it exits when the
  parent exits or on the exit event.
- **Binary-size wins don't outrank clean teardown** — Hybrid CRT was reverted because module DLLs
  failed to unload safely at quit ([PR #43484](https://github.com/microsoft/PowerToys/pull/43484)).
- **CropAndLock has no unit tests** — validate changes manually across the three modes, multiple
  monitors, and dark/light theme (noted in [PR #40720](https://github.com/microsoft/PowerToys/pull/40720)).

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**; then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you on recurring
themes and measurably lowers your catch rate on the PR's actual issues. If a symptom doesn't map to
a row, reason from the source, not the map. Best for planning / triage; a targeted checklist (not a
script) for review.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a CropAndLock PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/CropAndLock/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/CropAndLock)
- [DWM thumbnails](https://learn.microsoft.com/en-us/windows/win32/api/dwmapi/nf-dwmapi-dwmregisterthumbnail) · [PrintWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-printwindow) · [SetParent remarks (DPI)](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setparent#remarks) · [Shortcut conflict detection](https://github.com/microsoft/PowerToys/blob/main/doc/devdocs/core/settings/settings-implementation.md#shortcut-conflict-detection)
