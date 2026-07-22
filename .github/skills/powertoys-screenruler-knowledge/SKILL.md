---
name: powertoys-screenruler-knowledge
description: 'PowerToys Screen Ruler (Measure Tool) module knowledge: feature->file/function map, regression playbooks (pixel edge-detection off-by-one & 8-bit tolerance truncation, unit-conversion math, multi-monitor overlay lifecycle, continuous vs single-frame capture, cross-thread cursor alignment), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/MeasureTool — edge detection, measurement overlay, DPI/px-to-mm, Direct3D screen capture, multi-monitor, bounds/measure/spacing tools, clipboard, UI tests. Keywords: Screen Ruler, Measure Tool, MeasureTool, edge detection, DetectEdges, PixelsClose, px2mm, DPI, multi-monitor, Windows.Graphics.Capture, D2D overlay, continuous capture, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Screen Ruler (Measure Tool) Knowledge

Grounded engineering knowledge for the PowerToys **Screen Ruler** module (source dir
`src/modules/MeasureTool/`, product name "Measure Tool"). Screen Ruler captures the screen with
Windows.Graphics.Capture, runs a pixel-similarity **edge-detection** flood from the cursor to
measure bounded regions, and paints a Direct2D **measurement overlay** (bounds / horizontal /
vertical / cross tools) across every monitor, converting pixel spans to px / in / cm / mm. Use this
skill to localize code fast, avoid known regression traps, and enforce maintainer conventions.

## When to Use This Skill

- Planning or implementing a change under `src/modules/MeasureTool/` and needing prior art.
- Fixing/triaging a Screen Ruler bug: wrong measurement value, edges detected incorrectly, overlay
  missing on some monitors, black screen / overexposure, crash on capture, cursor gone, Esc not
  closing, tool activating when disabled.
- Reviewing a Measure Tool PR against maintainer conventions and the recurring regression classes.
- Touching edge detection, the px→mm/DPI math, the D3D capture loop, or the per-monitor overlay
  lifecycle / worker threads.
- Adding or porting UI tests under `src/modules/MeasureTool/Tests/`.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Module registration, GPO gate, hotkey parse, launch/terminate UI process | `MeasureToolModuleInterface/dllmain.cpp` `MeasureTool::parse_hotkey/on_hotkey/launch_process`, `gpo_policy_enabled_configuration` |
| Default activation shortcut (Win+Ctrl+Shift+M) | `dllmain.cpp::parse_hotkey` fallback block |
| WinUI 3 toolbar app (tool selection, close) | `MeasureToolUI/MeasureToolXAML/MainWindow.xaml(.cs)`, `App.xaml.cs`, `Settings.cs`, `NativeMethods.cs` |
| Core session orchestration (WinRT `Core`) | `MeasureToolCore/PowerToys.MeasureToolCore.cpp` `Core::StartBoundsTool/StartMeasureTool/ResetState/Close` |
| Cross-thread cursor sampling (atomic) | `PowerToys.MeasureToolCore.cpp::MouseCaptureThread` + `ToolState.h` `CommonState::cursorPosSystemSpace` (`alignas(8)`, `InterlockedExchange64`) |
| Shared tool/session state (per-screen maps) | `ToolState.h` `CommonState`, `MeasureToolState`, `BoundsToolState` |
| Screen capture (Windows.Graphics.Capture + DXGI swapchain) | `ScreenCapturing.cpp` `D3DCaptureState`, `StartCapturingThread`, `UpdateCaptureState` |
| Continuous vs single-frame capture modes | `ScreenCapturing.cpp::StartCapturingThread` (branch on `global.continuousCapture`) |
| DXGI/D3D device + Direct2D device wrapper | `DxgiAPI.cpp/.h`, `D2DState.cpp/.h` |
| CPU-mapped BGRA pixel view + SSE/NEON pixel compare | `BGRATextureView.h` `BGRATextureView::GetPixel`, `PixelsClose<perChannel>` |
| **Edge detection** (flood from cursor to region bounds) | `EdgeDetection.cpp` `DetectEdges`, `FindEdge<PerChannel,IsX,Increment>` |
| Measurement model + unit conversion + formatting | `Measurement.cpp` `Measurement::Convert`(anon)/`Width`/`Height`/`Print`; `Measurement.h` `enum Unit` |
| px→mm physical ratio (from monitor size) | `ToolState.h` `CommonState::GetPhysicalPx2MmRatio` |
| Per-monitor overlay window + D2D render loop | `OverlayUI.cpp` `OverlayUIState::CreateInternal/RunUILoop`, `CreateOverlayUIWindow` |
| Measure-tool overlay draw + WndProc (Esc/click/copy) | `MeasureToolOverlayUI.cpp` `DrawMeasureToolTick`, `MeasureToolWndProc` |
| Bounds-tool overlay draw + WndProc (drag/multi-measure/copy) | `BoundsToolOverlayUI.cpp` `DrawBoundsToolTick`, `BoundsToolWndProc` |
| Copy measurements to clipboard | `Clipboard.cpp` `SetClipboardToMeasurements` |
| Text rendering (per-glyph opacity) | `PerGlyphOpacityTextRender.cpp`, `BGRATextureView.cpp` |
| Coordinate conversions (system↔window space) | `CoordinateSystemConversion.h` `convert::FromSystemToWindow` |
| Settings load (tolerance, continuous, color, units, per-channel) | `Settings.cpp` `Settings::LoadFromFile` |
| Frame timing / constants | `constants.h` `consts::TARGET_FRAME_DURATION` |
| Telemetry | `MeasureToolModuleInterface/trace.cpp` |
| UI tests (bounds/spacing/shortcut) | `Tests/ScreenRuler.UITests/` (was also `Tests/ScreenRuler.UITests.Next/` — historical, not in current tree) |

