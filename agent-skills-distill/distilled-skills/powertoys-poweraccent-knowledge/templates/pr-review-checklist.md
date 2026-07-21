# Quick Accent PR Review Checklist

Apply **after** reading the diff cold (see anti-anchoring in SKILL.md). Only work the groups whose
files the PR actually touches. Each item cites where to verify in source.

## Keyboard hook / activation (`PowerAccentKeyboardService/KeyboardListener.cpp`, `.h`, `.idl`)
- [ ] `OnKeyDown`/`OnKeyUp` return values (swallow `true` / pass `false`) are symmetric and verified for **every** activation mode: LeftRightArrow, Space, Both, **PressAndHold**.
- [ ] PressAndHold still lets the base letter through on key-down (regression source: leaked/doubled letters #40373/#40541/#46963/#48841).
- [ ] Ctrl/Alt/AltGr/Win combos still bypass via `IsBlockingModifierDown` (shortcuts unaffected).
- [ ] Game-mode / excluded-app suppression respected (`IsSuppressedByGameMode`, `IsForegroundAppExcluded`, cache reset on `UpdateExcludedApps`).
- [ ] Any new enum value added to `.idl` is mirrored in `.h` **and** the managed `.cs`, with defaults matching `PowerAccentSettings` (e.g. `holdDuration{500}`).

## Toolbar orchestration / timing (`PowerAccent.Core/PowerAccent.cs`)
- [ ] Any deferred/async render re-checks `generation == _showGeneration && _visible` before showing (stale-flash guard, #48944).
- [ ] Shift-vs-navigation uses `_initialShiftState` captured at show time; Shift only tracked once toolbar visible (#46593/#45936).
- [ ] Hide path (`SendInputAndHideToolbar`) reachable for the affected focus/app state (toolbar-stuck family).
- [ ] New hook→UI work runs on `_runOnUiThread` (dispatcher); no new locking introduced around `_showGeneration`.
- [ ] `ToUpper` special-cases added for any new glyph whose uppercase doesn't round-trip via `InvariantCulture`.

## Positioning / DPI (`Tools/Calculation.cs`, `Tools/WindowsFunctions.cs`, `PowerAccentXAML/MainWindow.xaml.cs`)
- [ ] Bounds/DPI come from `GetActiveDisplay` (active monitor), not primary screen.
- [ ] Explicit `dpi` factor threaded through every DIP↔physical conversion; `GetDisplayMaxWidth` divides by DPI.
- [ ] Off-screen pre-render computed from `SM_*VIRTUALSCREEN`, not a hard-coded `(-10000,-10000)`.

## Language / character data (`PowerAccent.Common/*`)
- [ ] New language: `Language` enum + `LanguageInfo` in `All` + correct alphabetical `DisplayOrder` slot + resx string in `Settings.UI/Strings/en-us/Resources.resw`.
- [ ] Language identifier is a valid ISO 639-3 code.
- [ ] Correct Unicode codepoints (not visually-identical ASCII); description label reads correctly.
- [ ] `PowerAccent.Common` still does NOT import `Common.Dotnet.CsWinRT.props` (stays in `verifyCommonProps.ps1` exclusion).
- [ ] No new consumer mutates the inner `string[]` of `CharacterMappings.All`.
- [ ] Unit tests still pass, esp. `CharacterMappingsTests.DisplayOrder_SpokenLanguages_AreSortedAlphabeticallyByDisplayName`.

## Settings / lifecycle (`Services/SettingsService.cs`, `dllmain.cpp`, `Program.cs`)
- [ ] New setting is pushed to the native hook via an `Update*` method.
- [ ] `ReadSettings` tolerates unknown language tokens and expands the `ALL` sentinel.
- [ ] `disable` signals `POWERACCENT_EXIT_EVENT` (fallback `TerminateProcess`); hook never orphaned.
- [ ] GPO checked on both DLL (`gpo_policy_enabled_configuration`) and UI (`Program.cs`) sides.

## Hygiene
- [ ] No repo-wide dependency bump (`Directory.Packages.props`) bundled into this feature PR.
- [ ] End-user strings in `Resources.resw`; long labels checked for overflow.
