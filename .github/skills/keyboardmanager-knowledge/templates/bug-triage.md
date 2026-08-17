# Keyboard Manager — Bug Triage (symptom → likely file/function)

Use the Module Map in SKILL.md as **hypotheses to confirm in source**, not ground truth. Start from
the symptom; verify in code before editing.

| Symptom | Start here | Likely cause / check |
|---|---|---|
| A modifier (esp. Ctrl) gets permanently stuck after using AltGr | `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp::HandleShortcutRemapEvent`, `isAltRightKeyInvoked` | `static` flag set without a reset on `VK_RMENU` key-up (#46693 / PR #46672) |
| Remapping a modifier to a non-modifier deletes words / behaves as Alt+key | `HandleSingleKeyRemapEvent` (modifier reset before injection) | injected key stamped `WM_SYSKEYDOWN`; need suppress-flag key-up first (#47191 / PR #47192) |
| Remap silently does nothing while Alt is held | key-down guard in `HandleSingleKeyToTextRemapEvent` / single-key path | guard accepts only `WM_KEYDOWN`, missing `WM_SYSKEYDOWN` (PR #48571) |
| Spurious "injection blocked" error on a working remap | `common/Input.h::SendVirtualInput` + text branch call site | `SendVirtualInput` called with empty vector → `SendInput(0,…)` returns 0 (PR #48571) |
| Remapped key does nothing / physical key stuck down over elevated windows | `HandleSingleKeyRemapEvent` failure branch; `State::ConsumeSingleKeyRemapInjectionFailed` | UIPI blocks injection; must pass original key-down AND key-up through (PR #48571) |
| Multiline text replacement inconsistent across apps | `common/Helpers.cpp::SendTextInput`; text branch of `HandleShortcutRemapEvent` | Ctrl+V multiline approach; use Shift+Enter for newlines (PR #46794) |
| A key/shortcut still fires after being deleted in the editor | `common/MappingConfiguration.cpp` `LoadSettings`/`SaveSettingsToFile`; engine reload path | stale mapping not reloaded, or save didn't persist removal (#48945) |
| Config file corrupted after rebinding certain keys (e.g. `}`) | `common/MappingConfiguration.cpp` `Load*`/`SaveSettingsToFile` | serialization of special key codes (#48936) |
| Wrong/duplicate keys, or keys out of standard order in new editor | `KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml.cs`, `KeyboardManagerEditorUI/Controls/KeyDropDownButton.xaml.cs` | synthetic `None`/keycode 0, ordering, or duplicate handling (#48943, PR #46377) |
| New editor won't open ("component/runtime required") | `KeyboardManagerEditorUI` (WinUI 3 / Windows App Runtime); `KeyboardManagerEditorUI/Helpers/ServiceStatusHelper.cs` | missing WinAppSDK runtime / native service down (#48711, #48856) |
| Editor toggles change UI but don't persist | `KeyboardManagerEditorUI/Pages/MainPage.xaml.cs` (service-availability), `KeyboardManagerEditorUI/Settings/SettingsManager.cs` | native mapping service null; should be read-only (PR #46377) |
| Remap stops working after sleep/startup or over time | hook lifecycle in `KeyboardManagerEngineLibrary/KeyboardManager.cpp` (`HookProc`, install/reinstall); engine host `KeyboardManagerEngine/main.cpp` | hook dropped/not reinstalled (#49307, #48854) |

## Triage steps

1. Reproduce and classify: single-key vs shortcut vs key-to-text vs app-specific vs disable.
2. Identify whether the failure is **injection** (wrong output / stuck key) or **detection**
   (remap never fires) or **editor/serialization** (won't save/load/open).
3. Injection/detection → engine (`KeyboardManagerEngineLibrary`, `common`). Editor → the correct
   editor (new `KeyboardManagerEditorUI` is default; legacy `KeyboardManagerEditorLibrary` still ships).
4. Confirm the hypothesis in source, then add/extend a test in `KeyboardManagerEngineTest`.
