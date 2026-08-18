# GrabAndMove PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
links to a Regression Playbook / Review Rule in `SKILL.md`. All paths under `src/modules/GrabAndMove/`.

## General (any GrabAndMove PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] Change validated manually — this module has almost no unit tests; state the manual repro.
- [ ] No bare relative paths in `.vcxproj`; uses `$(RepoRoot)`.

## Low-level hooks — modifier handling (`main.cpp::KeyboardProc`)
- [ ] Injected events bailed first (`LLKHF_INJECTED`).
- [ ] Held non-modifier keys tracked by `g_keyHeld[256]` transition, **not** `KF_REPEAT`.
- [ ] Every swallowed modifier keydown (`return 1`) has a `ReplayAbsorbedModifier` path when no drag
      consumed it; Win replays **both** down and up.
- [ ] Non-modifier key while modifier absorbed → modifier replayed before the key.
- [ ] Game Mode / remote-session suppression rules unchanged unless intended.

## Low-level hooks — mouse (`main.cpp::MouseProc`)
- [ ] Injected events bailed first (`LLMHF_INJECTED`).
- [ ] Drag/resize only starts when `IsActivationModifierPressed()` and target passes `ResolveTargetWindow` + `IsExcluded`.
- [ ] Resize gated on `WS_THICKFRAME`; `MIN_WINDOW_WIDTH/HEIGHT` respected.
- [ ] Click swallow (`return 1`) balanced — no unmatched button-up reaching the target (two-button case).
- [ ] Move throttle (`THROTTLE_INTERVAL_MS`) preserved; final position flushed on button-up.

## Thread-safety (`WinEventProc`, `LoadSettingsFromFile`, `SettingsWatcherThread`)
- [ ] `g_excludedCache` touched **only** on the main message-pump thread; settings thread uses
      `PostMessage(WM_INVALIDATE_EXCLUDED_CACHE)`.
- [ ] Excluded-apps list shared via `atomic<shared_ptr<const vector<wstring>>>` snapshot, not a mutable container.
- [ ] `WinEventProc` resets both `g_heldNonAltKeyCount` and `g_keyHeld` on foreground change.
- [ ] No new shared global written from >1 thread without atomics/marshalling.

## Window filtering (`ResolveTargetWindow`, `IsSystemClass`, `IsExcluded`)
- [ ] New shell surface excluded by class **and** process path (not `WindowFromPoint` guessing).
- [ ] `GetAncestor(GA_ROOT)` normalization intact; overlay/msg windows rejected.

## Overlay (`PrepareOverlayMetrics`, `CornerRadiusForWindow`, `DrawOverlayBorder`, `RenderOverlayContent`)
- [ ] Remote-session branch forces square corners (radius 0).
- [ ] DPI scaling applied to corner radius + border thickness.
- [ ] Metrics computed on cold path only (drag/resize start, un-maximize), not the mouse-move hot path.

## Move / resize math (`HandleDragMove`, `HandleDragResize`)
- [ ] Maximized restore anchors proportionally to the **current** cursor `pt`; move and resize consistent.
- [ ] `SetWindowPos` flags preserved (`SWP_NOACTIVATE | SWP_ASYNCWINDOWPOS`, etc.).

## Settings / mapping
- [ ] Modifier mapping in sync: C++ `GrabAndMoveModifier { Alt=0, Win=1 }` ↔ C# int 0/1.
- [ ] New setting key read in `LoadSettingsFromFile` and surfaced in `GrabAndMoveViewModel.cs`.

## Build / toolset
- [ ] New native `.vcxproj` mirrors canonical `Microsoft.Windows.CppWinRT` NuGet wiring (avoids LNK2038).
- [ ] `pch.h` includes STL headers used explicitly (`<atomic>`); no reliance on transitive includes.
- [ ] Toolset-compat fixes go into the dependency (vcpkg + patch), not bespoke shim headers.

## Style
- [ ] Comments plain and factual — no hyphenated compound nouns / flowery wording.
