# GrabAndMove Bug Triage (template — fill per report)

Map the reported symptom to the likely file/function, then **confirm in source** before editing.
If the symptom doesn't map cleanly, reason from the symptom — don't force-fit the table. This module
is new; expect gaps. All paths under `src/modules/GrabAndMove/`.

## Report
- **Symptom:**
- **Repro / inputs:**
- **Modifier: Alt or Win? "absorb Alt" on?:**
- **OS / build; Remote Desktop?; Game Mode?:**
- **Target app / window class:**

## Symptom → likely location

| Reported symptom | Start here (file · function) | Likely class | Playbook |
|---|---|---|---|
| Alt/Win stops triggering GrabAndMove after a while | `main.cpp::KeyboardProc` (`g_heldNonAltKeyCount`/`g_keyHeld`); `WinEventProc` reset | Stuck-key counter | Stuck modifier |
| Plain key (e.g. `G`) unresponsive until re-pressed | `main.cpp::KeyboardProc` held-key tracking | Swallowed keyup | Stuck modifier |
| Alt no longer opens app menu / Win no longer opens Start | `main.cpp::KeyboardProc`, `ReplayAbsorbedModifier` | Missing replay | Absorbed modifier |
| A Win/Alt shortcut in another app breaks | `main.cpp::KeyboardProc` absorb/replay; `IsExcluded` | Modifier absorb | Absorbed modifier |
| Desktop icons / wallpaper get dragged | `main.cpp::IsSystemClass` (Progman); `ResolveTargetWindow` | Target filter | Wrong target |
| Start / Search / Quick Settings / Widgets get moved | `main.cpp::IsExcluded` (CoreWindow by process) | Shell surface filter | Wrong target |
| Command Palette / a specific app is draggable | `main.cpp::IsExcluded`; user `excluded_apps` | Exclusion gap | Wrong target |
| Maximized window jumps away from cursor on grab | `main.cpp::HandleDragMove`/`HandleDragResize` restore branch | Anchor after `SW_RESTORE` | Maximized anchoring |
| Resize prefers one axis / resizes wrong edge | `main.cpp::GetClosestHandle`, `HandleDragResize` | Handle selection | (Module Map) |
| Overlay corners rounded wrongly over RDP | `main.cpp::CornerRadiusForWindow`, `PrepareOverlayMetrics` | Remote-session corners | Remote overlay |
| Wrong window grabbed in a remote session | `main.cpp::ResolveTargetWindow` (`SM_REMOTESESSION`) | Remote hit-testing | Remote overlay |
| Overlay preview blurry / wrong DPI | `main.cpp::PrepareOverlayMetrics` (DPI scale), `RenderOverlayContent` | DPI scaling | (Module Map) |
| Not working after wake from hibernation | `main.cpp::wWinMain` hook install; `WinEventProc` | Hook lifetime | (Pitfalls) |
| Doesn't work in a game / fullscreen app | `main.cpp::IsSuppressedByGameMode` | Game Mode gate | Remote overlay |
| Resize does nothing on some windows | `MouseProc` `WS_THICKFRAME` gate | Non-resizable window | Review Rules |
| Build fails `LNK2038 'C++/WinRT version'` | `GrabAndMove.vcxproj` CppWinRT NuGet import | Toolset mismatch | CppWinRT LNK2038 |
| Settings change not applied live | `dllmain.cpp::set_config` event; `SettingsWatcherThread`, `LoadSettingsFromFile` | Hot-reload | Data race |

## Confirmation steps
1. Open the candidate file/function; verify the code path matches the symptom.
2. Check the linked issues/PRs in [regression-catalog.md](../references/regression-catalog.md) for prior fixes/guardrails.
3. Reproduce with the reporter's inputs (note modifier, absorb setting, remote/game mode, target class).
4. For a hook change, test that the modifier still behaves normally in unrelated apps (replay path).
