# Screen Ruler (Measure Tool) Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table. Source root:
`src/modules/MeasureTool/`.

## Report
- **Symptom:**
- **Repro / inputs:**
- **Monitors / DPI / scaling:**
- **Tool:** bounds / horizontal / vertical / cross · continuous capture on? · per-channel detection on?
- **Launched via:** hotkey / PowerToys Run / toolbar

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Measurement 1px short at top/left edge | `EdgeDetection.cpp::FindEdge` (clamp `[1,dim-2]`) | Off-by-one | Edge off-by-one |
| Edge detection erratic in total (non-per-channel) mode | `BGRATextureView.h::PixelsClose<false>` (`& 0xFF`) | 8-bit truncation | Tolerance truncation |
| Wrong mm / cm / inch value | `Measurement.cpp::Convert`; `GetPhysicalPx2MmRatio` | Unit math | Unit conversion |
| Ruler only on primary monitor / crash 2+ screens | `PowerToys.MeasureToolCore.cpp::StartMeasureTool` (`return` vs `continue`) | Per-monitor abort | Multi-monitor |
| Crash on startup on some machines | `ToolState.h::cursorPosSystemSpace`; `MouseCaptureThread` | Alignment/atomic | Cursor alignment |
| Overlay on wrong virtual desktop / not over app | `OverlayUI.cpp::CreateOverlayUIWindow` (`WS_EX_TOOLWINDOW`) | Overlay coverage | Overlay/desktop |
| Black screen / overexposure / blank capture | `ScreenCapturing.cpp` (frame/resize/device-lost); `D2DState.cpp` | Capture race | Overlay/desktop |
| No mouse cursor while ruler active | `MeasureToolOverlayUI.cpp` `WM_CREATE` `ShowCursor(false)` | By-design | (Pitfalls) |
| Esc doesn't close (opened via PowerToys Run) | overlay `WndProc` `VK_ESCAPE`; focus ownership | Focus/close | (Pitfalls #42243) |
| Tool activates when disabled / unexpected hotkey | `dllmain.cpp::on_hotkey/parse_hotkey`; GPO gate | Activation | Module Map |
| Copy to clipboard missing/garbled | `Clipboard.cpp::SetClipboardToMeasurements`; `CopyToClipboard` | Clipboard | Module Map |
| Continuous mode captures overlay / flicker | `ScreenCapturing.cpp::StartCapturingThread`; `excludeFromCapture` | Capture mode | Module Map |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues in the Regression Catalog for a prior fix/guardrail.
3. Reproduce with the reporter's inputs (note tool, capture mode, per-channel flag, monitor layout).
4. Add/extend a test under `src/modules/MeasureTool/Tests/` before fixing.
