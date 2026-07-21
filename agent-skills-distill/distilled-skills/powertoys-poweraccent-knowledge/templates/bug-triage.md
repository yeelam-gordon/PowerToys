# Quick Accent Bug Triage Map

Start from the user's symptom, jump to the likely file/function, then **confirm in source**
(the Module Map is a hypothesis, not ground truth — see anti-anchoring in SKILL.md).

| Symptom | Likely location | First thing to check | Evidence |
|---|---|---|---|
| Popup on wrong monitor / mis-scaled / too wide | `WindowsFunctions.GetActiveDisplay`, `Calculation.GetRawCoordinatesFromPosition(..., dpi)`, `MainWindow.xaml.cs::GetDisplayMaxWidth` | Bounds & DPI taken from active monitor; DIP↔physical conversions | #40865, #40498, #44980, #47347, #43247, #43031, #48123 |
| Popup flickers at first paint | UI off-screen pre-render spot | Uses `SM_*VIRTUALSCREEN`, not `(-10000,-10000)` | #46593 |
| Menu flashes early / lags on fast typing | `PowerAccent.cs::ShowToolbar` deferred render | `generation == _showGeneration && _visible` re-check | #42821, #39852, #39564, #41761 |
| Letter leaks / deleted / doubled | `KeyboardListener.cpp::OnKeyDown`/`OnKeyUp` | Swallow/pass symmetry across all activation modes; PressAndHold lets base letter through | #40373, #40541, #46963, #48841 |
| Toolbar stuck / won't close | `KeyboardListener.cpp::OnKeyUp` early-returns; `PowerAccent.cs::SendInputAndHideToolbar` | Hide path reached for the focus/app state | #37668, #38915, #44482, #43200, #37488 |
| Shift-typing capital jumps navigation back | `PowerAccent.cs::ProcessNextChar` | `_initialShiftState` captured at show; Shift-for-uppercase vs Shift-for-nav | #46593 |
| Shift stays "locked" | `KeyboardListener.cpp` shift tracking | Shift state cleared after navigation | #45936 |
| Stops activating after update / lost characters | `KeyboardListener.idl`↔`.cs` enums; `CharacterMappings` | Native/managed enum lockstep; defaults match `PowerAccentSettings` | #37922, #39436, #39470, #45480, #45456 |
| First-run "All available" selects only SPECIAL | `SettingsService.ReadSettings` | `ALL` sentinel expanded to `Enum.GetValues<Language>()`; unknown tokens skipped | #47113 → fixed #47117 |
| Usage frequency not remembered after restart | `CharactersUsageInfo` (`UsageInfo.json`), `Program.Terminate` | Save-on-exit path; hard-exit may skip `SaveUsageInfo` if exit races startup | #45630, #44355 |
| Activates in excluded app / game mode | `KeyboardListener.cpp::IsForegroundAppExcluded`, `UpdateExcludedApps` | Cached foreground HWND reset on update/empty list | #47804 |
| IME / on-screen-keyboard conflict | `KeyboardListener.cpp::OnKeyDown` | OSK continuous WM_KEYDOWN repeat swallowed while visible | #41151, #36853 |
| Empty language group header | `CharacterMappings.cs` + `Resources.resw` | Missing resx key for the language | #47211 |

## Triage tips
- Multi-monitor+DPI and hook-timing are the two dominant families — suspect them first for
  positioning and flicker/leak symptoms respectively.
- Reproduce with the **specific activation mode** the reporter used; behavior diverges sharply
  between Space/Arrow/Both/PressAndHold.
- Note target app: leaks/doubles are app-specific (Figma, browser address bars, Sibelius).
