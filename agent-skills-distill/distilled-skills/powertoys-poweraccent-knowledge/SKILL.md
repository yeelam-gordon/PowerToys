---
name: powertoys-poweraccent-knowledge
description: 'Engineering knowledge for the PowerToys PowerAccent (Quick Accent) module — the low-level keyboard hook + WinUI 3 accent-picker popup. Use when planning, fixing, or reviewing changes to Quick Accent: keyboard-hook activation/timing, key-eating (leaked/doubled letters), Shift/modifier handling, multi-monitor/DPI overlay positioning, off-screen pre-render, native↔managed WinRT enum lockstep, module lifecycle/GPO, language/character data, and settings round-trip. Keywords: PowerAccent, Quick Accent, accent picker, LowLevelKeyboardProc, WH_KEYBOARD_LL, KeyboardListener, DPI popup positioning, GetActiveDisplay, _showGeneration, CharacterMappings, activation key, press and hold.'
license: Complete terms in LICENSE.txt
---

# PowerToys PowerAccent (Quick Accent) Knowledge

Quick Accent lets a user hold/trigger a base letter and pick an accented or special glyph
(`a` → `à á â ä …`) from a popup toolbar. Architecture: a C++/WinRT low-level keyboard hook
(`PowerAccentKeyboardService`) drives a WinUI 3 picker (`PowerAccent.UI`); orchestration,
positioning, and settings live in `PowerAccent.Core`; language data is a WinRT-free POCO library
(`PowerAccent.Common`); the module DLL (`PowerAccentModuleInterface`) handles lifecycle/GPO.

This skill distills the module's real regression history and review conventions so you can plan,
triage, and review changes grounded in what the maintainers already decided.

## When to Use This Skill

- Planning or implementing a change to Quick Accent (new activation mode, language, positioning fix)
- Triaging a Quick Accent bug: popup on wrong monitor, off-screen, mis-scaled, flickers, sticks open,
  leaks/doubles a letter, Shift "locked", or stops activating after update
- Reviewing a PR touching `src/modules/poweraccent/**`
- Changing the C++/WinRT keyboard hook, key-eating logic, or the native↔managed enum boundary
- Adding/reordering languages or characters in `CharacterMappings`

## Module Map (feature → file)

Projects: `PowerAccentModuleInterface` (C++ module DLL, lifecycle/GPO), `PowerAccentKeyboardService`
(C++/WinRT hook), `PowerAccent.Core` (orchestration/positioning/settings), `PowerAccent.Common`
(language POCO), `PowerAccent.UI` (WinUI 3 picker).