**Session model (critical):** `Core::StartMeasureTool`/`StartBoundsTool` call `ResetState`, create
**one overlay window per monitor** (`OverlayUIState::Create` → its own UI thread + D2D device), and
for measure mode spawn **one capture thread per monitor** (`StartCapturingThread`). Any monitor's
overlay setting `commonState.closeOnOtherMonitors = true` (Esc/right-click/session end) tears down
**all** monitors. `cursorPosSystemSpace` is written atomically by one `MouseCaptureThread` and read
by every overlay/capture thread — it is the single shared hot value.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md). Bug bodies in this repo's
issue tracker are terse — confirm each claim in source (done for the entries below).

### Pixel edge-detection off-by-one at row/column 0
- **Symptom:** measured region is 1px short on the top/left; the pixel at row 0 or column 0 is never
  treated as an edge.
- **Where:** `EdgeDetection.cpp::FindEdge` — start point is `std::clamp(center, 1, dim-2)` and the
  decrement branch breaks on `--x == 0` / `--y == 0` before testing pixel 0.
- **Root cause:** the loop excludes index 0 from comparison; the outermost pixel is unreachable.
- **Guardrail:** when editing the flood bounds, verify indices `0` and `dim-1` are both testable;
  add a test measuring a region flush against the screen edge. Evidence:
  [#46947](https://github.com/microsoft/PowerToys/issues/46947).

### Total-mode tolerance truncated to 8 bits (`& 0xFF`)
- **Symptom:** with per-color-channel detection **off** (sum-of-channels mode), edge detection behaves
  erratically for large color jumps — a big BGRA difference can wrap and read as "close".
- **Where:** `BGRATextureView.h::PixelsClose<false>` — `score = _mm_sad_epu8(...) & 0xFF` (masked by
  `std::numeric_limits<uint8_t>::max()`) then `score <= tolerance`.
- **Root cause:** the SAD across 4 channels ranges 0–1020, but it is masked to 8 bits before the
  compare, discarding the high bits.
- **Guardrail:** don't mask a multi-channel sum to one channel's width; compare the full 16-bit sum
  (and keep `tolerance` semantics consistent between per-channel and total modes). Test both
  `perColorChannelEdgeDetection` values. Evidence:
  [#46946](https://github.com/microsoft/PowerToys/issues/46946).

### Unit-conversion math (mm 100× wrong on the DPI-fallback path)
- **Symptom:** millimetre readings are 100× too small (and historically cm was reported wrong) when a
  physical monitor size isn't available.
- **Where:** `Measurement.cpp` anonymous `Convert` — the `px2mmRatio <= 0` fallback returns
  `pixels / 96.f / 10.f * 2.54f` for `Millimetre` (≈ `px/96*0.254`) instead of `px/96*25.4`.
- **Root cause:** wrong constant chain in the 96-DPI fallback; the physical-ratio branch is correct,
  so bugs only appear when `GetPhysicalPx2MmRatio` returns ≤ 0.
- **Guardrail:** any change to `Convert` must keep both branches consistent (1 in = 25.4 mm = 2.54 cm
  = 96 px fallback) and be unit-tested for px/in/cm/mm on both the ratio and fallback paths. Evidence:
  [#46945](https://github.com/microsoft/PowerToys/issues/46945),
  [#43367](https://github.com/microsoft/PowerToys/issues/43367).

### Multi-monitor: overlay only on primary / crash with 2+ screens
- **Symptom:** ruler appears only on the primary monitor, or crashes on multi-monitor setups.
- **Where:** `PowerToys.MeasureToolCore.cpp::StartMeasureTool` — the per-monitor loop does
  `if (!overlayUI) return;` (aborts **all** remaining monitors), whereas `StartBoundsTool` uses
  `continue`. Overlay creation is per-monitor in `OverlayUI.cpp::CreateInternal`.
- **Root cause:** a single monitor's failed overlay/D2D init aborts the whole measure session;
  per-monitor lifecycle isn't isolated.
- **Guardrail:** treat each monitor independently — a failure on one must `continue`, not abort the
  set; validate on heterogeneous DPI / mixed-refresh multi-monitor layouts. Evidence:
  [#39195](https://github.com/microsoft/PowerToys/issues/39195),
  [#33345](https://github.com/microsoft/PowerToys/issues/33345),
  [#32205](https://github.com/microsoft/PowerToys/issues/32205).

### Cross-thread cursor read mis-alignment → crash on some machines
- **Symptom:** Measure Tool crashes on startup on some machines (alignment / torn-read fault).
- **Where:** `ToolState.h` `CommonState::cursorPosSystemSpace` (`alignas(8) POINT`, warning 4324
  suppressed) written via `InterlockedExchange64` in `MouseCaptureThread`, read by overlay/capture
  threads. Guarded by `static_assert(sizeof(...) == sizeof(LONG64))`.
- **Root cause:** a 64-bit interlocked exchange on an under-aligned `POINT` is UB and faults on some
  CPUs.
- **Guardrail:** keep the field 8-byte aligned and the `static_assert`; never read/write it
  non-atomically or change its type without re-checking alignment. Evidence:
  [#41555](https://github.com/microsoft/PowerToys/issues/41555), fix
  [PR #41556](https://github.com/microsoft/PowerToys/pull/41556).

### Overlay on wrong virtual desktop / not over target app; black screen
- **Symptom:** ruler opens on the desktop / previous virtual desktop instead of over the active app;
  black screen or "overexposure" (all-white) capture.
- **Where:** `OverlayUI.cpp::CreateOverlayUIWindow` (`WS_EX_TOOLWINDOW` topmost overlay,
  `DWMWA_EXCLUDED_FROM_PEEK`, blur-behind), `ScreenCapturing.cpp` (frame acquisition / swapchain
  resize), `D2DState.cpp`.
- **Root cause:** `WS_EX_TOOLWINDOW` overlays render on all virtual desktops; capture/compose races or
  device-lost during `ResizeBuffers` can blank the frame.
- **Guardrail:** when touching overlay window styles or the capture loop, verify virtual-desktop and
  target-window coverage and handle device-lost / resize on the capture thread. Evidence:
  [#33841](https://github.com/microsoft/PowerToys/issues/33841),
  [#44543](https://github.com/microsoft/PowerToys/issues/44543),
  [#34592](https://github.com/microsoft/PowerToys/issues/34592),
  [#40711](https://github.com/microsoft/PowerToys/issues/40711).

## Review Rules

Enforce these when reviewing or authoring Screen Ruler changes:

- **Test edge detection under both modes.** Any change to `FindEdge`/`PixelsClose`/`DetectEdges` must
  be validated with `perColorChannelEdgeDetection` = true **and** false, and against edge indices
  `0` and `dim-1` (#46946, #46947).
- **Keep unit conversion consistent across both branches.** In `Measurement::Convert`, the
  physical-ratio path and the 96-DPI fallback must agree (1 in = 25.4 mm = 2.54 cm); unit-test px/in/
  cm/mm (#46945).
- **Isolate per-monitor failures.** Overlay/capture setup loops must not let one monitor's failure
  abort the others — `continue`, don't `return` (#39195, #33345).
- **cursorPosSystemSpace is atomic-only.** Keep it `alignas(8)` and access it via the interlocked
  path; do not read the `POINT` field-by-field across threads
  ([Interlocked alignment rules](https://learn.microsoft.com/en-us/windows/win32/sync/interlocked-variable-access)).
- **Overlay teardown is all-or-nothing.** Setting `commonState.closeOnOtherMonitors = true` must
  reliably end every monitor's UI loop and fire `sessionCompletedCallback` exactly once
  (`OverlayUI.cpp::RunUILoop`); don't add early returns that skip the callback.
- **Respect fixed text buffers.** `OverlayBoxText::buffer` is `wchar_t[128]` and `Measurement::Print`
  uses `swprintf_s` into a caller buffer — keep bounded formatting; don't overflow on long
  multi-unit strings.
- **UI tests: serialize desktop state.** New UI-test assemblies under `Tests/` need
  `[assembly: DoNotParallelize]` (shared cursor/Settings/clipboard state), and should inherit the
  repo `TreatWarningsAsErrors=true` rather than disabling it
  ([PR #48842](https://github.com/microsoft/PowerToys/pull/48842) review).
- **No bare relative paths in project files.** Use `$(RepoRoot)` / `$(MSBuildThisFileDirectory)`, not
  `..\..\..\`; keep `Microsoft.Cpp.*.props` import order intact
  ([PR #43920](https://github.com/microsoft/PowerToys/pull/43920),
  [PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).

## Pitfalls

- **Never** mask a multi-channel pixel sum to 8 bits — `PixelsClose<false>`'s `& 0xFF` on a 0–1020
  SAD is a real bug; compare the full width (#46946).
- **Never** assume `FindEdge` covers the whole span — it clamps the start to `[1, dim-2]` and can't
  return an edge at index 0 (#46947).
- **Never** change `Convert` one branch at a time — the DPI-fallback mm path is already 100× off;
  fix both branches together and unit-test (#46945).
- **The overlay deliberately hides the cursor** (`MeasureToolWndProc` `WM_CREATE` loops
  `ShowCursor(false)`); "no cursor" reports (#34746) are this by design — don't "fix" it without
  understanding the crosshair replaces the system cursor.
- **Esc/right-click close only work when the overlay has focus.** The overlay `WndProc` handles
  `VK_ESCAPE`→`WM_CLOSE`; when launched via PowerToys Run the toolbar window may hold focus, so Esc
  appears dead (#42243). Route close through the shared session-completion path, not per-window only.
- **`WS_EX_TOOLWINDOW` overlays span all virtual desktops** — that's why blur-region and
  `DWMWA_EXCLUDED_FROM_PEEK` hacks exist in `CreateOverlayUIWindow`; changing window styles can
  resurrect the "wrong desktop" bugs (#33841).
- **Continuous mode excludes the overlay from capture** (`excludeFromCapture = continuousCapture`,
  `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`) and re-captures every frame; single-frame mode
  captures once and caches. Don't blur the two paths in `StartCapturingThread`.
- **px→mm depends on the monitor reporting a physical size** — `GetPhysicalPx2MmRatio` returns ≤ 0
  when `width_physical`/`width_mm` are unavailable, silently switching to the (buggy) 96-DPI fallback.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**; then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you on recurring
themes and measurably lowers your catch rate on the PR's actual issues. If a symptom doesn't map to
a row, reason from the source, not the map. Best for planning / triage; a targeted checklist (not a
script) for review.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression + decision list.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Screen Ruler PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/MeasureTool/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/MeasureTool)
- [Windows.Graphics.Capture](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/screen-capture) ·
  [Interlocked variable access](https://learn.microsoft.com/en-us/windows/win32/sync/interlocked-variable-access) ·
  [SetWindowDisplayAffinity](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowdisplayaffinity)
