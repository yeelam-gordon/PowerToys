# Screen Ruler (Measure Tool) Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This file is the historical evidence store behind `SKILL.md`.

**Role split:** `SKILL.md` owns the current regression playbooks, review rules, and actionable
guardrails. This ledger retains provenance: source locations, issue/PR evidence, chronology,
maintainer decisions, unresolved clusters, and evidence caveats. Do not repeat the playbook
mechanics here; revalidate source observations in `src/modules/MeasureTool/` before acting.

## Evidence ledger

| ID | Evidence / observation | Source location | History / provenance | Caveat |
|---|---|---|---|---|
| SR-E01 | Measure mode creates one overlay UI state and one capture thread per monitor; bounds mode creates overlays but no pixel-capture threads. | `PowerToys.MeasureToolCore.cpp` · `Core::StartMeasureTool`, `StartBoundsTool`; `OverlayUI.cpp`; `ScreenCapturing.cpp` | Source verification | Thread/lifecycle counts describe the inspected implementation. |
| SR-E02 | Every overlay UI loop and capture loop observes the shared atomic `closeOnOtherMonitors`; session completion is coordinated from `OverlayUIState::CreateInternal`. | `ToolState.h` · `CommonState`; `OverlayUI.cpp` · `CreateInternal`, `RunUILoop` | Source verification | Callback-once behavior must be rechecked if ownership changes. |
| SR-E03 | One mouse-capture thread writes the shared cursor position through `InterlockedExchange64`; the `POINT` storage is `alignas(8)` and size-checked against `LONG64`. | `PowerToys.MeasureToolCore.cpp` · `MouseCaptureThread`; `ToolState.h` · `cursorPosSystemSpace` | Startup crash [#41555](https://github.com/microsoft/PowerToys/issues/41555) → fix [PR #41556](https://github.com/microsoft/PowerToys/pull/41556) | Issue/PR establish the alignment fix; current atomic readers should still be audited. |
| SR-E04 | Edge comparison has per-channel and total-distance template modes selected by `perColorChannelEdgeDetection`. The total SAD spans 0–1020, but current source masks it to 8 bits before comparison. | `BGRATextureView.h` · `PixelsClose<perChannel>`; `EdgeDetection.cpp` · `DetectEdges` | Known current violation; report [#46946](https://github.com/microsoft/PowerToys/issues/46946) | SIMD implementations may differ by architecture; verify both SSE and NEON paths where applicable. |
| SR-E05 | Current `FindEdge` makes index 0 unreachable through its clamp and decrement-loop termination. | `EdgeDetection.cpp` · `FindEdge` | Known current violation; report [#46947](https://github.com/microsoft/PowerToys/issues/46947) | Preserve the full-span requirement when fixing either direction. |
| SR-E06 | Continuous capture reacquires frames and excludes the overlay via display affinity; single-frame mode captures once, caches the bitmap, and reruns detection against the cached texture. | `ScreenCapturing.cpp` · `StartCapturingThread`; overlay display-affinity setup | Source verification | These are distinct runtime paths despite sharing detection logic. |
| SR-E07 | Physical pixel-to-mm ratio is monitor-width-mm divided by physical pixel width; unavailable data selects a 96-DPI fallback. Current millimetre fallback `pixels / 96 / 10 * 2.54` is 100× too small. | `ToolState.h` · `GetPhysicalPx2MmRatio`; `Measurement.cpp` · `Convert` | Known current violation; reports [#43367](https://github.com/microsoft/PowerToys/issues/43367), [#46945](https://github.com/microsoft/PowerToys/issues/46945) | Fix and test both the physical-ratio and fallback branches together. |
| SR-E08 | A null overlay in `StartMeasureTool` used `return`, while the bounds-tool loop used `continue`, allowing one monitor failure to abort remaining measure overlays. | `PowerToys.MeasureToolCore.cpp` · `StartMeasureTool`, `StartBoundsTool` | Multi-monitor cluster [#32205](https://github.com/microsoft/PowerToys/issues/32205), [#33345](https://github.com/microsoft/PowerToys/issues/33345), later [#39195](https://github.com/microsoft/PowerToys/issues/39195) | The issues establish recurring multi-monitor symptoms, not necessarily one cause for every report. |
| SR-E09 | The overlay is a topmost tool window using `WS_EX_TOOLWINDOW`, `WS_EX_TOPMOST`, `WS_EX_NOREDIRECTIONBITMAP`, blur-behind, and exclusion from Peek; measure mode hides the system cursor and draws a crosshair. | `OverlayUI.cpp` · `CreateOverlayUIWindow`; `MeasureToolOverlayUI.cpp` · `WM_CREATE` | Virtual-desktop/capture reports [#33841](https://github.com/microsoft/PowerToys/issues/33841), [#34592](https://github.com/microsoft/PowerToys/issues/34592), [#37972](https://github.com/microsoft/PowerToys/issues/37972), [#40711](https://github.com/microsoft/PowerToys/issues/40711), [#44543](https://github.com/microsoft/PowerToys/issues/44543); cursor report [#34746](https://github.com/microsoft/PowerToys/issues/34746) | #34746 records intentional UX; the other reports span several rendering/capture failure modes. |
| SR-E10 | Escape is handled by the overlay `WndProc`, but launch paths can leave focus on the toolbar rather than the overlay. | `MeasureToolOverlayUI.cpp` · `MeasureToolWndProc`; toolbar/overlay focus path | PowerToys Run close report [#42243](https://github.com/microsoft/PowerToys/issues/42243) | Focus explains the observed path but should be reproduced before assigning causality to a new report. |
| SR-E11 | Module activation passes through enabled-state, hotkey parsing, and GPO handling. | `MeasureToolModuleInterface/dllmain.cpp` · `on_hotkey`, `parse_hotkey`, policy gate | Activation reports [#33075](https://github.com/microsoft/PowerToys/issues/33075), later [#48613](https://github.com/microsoft/PowerToys/issues/48613) | The two reports may represent different activation failures. |
| SR-E12 | Overlay text uses a fixed `wchar_t[128]` buffer and bounded `swprintf_s` formatting. | `OverlayBoxText`; `Measurement.cpp` · `Measurement::Print` | Source verification | Buffer safety is a review constraint, not a linked regression in this corpus. |
| SR-E13 | UI-test assemblies share desktop state and have received review direction to use `[assembly: DoNotParallelize]`; project files inherit warnings-as-errors. | `src/modules/MeasureTool/Tests/`; project configuration | PR [#48842](https://github.com/microsoft/PowerToys/pull/48842) review | Review evidence applies to tests sharing cursor, Settings, or clipboard state. |
| SR-E14 | Build reviews rejected fragile relative paths and import-order changes in C++ project files. | Measure Tool project files and repository build conventions | PR reviews [#43920](https://github.com/microsoft/PowerToys/pull/43920), [#44639](https://github.com/microsoft/PowerToys/pull/44639) | Repo-wide build convention, retained because it appeared in module review history. |

## Decision ledger

| ID | Decision / review outcome | Basis | Status |
|---|---|---|---|
| SR-D01 | Keep shared cursor storage 8-byte aligned, size-checked, and accessed atomically. | #41555 → PR #41556 | Accepted and implemented |
| SR-D02 | Evaluate edge changes in both per-channel and total modes, including both image boundaries. | #46946, #46947; two template paths | Ongoing review decision |
| SR-D03 | Treat physical-ratio and 96-DPI conversion branches as one contract. | #43367, #46945; `Measurement::Convert` | Ongoing review decision |
| SR-D04 | Multi-monitor setup reports exist, but the cited issues do not establish null-overlay creation or `continue` as the accepted fix. | #32205, #33345, #39195; differing `return`/`continue` implementations | Unresolved attribution |
| SR-D05 | Each per-monitor overlay thread currently invokes `sessionCompletedCallback()`; there is no once-guard establishing single completion ownership. | `OverlayUI.cpp` callback sites and shared close flag | Current source observation; ownership risk remains |
| SR-D06 | Keep the hidden cursor/crosshair behavior classified as intentional unless product direction changes. | #34746; overlay creation code | By-design decision |
| SR-D07 | Serialize UI tests that mutate shared desktop state and retain warnings-as-errors. | PR #48842 review | Maintainer review decision |
| SR-D08 | Use repository-rooted MSBuild paths and preserve `Microsoft.Cpp.*.props` import order. | PR #43920 and #44639 reviews | Maintainer review decision |

## Evidence clusters (lifecycle noted)

- **Multi-monitor reliability:** [#32205](https://github.com/microsoft/PowerToys/issues/32205),
  [#33345](https://github.com/microsoft/PowerToys/issues/33345), and
  [#39195](https://github.com/microsoft/PowerToys/issues/39195) span missing overlays and crashes;
  all three reports closed on April 18, 2026, but not every report is proven to share the
  null-overlay early return.
- **Virtual desktop / capture rendering:** [#33841](https://github.com/microsoft/PowerToys/issues/33841),
  [#34592](https://github.com/microsoft/PowerToys/issues/34592),
  [#37972](https://github.com/microsoft/PowerToys/issues/37972),
  [#40711](https://github.com/microsoft/PowerToys/issues/40711), and
  [#44543](https://github.com/microsoft/PowerToys/issues/44543) cover wrong-desktop, black-frame,
  white/overexposed, resize, and device-loss-adjacent symptoms. Preserve them as a cluster until
  each path is reproduced and localized.
- **Activation while disabled / hotkey conflict:** [#33075](https://github.com/microsoft/PowerToys/issues/33075)
  and [#48613](https://github.com/microsoft/PowerToys/issues/48613) remain separate evidence points
  around enable, policy, and hotkey state.

## Evidence caveats

- Corpus basis: 12 merged PRs, 121 review comments, and 30 bug issues, plus direct source
  verification under `src/modules/MeasureTool/`.
- Issue bodies include reproduction detail, but they do not prove the technical mechanisms or
  source locations; those were derived from current code and remain hypotheses where labeled.
- Source locations and observed defects may have changed after capture. Re-check the current branch,
  especially for #46945–#46947.
- The ledger intentionally omits repeated symptom → root cause → guardrail instructions already
  maintained in `SKILL.md`.
