# GrabAndMove Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

Historical evidence for the PowerToys **GrabAndMove** module. Issues and PRs are in
`microsoft/PowerToys`; source anchors are under `src/modules/GrabAndMove/`.

> **Role split:** `SKILL.md` owns current mechanics, guardrails, and review workflow. This ledger owns
> provenance: chronology, exact source anchors, reviewer decisions, unresolved report clusters, and
> caveats. Confirm current behavior in source before applying historical evidence.

> **Evidence caveat:** GrabAndMove is a new module, first landed in
> [PR #47024](https://github.com/microsoft/PowerToys/pull/47024) in April 2026. The corpus is roughly
> 11 PRs plus a burst of reports, with no dedicated unit-test suite. Open issues below are symptom
> clusters, not confirmed root causes.

## Chronology and evidence ledger

| Sequence | Evidence | Decision or observed regression | Exact source anchors |
|---|---|---|---|
| 1 | [PR #47024](https://github.com/microsoft/PowerToys/pull/47024) | Initial module landed as a separate executable driven by global low-level keyboard/mouse hooks and a foreground WinEvent hook. | `GrabAndMoveModuleInterface/dllmain.cpp`; `GrabAndMove/main.cpp::wWinMain`, `KeyboardProc`, `MouseProc`, `WinEventProc` |
| 2 | [PR #47033](https://github.com/microsoft/PowerToys/pull/47033) | Added the module/OOBE assets omitted from the initial integration. | `src/settings-ui/Settings.UI/Assets/Settings/Icons/GrabAndMove.png`; `.../Modules/GrabAndMove.png`; `.../Modules/OOBE/GrabAndMove.gif` |
| 3 | [PR #47052](https://github.com/microsoft/PowerToys/pull/47052), commit `ea37c3a` | Added Win as a selectable modifier and coordinate improvements. Review found two durable defects: `KF_REPEAT` was incorrectly inferred from the LL-hook flags, and the settings thread raced the main thread on `g_excludedCache`. The accepted design uses transition-based held-key state, foreground reset, an immutable excluded-app snapshot, and message-based cache invalidation. | `main.cpp::KeyboardProc`; `g_keyHeld`; `g_heldNonAltKeyCount`; `WinEventProc`; `LoadSettingsFromFile`; `IsExcluded`; `g_excludedApps`; `g_excludedCache`; `WM_INVALIDATE_EXCLUDED_CACHE` |
| 4 | [PR #47261](https://github.com/microsoft/PowerToys/pull/47261), closes [#47257](https://github.com/microsoft/PowerToys/issues/47257) | Fixed absorbed Alt remaining internally pressed after another key arrived. The accepted fix clears `g_altPressed` with `g_altAbsorbed` before replay. | `main.cpp::KeyboardProc`, non-Alt-key branch containing `g_altAbsorbed`, `g_dragConsumedAlt`, and `ReplayAbsorbedModifier(false)` |
| 5 | [PR #47302](https://github.com/microsoft/PowerToys/pull/47302) | Hardened target filtering for desktop/Explorer and shell surfaces. Class-only filtering was insufficient because shell processes share generic classes. | `main.cpp::ResolveTargetWindow`; `IsSystemClass`; `IsExcluded`; `GetAncestor(..., GA_ROOT)` |
| 6 | [PR #47326](https://github.com/microsoft/PowerToys/pull/47326) | Completed modifier replay behavior, including replaying Win down and up when no drag consumed it. | `main.cpp::KeyboardProc`; `ReplayAbsorbedModifier` |
| 7 | [PR #47910](https://github.com/microsoft/PowerToys/pull/47910) | Fixed CI `LNK2038` C++/WinRT-version mismatch by restoring canonical pinned CppWinRT NuGet wiring. Review also rejected relying on transitive STL includes and preferred dependency patches over bundled compatibility shims. | `GrabAndMove/GrabAndMove.vcxproj`; `GrabAndMove/pch.h`; historical `deps/spdlog-msvc-fix` discussion |
| 8 | [PR #48474](https://github.com/microsoft/PowerToys/pull/48474) | Introduced the warning-gold overlay appearance; wording was later cited in review as an example of comments that should remain plain and factual. | `main.cpp::RenderOverlayContent`; overlay fill/border state |
| 9 | [PR #48999](https://github.com/microsoft/PowerToys/pull/48999) | Established Remote Desktop exceptions: square overlay corners, foreground-based target selection, and no Game Mode suppression. | `main.cpp::CornerRadiusForWindow`; `PrepareOverlayMetrics`; `ResolveTargetWindow`; `IsSuppressedByGameMode`; `SM_REMOTESESSION` branches |
| 10 | [PR #49118](https://github.com/microsoft/PowerToys/pull/49118), report [#49123](https://github.com/microsoft/PowerToys/issues/49123) | Review found maximized-window restoration anchored move to stale `g_dragStart` rather than live `pt`; move and resize were aligned on proportional cursor anchoring. | `main.cpp::HandleDragMove`; `HandleDragResize`; `SW_RESTORE` branches |
| 11 | Review on [PR #49121](https://github.com/microsoft/PowerToys/pull/49121) | Prevented a second mouse button from overwriting an existing pending drag/resize and leaking an unmatched button-up to the target. | `main.cpp::MouseProc`; pending-press state |

## Issue evidence by resolved area

| Area | Reports | What the reports establish | Source anchors |
|---|---|---|---|
| Held/stuck keys | [#48190](https://github.com/microsoft/PowerToys/issues/48190), [#47802](https://github.com/microsoft/PowerToys/issues/47802), [#49037](https://github.com/microsoft/PowerToys/issues/49037), [#48215](https://github.com/microsoft/PowerToys/issues/48215) | Repeated user-visible failures around modifiers/plain keys support the held-state and foreground-reset review findings from #47052; individual reports do not independently prove the same root cause. | `KeyboardProc`; `g_keyHeld`; `g_heldNonAltKeyCount`; `WinEventProc` |
| Modifier absorb/replay | [#47585](https://github.com/microsoft/PowerToys/issues/47585), [#47787](https://github.com/microsoft/PowerToys/issues/47787), [#47774](https://github.com/microsoft/PowerToys/issues/47774), [#48121](https://github.com/microsoft/PowerToys/issues/48121), [#47715](https://github.com/microsoft/PowerToys/issues/47715) | Normal Alt/Win behavior or foreign shortcuts broke while absorption was enabled; #47261/#47326 contain the confirmed replay decisions. | `KeyboardProc`; `ReplayAbsorbedModifier` |
| Target filtering | [#47926](https://github.com/microsoft/PowerToys/issues/47926), [#48056](https://github.com/microsoft/PowerToys/issues/48056), [#47832](https://github.com/microsoft/PowerToys/issues/47832), [#48081](https://github.com/microsoft/PowerToys/issues/48081), [#47667](https://github.com/microsoft/PowerToys/issues/47667) | Desktop, taskbar, Start, shell, and Command Palette reports motivated class-plus-process filtering. | `ResolveTargetWindow`; `IsSystemClass`; `IsExcluded` |

## Reviewer decision ledger

- **Thread ownership:** confine hook/cache state to the main message-pump thread. The settings thread
  posts invalidation; `g_excludedApps` crosses threads only as
  `atomic<shared_ptr<const vector<wstring>>>` ([#47052](https://github.com/microsoft/PowerToys/pull/47052),
  `ea37c3a`).
- **Event model:** use key transitions, not `KF_REPEAT`. A maintainer also confirmed
  `WINEVENT_OUTOFCONTEXT` callbacks dispatch on the installing thread's pump, so `WinEventProc` does
  not race the LL hooks; the proven race was settings-thread cache mutation.
- **Settings contract scope:** keep `Alt=0`, `Win=1` synchronized across C++ and C#. Review rejected
  converting the C# integer to an enum inside the two-value bugfix as unrelated churn (#47052).
- **Build dependency policy:** mirror canonical pinned CppWinRT imports; include `<atomic>` explicitly;
  prefer vcpkg plus a patch over a bundled shim header (#47910).
- **Comment style:** reviewers repeatedly rejected hyphenated/ornamental wording such as
  “warning-gold” and “literal equivalent”; comments should be plain and factual.

## Open symptom clusters

These remain investigation leads, not root-caused regressions:

- **Resize axis/edge selection:** [#48313](https://github.com/microsoft/PowerToys/issues/48313),
  [#47733](https://github.com/microsoft/PowerToys/issues/47733),
  [#47544](https://github.com/microsoft/PowerToys/issues/47544). Anchors:
  `GetClosestHandle`, `HandleDragResize`.
- **Preview DPI:** [#47771](https://github.com/microsoft/PowerToys/issues/47771). Anchor:
  `PrepareOverlayMetrics`.
- **Cross-utility/app conflicts:** Keyboard Manager
  [#49127](https://github.com/microsoft/PowerToys/issues/49127), FancyZones
  [#47774](https://github.com/microsoft/PowerToys/issues/47774), Command Palette
  [#47787](https://github.com/microsoft/PowerToys/issues/47787) and
  [#47832](https://github.com/microsoft/PowerToys/issues/47832). Anchors:
  modifier absorb/replay and `IsExcluded`.
- **Stops after sleep/hibernate:** [#47699](https://github.com/microsoft/PowerToys/issues/47699).
  Anchors: hook lifetime in `wWinMain`, `WinEventProc`.
- **WSLg, Task Manager, elevated targets:** [#48304](https://github.com/microsoft/PowerToys/issues/48304),
  [#47658](https://github.com/microsoft/PowerToys/issues/47658). Anchors: target resolution and
  integrity level.