| Sub-feature | File / function |
|---|---|
| Module launch (spawns UI process) | `PowerAccentModuleInterface/dllmain.cpp::launch_process` → `CreateProcess("WinUI3Apps\\PowerToys.PowerAccent.exe", <runner PID>)` |
| Enable/disable + exit handshake | `dllmain.cpp::enable`/`disable`; `disable` signals `CommonSharedConstants::POWERACCENT_EXIT_EVENT` (falls back to `TerminateProcess`); UI waits in `Program.cs::InitExitListener` |
| GPO enforcement (DLL side) | `dllmain.cpp::gpo_policy_enabled_configuration` → `powertoys_gpo::getConfiguredQuickAccentEnabledValue`; UI double-checks in `Program.cs` |
| Settings serialize to/from runner | `dllmain.cpp::get_config`/`set_config`; telemetry `trace.cpp::Trace::EnablePowerAccent` |
| Low-level keyboard hook | `PowerAccentKeyboardService/KeyboardListener.cpp` — `LowLevelKeyboardProc` (WH_KEYBOARD_LL), `OnKeyDown`, `OnKeyUp` |
| Activation modes (LeftRightArrow/Space/Both/PressAndHold) | `KeyboardListener.h` enum `PowerAccentActivationKey`; defaults `inputTime` 300ms, `holdDuration` 500ms |
| Interop enums (managed ↔ native) | `KeyboardListener.idl` — `LetterKey`, `TriggerKey`, `InputType`; delegates `ShowToolbar`/`HideToolbar`/`NextChar`/`IsLanguageLetter` |
| Modifier guard (Ctrl/Alt/AltGr/Win) | `KeyboardListener.cpp::IsBlockingModifierDown` |
| Game-mode / excluded-app suppression | `KeyboardListener.cpp` — `IsSuppressedByGameMode`, `IsForegroundAppExcluded`, `UpdateExcludedApps` |
| Shift tracking for uppercase/back-nav | `KeyboardListener.cpp` `m_leftShiftPressed`/`m_rightShiftPressed` (only after toolbar visible) + `PowerAccent.cs::ProcessNextChar` (`_initialShiftState`) |
| Toolbar orchestration, stale-timer guard, navigation | `PowerAccent.Core/PowerAccent.cs` — `ShowToolbar` (`_showGeneration`), `ProcessNextChar`, `SendInputAndHideToolbar`, `PrepareCharacters` |
| Character build, upper-casing, usage sort | `PowerAccent.cs` — `GetCharacters`, `ToUpper` (ß→ẞ etc.), `GetCharacterDescriptions` |
| Overlay position math (DPI-scaled) | `PowerAccent.Core/Tools/Calculation.cs::GetRawCoordinatesFromPosition(position, screen, window, dpi)` |
| Active monitor + DPI detection | `PowerAccent.Core/Tools/WindowsFunctions.cs::GetActiveDisplay` (GetGUIThreadInfo → MonitorFromWindow → GetDpiForMonitor); `IsShiftState`/`IsCapsLockState`; `Insert` (SendInput UNICODE); `SendArrowKey` |
| Settings load / file-watch / ALL parsing / position map | `PowerAccent.Core/Services/SettingsService.cs::ReadSettings`; `Position` enum |
| Language data (single source of truth) | `PowerAccent.Common/CharacterMappings.cs` (`All`, `DisplayOrder`, `GroupDisplayOrder`); `Language.cs`, `LanguageGroup.cs`, `LanguageInfo.cs`, `LetterKey.cs` |
| Usage-frequency persistence | `PowerAccent.Core/Tools/CharactersUsageInfo.cs` (`UsageInfo.json`) |
| Picker UI (WinUI 3) | `PowerAccent.UI/PowerAccentXAML/MainWindow.xaml(.cs)` (`GetDisplayCoordinates`/`GetDisplayMaxWidth`, `SaveUsageInfo`), `SelectorControl.xaml(.cs)` (`ScrollIntoView`), `SelectorViewModel.cs` |
| Process lifecycle (single-instance, GPO, exit) | `PowerAccent.UI/Program.cs` — `QuickAccent` mutex, GPO check, `InitExitListener`/`Terminate` (hard-exit fallback so the hook is never orphaned) |

