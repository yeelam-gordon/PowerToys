# Screen Ruler (Measure Tool) Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each claim
in source before acting. Symptoms map to `src/modules/MeasureTool/`. Issue bodies in this corpus are
terse (title-only); the technical rows below were verified directly against source.

## Key Decisions (context for the playbooks)

- **One overlay + one capture thread per monitor.** `Core::StartMeasureTool` iterates
  `MonitorInfo::GetMonitors(true)`, creating an `OverlayUIState` (own UI thread + D2D device) and a
  capture thread each (`PowerToys.MeasureToolCore.cpp`, `OverlayUI.cpp`, `ScreenCapturing.cpp`).
  `StartBoundsTool` skips capture threads (bounds tool draws user-dragged rectangles, no pixel flood).
- **All-or-nothing teardown via a shared flag.** `CommonState::closeOnOtherMonitors` (atomic bool) is
  polled by every overlay `RunUILoop` and capture loop; whoever sets it ends the whole session and
  fires `sessionCompletedCallback` once (`OverlayUI.cpp::CreateInternal`).
- **Single atomic cursor value.** One `MouseCaptureThread` writes `cursorPosSystemSpace` via
  `InterlockedExchange64`; all overlay/capture threads read it. It is `alignas(8)` with a
  `static_assert(sizeof == LONG64)` — the fix for the alignment crash.
- **Two edge-detection modes.** `PixelsClose<perChannel>` (SSE/NEON): per-channel compares each BGRA
  channel distance against `tolerance`; total mode sums channel diffs. `DetectEdges` picks the
  template via the `perColorChannelEdgeDetection` setting.
- **Two capture modes.** Continuous mode (`continuousCapture`) re-captures every frame and excludes
  the overlay from capture (`WDA_EXCLUDEFROMCAPTURE`); single-frame mode captures once, caches the
  bitmap, and re-runs edge detection against the cached texture (`StartCapturingThread`).
- **px→mm from physical monitor size.** `GetPhysicalPx2MmRatio` = `width_mm / width_physical`; when
  unavailable (≤ 0) `Measurement::Convert` falls back to a 96-DPI assumption (whose mm branch is bugged).
- **Overlay is a topmost tool window with cursor hidden.** `CreateOverlayUIWindow` uses
  `WS_EX_TOOLWINDOW | WS_EX_TOPMOST | WS_EX_NOREDIRECTIONBITMAP`, blur-behind + `EXCLUDED_FROM_PEEK`
  to avoid Win+Tab/virtual-desktop artifacts; the measure overlay hides the system cursor and draws a
  crosshair.

## Regression Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| Edge off-by-one | Region 1px short at row/col 0 | `EdgeDetection.cpp::FindEdge` (clamp `[1,dim-2]`, break at 0) | Index 0 never tested as edge | Make `0` and `dim-1` reachable; edge-flush test | [#46947](https://github.com/microsoft/PowerToys/issues/46947) |
| Tolerance truncation | Total-mode detection erratic on big color jumps | `BGRATextureView.h::PixelsClose<false>` `& 0xFF` | 0–1020 SAD masked to 8 bits | Compare full-width sum; test both modes | [#46946](https://github.com/microsoft/PowerToys/issues/46946) |
| Unit math | mm 100× too small (DPI fallback); cm wrong | `Measurement.cpp::Convert` fallback `px/96/10*2.54` | Wrong constant chain in 96-DPI branch | Both branches agree (1in=25.4mm=2.54cm); unit tests | [#46945](https://github.com/microsoft/PowerToys/issues/46945), [#43367](https://github.com/microsoft/PowerToys/issues/43367) |
| Multi-monitor | Only primary shows / crash 2+ screens | `PowerToys.MeasureToolCore.cpp::StartMeasureTool` `return` on null overlay | One monitor's failure aborts all | `continue` per monitor; isolate lifecycle | [#39195](https://github.com/microsoft/PowerToys/issues/39195), [#33345](https://github.com/microsoft/PowerToys/issues/33345), [#32205](https://github.com/microsoft/PowerToys/issues/32205) |
| Cursor alignment | Crash on startup on some machines | `ToolState.h::cursorPosSystemSpace`; `MouseCaptureThread` | 64-bit interlocked on under-aligned `POINT` | `alignas(8)` + `static_assert`; atomic access only | [#41555](https://github.com/microsoft/PowerToys/issues/41555), [PR #41556](https://github.com/microsoft/PowerToys/pull/41556) |
| Overlay/desktop | Wrong virtual desktop / not over app; black screen; overexposure | `OverlayUI.cpp::CreateOverlayUIWindow`; `ScreenCapturing.cpp`; `D2DState.cpp` | `WS_EX_TOOLWINDOW` spans desktops; capture/resize/device-lost race | Verify desktop coverage; handle device-lost on capture thread | [#33841](https://github.com/microsoft/PowerToys/issues/33841), [#44543](https://github.com/microsoft/PowerToys/issues/44543), [#34592](https://github.com/microsoft/PowerToys/issues/34592), [#40711](https://github.com/microsoft/PowerToys/issues/40711), [#37972](https://github.com/microsoft/PowerToys/issues/37972) |
| Close/focus | Esc doesn't close when opened via PowerToys Run | overlay `WndProc` `VK_ESCAPE`→`WM_CLOSE`; toolbar focus | Overlay lacks focus; per-window close only | Route close through shared session-completion | [#42243](https://github.com/microsoft/PowerToys/issues/42243) |
| By-design UX | "No mouse cursor" | `MeasureToolOverlayUI.cpp` `WM_CREATE` `ShowCursor(false)` | Intentional crosshair replaces cursor | Document; don't regress crosshair | [#34746](https://github.com/microsoft/PowerToys/issues/34746) |
| Activation | Activates when disabled / hotkey conflict | `dllmain.cpp::on_hotkey/parse_hotkey`; GPO gate | Enable/hotkey state handling | Honor `m_enabled` + GPO; validate hotkey parse | [#48613](https://github.com/microsoft/PowerToys/issues/48613), [#33075](https://github.com/microsoft/PowerToys/issues/33075) |

## Common Practices (enforced in review)

- **Test both edge-detection modes and both capture modes.** Toggle `perColorChannelEdgeDetection`
  and `continuousCapture`; they exercise different code paths.
- **Keep conversion math branch-consistent and unit-tested.** `Convert` has a physical-ratio path and
  a 96-DPI fallback; they must not disagree.
- **Per-monitor isolation.** Overlay/capture setup and teardown iterate monitors independently; never
  let one monitor's failure abort the others, and always fire `sessionCompletedCallback` once.
- **Atomic shared cursor.** Preserve `alignas(8)` + `InterlockedExchange64` for `cursorPosSystemSpace`.
- **Bounded formatting.** `OverlayBoxText::buffer` is `wchar_t[128]`; `Measurement::Print` uses
  `swprintf_s` — keep within bounds.
- **UI tests.** New assemblies under `Tests/` use `[assembly: DoNotParallelize]` (shared cursor /
  Settings window / clipboard) and inherit repo `TreatWarningsAsErrors=true`.
- **Packaging/build.** Use `$(RepoRoot)` / `$(MSBuildThisFileDirectory)` not relative paths; don't
  reorder `Microsoft.Cpp.*.props` imports (#43920, #44639).

---
*Corpus: 12 merged PRs, 121 review comments, 30 bug issues (title-level) + source verification against
`src/modules/MeasureTool`. Technical regression rows confirmed directly in source; issue bodies in the
raw corpus were empty, so those symptoms are grounded on titles + code, not maintainer prose.*
