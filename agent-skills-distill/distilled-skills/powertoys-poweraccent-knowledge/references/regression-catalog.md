# Quick Accent Regression Catalog (fuller list)

Progressive-disclosure companion to SKILL.md's Regression Playbooks. Read on demand when a symptom
doesn't map cleanly to the seven core playbooks. Format: **Symptom → Root cause → Fix / Guardrail**.
Issue/PR numbers are from [microsoft/PowerToys](https://github.com/microsoft/PowerToys).

## Multi-monitor / DPI positioning (dominant family)
- **Popup on wrong monitor, mis-scaled, or wider than the screen** (#40865, #40498, #44980, #47347,
  #43247, #43031 "wider than screen", #48123 WinForms) → screen bounds/DPI not taken from the
  *active* monitor, or DIP-vs-physical mix-ups.
  **Guardrail:** route through `WindowsFunctions.GetActiveDisplay` + `Calculation.GetRawCoordinatesFromPosition(..., dpi)`; `GetDisplayMaxWidth` divides by DPI. Fixed by DPI rework in #43314 and #46593.
- **Flicker at first paint on large layouts** → hard-coded `(-10000,-10000)` pre-render can be
  on-screen on large virtual desktops.
  **Guardrail:** compute from `SM_XVIRTUALSCREEN`/`SM_CXVIRTUALSCREEN`/`SM_YVIRTUALSCREEN`/`SM_CYVIRTUALSCREEN` (#46593).

## Hook timing / stale render (dominant family)
- **Menu flashes early / lags on fast typing** (#42821, #39852, #39564, #41761 space unresponsive)
  → a delayed `Task.Delay().ContinueWith()` render from an earlier press fired for a newer/hidden summon.
  **Guardrail:** `_showGeneration` counter — render only if `generation == _showGeneration && _visible` (#48944). Any new deferred render must re-check the generation.

## Key-eating asymmetry
- **Letter leaks / doubled characters** (#40373 deletes typed char, #40541 Figma double letter,
  #46963 `OnKeyUp` normal path returns false leaking the letter, #48841 duplicate in browser address bar)
  → key-eating (`return true/false`) asymmetry between key-down and key-up across activation modes and target apps.
  **Guardrail:** any change to `OnKeyDown`/`OnKeyUp` return values is high-risk; verify both paths for every activation mode and for PressAndHold (which must let the base letter through).

## Toolbar lifecycle
- **Toolbar stuck / won't close** (#37668, #38915 Sibelius, #44482, #43200, #37488) → hide path not
  reached for certain focus/app states.
  **Guardrail:** watch `OnKeyUp` early-returns and `SendInputAndHideToolbar`.

## Shift / modifier
- **Shift-typing a capital jumps navigation backwards** (#46593) → `shiftPressed || IsShiftState()`
  treated any held Shift as backwards-navigation.
  **Guardrail:** capture `_initialShiftState` at show time in `PowerAccent.cs::ProcessNextChar`; distinguish "Shift for uppercase" from "Shift for nav."
- **Shift stays "locked"** (#45936) → shift state left set after navigation; tied to the
  hook-tracks-Shift-only-when-visible rule (#46593).

## Settings / first-run
- **First-run "All available" selects only SPECIAL** (#47113) → persisted `"ALL"` sentinel
  not understood by the app, falling back to SPECIAL.
  **Guardrail:** `SettingsService.ReadSettings` expands `ALL` to `Enum.GetValues<Language>()` and skips unknown language tokens (fixed #47117).

## Persistence / lifecycle
- **Usage frequency not remembered after restart** (#45630, #44355 flagged as keyboard activity) →
  `UsageInfo.json` persistence / `SaveUsageInfo` on exit path.
  **Note:** `Program.Terminate` may hard-exit before saving if the exit signal races startup.

## Activation-mode / character-set drift
- **Stopped activating after update / lost characters** (#37922, #39436, #39470, #45480, #45456 arrow
  activation) → activation-mode/character-set changes across releases.
  **Guardrail:** shared `CharacterMappings` source of truth after #47211; keep native/managed enums in lock-step.

## Input-method conflicts
- **IME / on-screen-keyboard conflicts** (#41151 Pinyin; code cites #36853) → OSK sends continuous
  WM_KEYDOWN; `OnKeyDown` swallows the repeat while the toolbar is visible.

## Exclusions / game mode
- **Activates in excluded app / game mode** (#47804) → `IsForegroundAppExcluded` caches the last
  foreground HWND result.
  **Guardrail:** cache reset on `UpdateExcludedApps` and empty-list.

## Data / localization
- **Empty language group header** (#47211) → missing resx string for a language.
  **Guardrail:** the 4-part add-a-language checklist (enum + `All` + `DisplayOrder` slot + resx).
- **Language sort test failure** (#48561) → language inserted out of alphabetical order.
  **Guardrail:** `CharacterMappingsTests.DisplayOrder_SpokenLanguages_AreSortedAlphabeticallyByDisplayName`.
- **Wrong glyph description / wrong code** (#47021, #48561) → wrong Unicode codepoint or non-ISO-639-3
  identifier.
  **Guardrail:** use ISO 639-3 codes and exact Unicode codepoints (e.g. Erotimatiko U+037E).
