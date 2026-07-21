# Screen Ruler (Measure Tool) PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
maps to the Regression Playbook / Review Rule it enforces. Source root: `src/modules/MeasureTool/`.

## General (any Screen Ruler PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] A test accompanies each behavior change (`src/modules/MeasureTool/Tests/`).
- [ ] No bare relative paths in `.vcxproj`/`.csproj`; uses `$(RepoRoot)` / `$(MSBuildThisFileDirectory)`;
      `Microsoft.Cpp.*.props` import order unchanged.

## Edge detection / pixel compare (`EdgeDetection.cpp`, `BGRATextureView.h`)
- [ ] Change validated with `perColorChannelEdgeDetection` = true **and** false.
- [ ] Indices `0` and `dim-1` both reachable as edges (no off-by-one; #46947).
- [ ] Multi-channel sums not truncated to 8 bits — no `& 0xFF` on the SAD (#46946).
- [ ] `GetPixel` bounds/pitch respected (`pitch = RowPitch/4`, not width).

## Unit conversion / measurement (`Measurement.cpp`, `ToolState.h::GetPhysicalPx2MmRatio`)
- [ ] `Convert` physical-ratio branch **and** 96-DPI fallback agree (1 in = 25.4 mm = 2.54 cm; #46945).
- [ ] Unit-tested for px / in / cm / mm on both paths.
- [ ] `Width()/Height()` still add the inclusive `+1` pixel; `Print` stays within the caller buffer.

## Multi-monitor / session lifecycle (`PowerToys.MeasureToolCore.cpp`, `OverlayUI.cpp`)
- [ ] Per-monitor setup loop `continue`s on a single-monitor failure — never `return`/abort all
      (#39195, #33345).
- [ ] `closeOnOtherMonitors = true` reliably ends every UI loop and fires `sessionCompletedCallback`
      exactly once.
- [ ] Overlay window styles / virtual-desktop coverage unchanged unless intended (#33841).

## Capture (`ScreenCapturing.cpp`, `DxgiAPI.cpp`, `D2DState.cpp`)
- [ ] Continuous vs single-frame paths kept distinct; `excludeFromCapture` tracks `continuousCapture`.
- [ ] Device-lost / `ResizeBuffers` handled on the capture thread; no blank/overexposed frame (#40711).
- [ ] Frame callbacks guarded (`frameArrivedMutex`) against teardown races.

## Concurrency (`ToolState.h`, `MouseCaptureThread`)
- [ ] `cursorPosSystemSpace` stays `alignas(8)` and accessed via `InterlockedExchange64`; the
      `static_assert(sizeof == LONG64)` preserved (#41555).
- [ ] No field-by-field cross-thread reads of the shared `POINT`.

## Input / UX (`MeasureToolOverlayUI.cpp`, `BoundsToolOverlayUI.cpp`, `dllmain.cpp`)
- [ ] Esc / right-click close routed through the shared session-completion path, not per-window only
      (#42243).
- [ ] Deliberate cursor hide (`ShowCursor(false)`) preserved; crosshair still shown (#34746).
- [ ] Hotkey parse fallback (Win+Ctrl+Shift+M) intact; GPO gate honored.

## UI tests (`Tests/ScreenRuler.UITests*`)
- [ ] New test assembly has `[assembly: DoNotParallelize]` (shared desktop state).
- [ ] Inherits repo `TreatWarningsAsErrors=true`; `RootNamespace` matches file namespaces.