> **Historical note (older PRs cite pre-rename paths):** language data lived in
> `PowerAccent.Core/Languages.cs` before [#47211](https://github.com/microsoft/PowerToys/pull/47211)
> (now `PowerAccent.Common/CharacterMappings.cs`); the UI was WPF (`Selector.xaml`) before the wpfui
> removal ([#46604](https://github.com/microsoft/PowerToys/pull/46604)) and WinUI 3 migration.

## Regression Playbooks (rule-by-rule)

Multi-monitor+DPI and hook-timing are the two dominant recurring failure families. Each playbook:
Symptom → Where → Root cause → Guardrail.

### 1. Keyboard-hook activation & timing (menu flashes early / lags on fast typing)
- **Symptom:** popup flashes for a key you already released, or lags/opens for a stale summon on fast typing (#42821, #39852, #39564, #41761).
- **Where:** `PowerAccent.cs::ShowToolbar` deferred render path; `_showGeneration` counter.
- **Root cause:** a delayed `Task.Delay().ContinueWith()` render from an earlier press fired for a newer/hidden summon.
- **Guardrail:** every deferred render must re-check `generation == _showGeneration && _visible` before showing. Fixed in [#48944](https://github.com/microsoft/PowerToys/pull/48944). See [WH_KEYBOARD_LL](https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc).

### 2. Key-eating contract (leaked / doubled characters)
- **Symptom:** base letter leaks through, is deleted, or is doubled — app-specific (#40373 deletes typed char, #40541 Figma double letter, #46963 `OnKeyUp` leak, #48841 duplicate in browser address bar).
- **Where:** `KeyboardListener.cpp::OnKeyDown`/`OnKeyUp` return values.
- **Root cause:** asymmetry in returning `true` (swallow) vs `false` (pass) between key-down and key-up across activation modes and target apps; PressAndHold must let the base letter through on key-down.
- **Guardrail:** any change to `OnKeyDown`/`OnKeyUp` return values is high-risk. Verify both paths for **every** activation mode (LeftRightArrow/Space/Both/**PressAndHold**). Returning `true` swallows the key — [WH_KEYBOARD_LL contract](https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc).

### 3. Shift / modifier handling ("navigate back" vs uppercase; Shift stuck)
- **Symptom:** triggering Quick Accent while Shift-typing a capital jumps Space-navigation backwards (#46593); Shift stays "locked" after navigation (#45936).
- **Where:** `PowerAccent.cs::ProcessNextChar` (`_initialShiftState`); `KeyboardListener.cpp` shift tracking.
- **Root cause:** `shiftPressed || IsShiftState()` treated *any* held Shift as backwards-nav; and shift state was left set after navigation.
- **Guardrail:** Shift is only tracked once the toolbar is visible; capture `_initialShiftState` at show time and only treat Shift as "navigate back" if it transitioned *after* show. Distinguish "Shift for uppercase" from "Shift for nav" ([#46593](https://github.com/microsoft/PowerToys/pull/46593)). Also guard Ctrl/Alt/AltGr/Win via `IsBlockingModifierDown` so shortcuts still work.

### 4. Multi-monitor / DPI overlay positioning (wrong monitor, mis-scaled, too wide)
- **Symptom:** popup opens on wrong monitor, mis-scaled, or wider than screen (#40865, #40498, #44980, #47347, #43247, #43031, #48123).
- **Where:** `WindowsFunctions.GetActiveDisplay`; `Calculation.GetRawCoordinatesFromPosition(..., dpi)`; `MainWindow.xaml.cs::GetDisplayMaxWidth`.
- **Root cause:** screen bounds/DPI not taken from the *active* monitor, or DIP-vs-physical unit mix-ups crossing the DIP↔physical boundary.
- **Guardrail:** always route through `GetActiveDisplay` (returns active monitor work area **and** effective DPI) and pass the explicit `dpi` factor through every conversion; `GetDisplayMaxWidth` divides by DPI. Fixed in [#43314](https://github.com/microsoft/PowerToys/pull/43314), [#46593](https://github.com/microsoft/PowerToys/pull/46593). See [Per-Monitor DPI awareness](https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows).

### 5. Off-screen pre-render (flicker on large multi-monitor layouts)
- **Symptom:** popup flickers at first paint on large/multi-monitor setups.
- **Where:** pre-render off-screen spot in the UI positioning path.
- **Root cause:** a hard-coded `(-10000,-10000)` pre-render position can still land on-screen on large virtual desktops.
- **Guardrail:** compute the off-screen spot from virtual-screen metrics (`SM_XVIRTUALSCREEN`/`SM_CXVIRTUALSCREEN`/`SM_YVIRTUALSCREEN`/`SM_CYVIRTUALSCREEN`), not a magic constant ([#46593](https://github.com/microsoft/PowerToys/pull/46593)).

### 6. Native ↔ managed enum lockstep (stops activating / lost characters after update)
- **Symptom:** stops activating or loses characters after an update; arrow activation breaks (#37922, #39436, #39470, #45480, #45456).
- **Where:** `KeyboardListener.idl` ↔ `LetterKey.cs`/`Position`/`PowerAccentActivationKey`; `KeyboardListener.h` defaults.
- **Root cause:** `LetterKey`, `TriggerKey`, `InputType`, `PowerAccentActivationKey` are mirrored across the C++/WinRT boundary; drift or default mismatch silently breaks activation.
- **Guardrail:** keep enums in lock-step across the `.idl`/`.h`/`.cs`; the `.h` comments explicitly require defaults to match `UI.Library.PowerAccentSettings` (e.g. `holdDuration{500}` // Should match DefaultHoldDurationMs). Shared `CharacterMappings` source of truth (after [#47211](https://github.com/microsoft/PowerToys/pull/47211)) reduces this risk.

### 7. Module lifecycle (orphaned hook, first-run ALL, usage not saved)
- **Symptom:** hook orphaned after disable; first-run "All available" selects only SPECIAL (#47113); usage frequency not remembered after restart (#45630, #44355).
- **Where:** `dllmain.cpp::disable` + `Program.cs::InitExitListener`/`Terminate`; `SettingsService.ReadSettings`; `CharactersUsageInfo`/`SaveUsageInfo`.
- **Root cause:** exit signal (`POWERACCENT_EXIT_EVENT`) races startup so `Terminate` hard-exits before saving; persisted `"ALL"` sentinel wasn't expanded by the app.
- **Guardrail:** `disable` must signal the exit event (fallback `TerminateProcess`) so the hook is never orphaned; `ReadSettings` expands `ALL` to `Enum.GetValues<Language>()` and skips unknown language tokens ([#47117](https://github.com/microsoft/PowerToys/pull/47117)). Be aware `Terminate`'s hard-exit fallback can skip `SaveUsageInfo`.

For the fuller catalog (toolbar-stuck, IME/OSK conflicts, excluded-app cache), see
[references/regression-catalog.md](./references/regression-catalog.md).

## Review Rules

Apply these imperative rules when reviewing a Quick Accent PR (generic rule + public ref + this-app hook):

- **Guard the key-eating contract** ([WH_KEYBOARD_LL](https://learn.microsoft.com/windows/win32/winmsg/lowlevelkeyboardproc)):
  in `OnKeyDown`/`OnKeyUp`, verify the swallow/pass (`return true`/`false`) is symmetric across all activation modes and lets the base letter through in PressAndHold — the direct cause of leaked/doubled-letter regressions.
- **Route all positioning through the active monitor + explicit DPI** ([Per-Monitor DPI](https://learn.microsoft.com/windows/win32/hidpi/high-dpi-desktop-application-development-on-windows)):
  never read primary-screen bounds or hard-code sizes; use `GetActiveDisplay` and thread the `dpi` factor through `Calculation`/`GetDisplayMaxWidth`.
- **Re-check the generation on any deferred render** (`PowerAccent.cs`): a new `Task.Delay`/async render must confirm `generation == _showGeneration && _visible` before showing, or it flashes stale popups.
- **Keep native/managed enums in lock-step** (`KeyboardListener.idl` ↔ `.cs`): adding/reordering `LetterKey`/`TriggerKey`/`InputType`/activation values requires updating both sides and matching defaults to `PowerAccentSettings`.
- **Use ISO 639-3 language codes** ([ISO 639-3](https://iso639-3.sil.org/)): e.g. `PJT` not `PJ`, `GRC` for Greek polytonic — applies at `Language.cs`/`All` ([#48561](https://github.com/microsoft/PowerToys/pull/48561), [#47021](https://github.com/microsoft/PowerToys/pull/47021)).
- **Pick the correct Unicode codepoint even when glyphs look identical** ([Unicode charts](https://www.unicode.org/charts/)): e.g. Greek question mark Erotimatiko U+037E ≠ ASCII semicolon, so the description label reads correctly ([#47021](https://github.com/microsoft/PowerToys/pull/47021)).
- **All end-user strings live in `Resources.resw`** and long language labels must be checked for UI overflow ([#48561](https://github.com/microsoft/PowerToys/pull/48561)).
- **Keep repo-wide dependency bumps out of a feature PR** — a `CsWin32`/`CppWinRT` bump in `Directory.Packages.props` has repo-wide blast radius ([#46593](https://github.com/microsoft/PowerToys/pull/46593)).

## Gotchas

- **Never** change an `OnKeyDown`/`OnKeyUp` return value without checking **all** activation modes including PressAndHold — asymmetric swallow/pass leaks or doubles the base letter, and it's app-specific (Figma, browser address bars regressed).
- **Never** read screen bounds from the primary monitor or hard-code pixel sizes — Quick Accent must position on the *active* monitor with per-monitor DPI; DIP↔physical mix-ups are the single most recurring bug family.
- **Never** hard-code an off-screen pre-render coordinate like `(-10000,-10000)` — compute from `SM_*VIRTUALSCREEN`; the constant can be on-screen on large layouts.
- **Shift is only tracked once the toolbar is visible.** Treating any held Shift as "navigate back" breaks Shift-typing a capital while triggering. Use `_initialShiftState`.
- **`PowerAccent.Common` deliberately does NOT import `Common.Dotnet.CsWinRT.props`** — it's a WinRT-free POCO; it's in `verifyCommonProps.ps1`'s exclusion list. Do not "fix" it by adding the shared props ([#47211](https://github.com/microsoft/PowerToys/pull/47211)).
- **`SettingsUtils.Default` is a shared, not-thread-safe singleton** — safe only because access is single-threaded ([#44064](https://github.com/microsoft/PowerToys/pull/44064)). Don't introduce concurrent access.
- **`CharacterMappings.All` exposes mutable inner `string[]`** despite `IReadOnlyList`/`IReadOnlyDictionary` wrappers — treat the arrays as read-only in new consumers ([#47211](https://github.com/microsoft/PowerToys/pull/47211)).
- **`DisplayOrder` must stay alphabetical by display name** — inserting a language out of order fails `CharacterMappingsTests.DisplayOrder_SpokenLanguages_AreSortedAlphabeticallyByDisplayName`. Place it in the correct slot, don't append ([#48561](https://github.com/microsoft/PowerToys/pull/48561)).
- **Upper-casing needs explicit special-cases** in `PowerAccent.cs::ToUpper` (ß→ẞ, ǰ→J̌, ı→İ, superscripts…) — glyphs that don't round-trip via `ToUpper(InvariantCulture)` must be hard-coded.
- **All hook callbacks marshal to the UI thread** (`SetEvents` via `_runOnUiThread`), which is why `_showGeneration` needs no locking. Keep new hook→UI work on that path.

## Common Practices

- **Adding a language is a 4-part checklist** (documented in `CharacterMappings.cs` header): add a `Language` enum value, a `LanguageInfo` entry in `All`, a slot in `DisplayOrder` (correct alphabetical position — unit-tested), and a resx string in `Settings.UI/Strings/en-us/Resources.resw`. A missing resx key renders an empty group header ([#47211](https://github.com/microsoft/PowerToys/pull/47211)).
- **Settings round-trip through JSON + live file-watch:** `SettingsService` reads via `SettingsUtils`, watches `settings.json`, tolerates unknown language tokens, and maps display strings ("Top center"…) to the `Position` enum. New settings must be pushed to the native hook via `Update*` methods (`UpdateActivationKey`, `UpdateExcludedApps`, `UpdateHoldDuration`, …).
- **Press-and-hold mode is purely additive** ([#48937](https://github.com/microsoft/PowerToys/pull/48937)): base letter types immediately on key-down, picker arms after `holdDuration`, release inserts. Don't change existing trigger modes or serialized settings.

## Using This Skill in PR Review (Anti-Anchoring)

**Benchmark-derived warning.** Do **not** read these playbooks first and then hunt the diff for
their themes — that anchors you on recurring concerns and measurably lowers your catch rate on the
PR's actual concrete issues.

1. **Read the diff cold first.** Form your own list of concerns from what actually changed.
2. **Then** cross-check only the Regression Playbooks / Review Rules for the code paths the diff
   actually touches (targeted retrieval, not the whole file).
3. Treat the Module Map as **hypotheses to confirm in source**, not ground truth. If the symptom
   doesn't map cleanly to a listed area, reason from the symptom and verify in source — don't
   force-fit the map. This file is most valuable for planning, onboarding, and issue-fixing.

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller symptom→cause→fix catalog
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — checklist grouped by touched area
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function
- Source: `src/modules/poweraccent/` in [microsoft/PowerToys](https://github.com/microsoft/PowerToys)
