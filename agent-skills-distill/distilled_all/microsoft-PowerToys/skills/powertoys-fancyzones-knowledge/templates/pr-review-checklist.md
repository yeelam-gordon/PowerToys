# FancyZones PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
links to the Regression Playbook / Review Rule it enforces.

## General (any FancyZones PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] A test accompanies each behavior change where a harness exists (`FancyZonesTests`,
      `FancyZonesEditor.UnitTests`, `*.UITests`); the live hook/drag path has no unit harness —
      manual validation steps listed.
- [ ] No bare relative paths in `.vcxproj`/`.csproj`; uses `$(RepoRoot)`; deps centrally managed.

## Teardown / lifecycle (`ZonesOverlay.cpp`, `WorkArea.cpp`, `OnThreadExecutor.cpp`)
- [ ] `thread::join()` guarded by `joinable()`; ctor early-returns can't leave a thread joined blindly.
- [ ] Render thread stopped (`m_zonesOverlay.reset()`) **before** `FreeZonesOverlayWindow`.
- [ ] Shutdown/state flags written under the same mutex the waiter's `cv.wait` uses.
- [ ] Overlay HWND not recycled while a render target is still drawing into it.

## Drag / snap state (`FancyZones.cpp`, `WindowMouseSnap.cpp`, `DraggingState.cpp`)
- [ ] No `WorkArea*`/`const&` held across `WorkAreaConfiguration::Clear()`; `MoveSizeEnd()` called first.
- [ ] Window-destroyed-mid-drag path uses `Abort()`, not `MoveSizeEnd()`.
- [ ] `MoveSizeEnd()` always clears dragging state, even when the snapper is null.
- [ ] Any dispatched `WM_PRIV_*` has its `EVENT_*` registered in the WinEvent hook.

## Keyboard handling (`FancyZones.cpp::OnKeyDown`, `WindowKeyboardSnap.cpp`)
- [ ] Only real snap/quick-layout hotkeys and the **bare** Shift (during drag) are swallowed; never
      `Shift+<other>`.
- [ ] Quick-layout digit switch gated behind Win+Ctrl+Alt (both dragging and idle).
- [ ] Override-Windows-Snap swallow decision matches the actual snap action and honors the setting.

## Multi-monitor / virtual desktop / data (`MonitorUtils.cpp`, `VirtualDesktop.cpp`, `FancyZonesData/`)
- [ ] Virtual-desktop id resolved defensively (registry layout varies by OS build).
- [ ] Work-area/layout id used for zone-history lookup matches the target monitor.
- [ ] JSON writes (`applied-layouts.json`, `app-zone-history.json`, layouts) serialized + error-handled.
- [ ] `SpanZonesAcrossMonitors` and monitor-change paths rebuild work areas without dangling refs.

## Editor (`editor/FancyZonesEditor/`)
- [ ] End-user strings localizable with translator comments (e.g. spacing/highlight labels).
- [ ] Layout math (spacing, highlight distance) matches the native side's interpretation.

## CLI (`FancyZonesCLI/`)
- [ ] Brace-less GUIDs accepted; PowerShell script-block case detected with a friendly message.
- [ ] Per-subcommand `--help` present for new commands.
