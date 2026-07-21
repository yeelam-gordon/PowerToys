# GrabAndMove Regression Catalog (Progressive Disclosure)

Fuller regression + decision list for the PowerToys **GrabAndMove** module. Read the row for the area
your change touches; confirm each claim in source before acting. Symptoms map to
`src/modules/GrabAndMove/`. Issues/PRs are on `microsoft/PowerToys`.

> **Honesty note:** GrabAndMove is a **new** module (first landed
> [PR #47024](https://github.com/microsoft/PowerToys/pull/47024), Apr 2026). Its history is short —
> ~11 PRs and a burst of open bug reports — and it has **no dedicated unit-test suite**. Most durable
> lessons come from PR review comments and the source itself, not from long-lived regression cycles.
> Treat the open-issue rows as *symptom clusters to verify*, not confirmed root-caused regressions.

## Key Decisions (context for the playbooks)

- **Separate process driven by global hooks.** The module interface (`dllmain.cpp`) launches
  `PowerToys.GrabAndMove.exe`; `main.cpp` installs `WH_KEYBOARD_LL` + `WH_MOUSE_LL` + a
  `WINEVENT_OUTOFCONTEXT` foreground hook. Nearly all behavior — and nearly all bugs — live in those
  hook callbacks and their shared globals.
- **Modifier is Alt or Win, selectable.** `enum class GrabAndMoveModifier { Alt = 0, Win = 1 }`;
  `LoadSettingsFromFile` maps int `modifierKey`. Move = modifier + left-drag, resize = modifier +
  right-drag (`useAltResize` gates resize). Added in
  [PR #47052](https://github.com/microsoft/PowerToys/pull/47052) (Win modifier + coord improvements).
- **Absorb-and-replay the modifier.** With `shouldAbsorbAlt` (and always for Win), the modifier
  keydown is swallowed to avoid a Start-menu/app-menu flash, then **replayed** if no drag/resize
  consumed it. This is the single most fragile area — see the stuck-key and absorb playbooks.
- **Foreground change is a reset point.** `WinEventProc` clears the held-key counter + `g_keyHeld` and
  invalidates `g_excludedCache` on every foreground switch, because other apps/OS can swallow a keyup
  (e.g. Win+L). A maintainer confirmed `WINEVENT_OUTOFCONTEXT` dispatches on the installing thread's
  pump, so it does not race the LL hooks.
- **Excluded-apps as an immutable snapshot.** `g_excludedApps` is an
  `atomic<shared_ptr<const vector<wstring>>>`; the per-HWND `g_excludedCache` is confined to the main
  thread and invalidated via `PostMessage(WM_INVALIDATE_EXCLUDED_CACHE)`. This was the fix for the
  settings-thread vs main-thread data race.
- **Remote Desktop is special-cased.** `SM_REMOTESESSION` forces square overlay corners
  ([PR #48999](https://github.com/microsoft/PowerToys/pull/48999)), prefers foreground-based target
  resolution, and disables Game Mode suppression.
- **Overlay is a persistent GDI+ window.** Created once (`EnsureOverlayWindow`), shown/repositioned
  per drag; border + rounded corners drawn with GDI+; warning-gold fill/border introduced in
  [PR #48474](https://github.com/microsoft/PowerToys/pull/48474).

## Regression / Symptom Table

| Area | Symptom | Where (file · function) | Root cause / guardrail | Evidence |
|---|---|---|---|---|
| Concurrency | Rare crash; stale excluded-apps | `IsExcluded` `g_excludedCache`; `LoadSettingsFromFile`; `WinEventProc` | `unordered_map` race across settings/main thread → confine to main thread, `PostMessage` invalidation, atomic snapshot list | review on [#47052](https://github.com/microsoft/PowerToys/pull/47052), commit `ea37c3a` |
| Stuck keys | Modifier stops working; plain key unresponsive | `KeyboardProc` `g_keyHeld`/`g_heldNonAltKeyCount`; `WinEventProc` | `KF_REPEAT` misread off LL hook struct + swallowed keyup left counter >0 → transition tracking + foreground reset | [#47052](https://github.com/microsoft/PowerToys/pull/47052); reports [#48190](https://github.com/microsoft/PowerToys/issues/48190), [#47802](https://github.com/microsoft/PowerToys/issues/47802), [#49037](https://github.com/microsoft/PowerToys/issues/49037), [#48215](https://github.com/microsoft/PowerToys/issues/48215) |
| Stuck modifier (absorbed Alt) | Alt held, another key pressed → Alt behaves as still held | `KeyboardProc` else-branch: `if (g_altAbsorbed && !g_dragConsumedAlt)` non-Alt-key path | Branch cleared `g_altAbsorbed` and replayed Alt but left paired `g_altPressed == true` → internal state thinks Alt still down. Fix adds `g_altPressed = false;` before `ReplayAbsorbedModifier(false)` | [#47261](https://github.com/microsoft/PowerToys/pull/47261) (closes [#47257](https://github.com/microsoft/PowerToys/issues/47257)) |
| Modifier absorb | Alt/Win normal use breaks in other apps | `KeyboardProc`, `ReplayAbsorbedModifier` | Swallowed keydown not replayed → replay down (+up for Win) when no drag consumed it | [#47326](https://github.com/microsoft/PowerToys/pull/47326); reports [#47585](https://github.com/microsoft/PowerToys/issues/47585), [#47787](https://github.com/microsoft/PowerToys/issues/47787), [#47774](https://github.com/microsoft/PowerToys/issues/47774), [#48121](https://github.com/microsoft/PowerToys/issues/48121), [#47715](https://github.com/microsoft/PowerToys/issues/47715) |
| Target filter | Desktop/taskbar/shell/palette dragged | `ResolveTargetWindow`, `IsSystemClass`, `IsExcluded` | Shared classes (`CoreWindow`) → filter by class + process path; normalize `GA_ROOT` | [#47302](https://github.com/microsoft/PowerToys/pull/47302); reports [#47926](https://github.com/microsoft/PowerToys/issues/47926), [#48056](https://github.com/microsoft/PowerToys/issues/48056), [#47832](https://github.com/microsoft/PowerToys/issues/47832), [#48081](https://github.com/microsoft/PowerToys/issues/48081), [#47667](https://github.com/microsoft/PowerToys/issues/47667) |
| Maximized | Window jumps from cursor on grab | `HandleDragMove`/`HandleDragResize` restore branch | Re-anchor proportionally to current `pt` after `SW_RESTORE`; keep move/resize consistent | [#49118](https://github.com/microsoft/PowerToys/pull/49118); report [#49123](https://github.com/microsoft/PowerToys/issues/49123) |
| Remote/overlay | Rounded corners over RDP; wrong target | `CornerRadiusForWindow`, `ResolveTargetWindow`, `IsSuppressedByGameMode` | `SM_REMOTESESSION` → square corners, foreground target, no game suppression | [#48999](https://github.com/microsoft/PowerToys/pull/48999) |
| Two-button | Unmatched button-up reaches target | `MouseProc` pending-press logic | Guard against overwriting an existing pending drag/resize when both buttons used | review on [#49121](https://github.com/microsoft/PowerToys/pull/49121) |
| Build | `LNK2038 'C++/WinRT version'` breaks CI | `GrabAndMove.vcxproj` | Missing CppWinRT NuGet import → mirror canonical pinned wiring | [#47910](https://github.com/microsoft/PowerToys/pull/47910) |
| Build hygiene | Fragile transitive include | `pch.h` | `std::atomic` used but `<atomic>` not included → include STL headers explicitly | review on [#47052](https://github.com/microsoft/PowerToys/pull/47052) |
| OOBE/assets | Missing OOBE + icons | `src/settings-ui/Settings.UI/Assets/Settings/Icons/GrabAndMove.png`; `src/settings-ui/Settings.UI/Assets/Settings/Modules/GrabAndMove.png`; `src/settings-ui/Settings.UI/Assets/Settings/Modules/OOBE/GrabAndMove.gif` | Wire OOBE + module assets for the new module | [#47033](https://github.com/microsoft/PowerToys/pull/47033) |

## Open symptom clusters (verify before acting — not yet root-caused here)

These are recurring **user reports** at the time of distillation; they point at areas above but have no
confirmed fix. Reason from the symptom and confirm in source.

- **Resize axis bias / undesirable edges:** [#48313](https://github.com/microsoft/PowerToys/issues/48313),
  [#47733](https://github.com/microsoft/PowerToys/issues/47733),
  [#47544](https://github.com/microsoft/PowerToys/issues/47544) → `GetClosestHandle`/`HandleDragResize`.
- **DPI awareness of the preview:** [#47771](https://github.com/microsoft/PowerToys/issues/47771) →
  `PrepareOverlayMetrics` DPI scaling.
- **Conflicts with other PowerToys/apps:** Keyboard Manager
  [#49127](https://github.com/microsoft/PowerToys/issues/49127), FancyZones
  [#47774](https://github.com/microsoft/PowerToys/issues/47774), Command Palette
  [#47787](https://github.com/microsoft/PowerToys/issues/47787),
  [#47832](https://github.com/microsoft/PowerToys/issues/47832) → modifier absorb/replay + `IsExcluded`.
- **Stops after sleep/hibernation:** [#47699](https://github.com/microsoft/PowerToys/issues/47699) →
  hook lifetime in `wWinMain`/`WinEventProc`.
- **WSLg / Task Manager / elevated targets not honored:**
  [#48304](https://github.com/microsoft/PowerToys/issues/48304),
  [#47658](https://github.com/microsoft/PowerToys/issues/47658) → target resolution / integrity level.

## Maintainer conventions surfaced in review

- Confine shared hook state to one thread; marshal cross-thread work with `PostMessage`.
- Track held keys by transition, never `KF_REPEAT`.
- Include STL headers explicitly in `pch.h`.
- Keep the C++/C# modifier mapping (`Alt=0`,`Win=1`) as a simple int for a two-value bugfix; don't
  churn it into an enum in an unrelated PR (scope-creep pushback).
- New native `.vcxproj` mirrors the canonical CppWinRT NuGet wiring.
- Prefer vcpkg + patch file over vendored shim headers for toolset compat.
- Keep code comments plain — a maintainer repeatedly rejected hyphenated compound nouns and flowery
  phrasing.
