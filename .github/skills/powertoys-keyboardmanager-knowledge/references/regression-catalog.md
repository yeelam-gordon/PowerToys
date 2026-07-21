# Keyboard Manager — Regression Catalog

Progressive-disclosure companion to `SKILL.md`. Fuller list of grounded regressions/decisions plus
open reports that indicate live risk areas. Every fixed entry cites a real PR/issue; verify in
source before relying on a claim.

## Fixed regressions (with guardrails)

### 1. Stale AltGr flag → sticky Ctrl
- **Files:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` (`HandleShortcutRemapEvent`, `static bool isAltRightKeyInvoked`).
- **Root cause:** flag set on AltGr (RAlt + LCtrl) but reset only in a branch requiring an active
  shortcut; a bare AltGr press/release left it `true` permanently, blocking modifier restoration at
  ~13 sites and `break`-ing on LCtrl key-up.
- **Guardrail:** reset on `VK_RMENU` key-up regardless of invoked state; set only on key-down.
- **Evidence:** [#46693](https://github.com/microsoft/PowerToys/issues/46693) → [PR #46672](https://github.com/microsoft/PowerToys/pull/46672).

### 2. Modifier → non-modifier injected as WM_SYSKEYDOWN
- **Files:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` (`HandleSingleKeyRemapEvent`).
- **Root cause:** `SendInput` runs in the hook while the source modifier is still down, so the OS
  stamps the injected key with Alt/system context. E.g. LAlt → Backspace deletes whole words.
- **Guardrail:** inject a `KEYEVENTF_KEYUP` with `KEYBOARDMANAGER_SUPPRESS_FLAG` to reset modifier
  state before injecting the target — the same mechanism used for the Caps Lock case (issue #3397).
- **Evidence:** [#47191](https://github.com/microsoft/PowerToys/issues/47191) → [PR #47192](https://github.com/microsoft/PowerToys/pull/47192).

### 3. WM_SYSKEYDOWN dropped while Alt held; stuck modifiers on key-to-text
- **Files:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` (`HandleSingleKeyToTextRemapEvent`), `common/Input.h`, `common/Helpers.cpp`.
- **Root cause(s):** (a) key-down guard accepted only `WM_KEYDOWN`; (b) lone Win/Alt key-up release
  triggered Start Menu / menu bar; (c) re-pressing released modifiers could strand them down.
- **Guardrail:** accept `WM_SYSKEYDOWN` too; precede modifier releases with a dummy key event and
  only inject them when a modifier is actually held; never re-press released modifiers; route
  `SendTextInput` through `InputInterface` (mockable, per-character flush preserved).
- **Evidence:** [PR #48571](https://github.com/microsoft/PowerToys/pull/48571). Test seam:
  `MockedInput::SetSendVirtualInputShouldFail`; new tests
  `RemappedKey_ShouldPassOriginalKeyThrough_WhenInjectionFails`,
  `HandleSingleKeyToTextRemapEvent_ShouldFireAndReleaseAlt_WhenAltIsHeld`.

### 4. Empty SendVirtualInput → false "injection blocked" error
- **Files:** `common/Input.h` (`SendVirtualInput`); text branch of `HandleShortcutRemapEvent`.
- **Root cause:** empty vector → `SendInput(0, …)` returns 0, read as "blocked by UIPI" by the new
  failure-detection logic → spurious error on every successful shortcut→text remap.
- **Guardrail:** early-return `true` on empty input; don't call `SendVirtualInput` when there are no
  key events to inject (single-key→text branch drops the redundant call).
- **Evidence:** [PR #48571](https://github.com/microsoft/PowerToys/pull/48571) review (@MuyuanMS).

### 5. UIPI injection failure → key stranded DOWN / eaten
- **Files:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` (`HandleSingleKeyRemapEvent`), `KeyboardManagerEngineLibrary/State.cpp` (`ConsumeSingleKeyRemapInjectionFailed`).
- **Root cause:** when injection into a higher-integrity foreground window failed, the original key
  was still swallowed.
- **Guardrail:** on failure `return 0` to pass the original through; record the failed key-down so
  the matching key-up (a separate hook event) is also passed through, avoiding a stranded-down key.
- **Evidence:** in-source failure branch; hardened in [PR #48571](https://github.com/microsoft/PowerToys/pull/48571).

### 6. Multiline text replacement inconsistency (0.98.0 regression)
- **Files:** `Helpers::SendTextInput`; text path of the remap handlers.
- **Root cause:** the Ctrl+V multiline approach added in 0.98.0 was inconsistent across apps.
- **Guardrail:** revert to per-character injection; send Shift+Enter for newline indicators.
- **Evidence:** [PR #46794](https://github.com/microsoft/PowerToys/pull/46794) closes
  [#46498](https://github.com/microsoft/PowerToys/issues/46498),
  [#46440](https://github.com/microsoft/PowerToys/issues/46440),
  [#46366](https://github.com/microsoft/PowerToys/issues/46366).

### 7. New WinUI editor manual-key-selection hardening
- **Files:** `KeyboardManagerEditorUI/Pages/MainPage.xaml.cs`,
  `KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml.cs`,
  `KeyboardManagerEditorUI/Controls/KeyDropDownButton.xaml.cs`,
  `KeyboardManagerEditorUI/Helpers/ServiceStatusHelper.cs`,
  `KeyboardManagerEditorUI/Settings/SettingsManager.cs`,
  `KeyboardManagerEditorUI/Helpers/KeyboardHookHelper.cs`.
- **Fixes:** filter synthetic `None`/keycode 0 from the picker; `ValidateDropDownSelection` skips
  empty placeholder slots; dedicated `ValidateDisableMapping`; centralized `VK_DISABLED`
  (`src/common/interop/shared_constants.h`) plus WinUI `VkDisabled`/`VkDisabledString`
  (`KeyboardManagerEditorUI/Pages/MainPage.xaml.cs`); binding-safe revert via `ObservableCollection.RemoveAt`+`Insert` (not setting the
  bound DP); dispose `Process` handles from the 3s polling timer; read-only mode when the native
  service is down; localized dialog titles; broadened hook-init `catch`; select ComboBox item by Tag
  (not hard-coded index).
- **Evidence:** [PR #46377](https://github.com/microsoft/PowerToys/pull/46377).
- **Design note:** maintainers **declined** validation that would forbid remapping/disabling
  modifier-only or "protected" shortcuts (e.g. Win+L) — these are legitimate use cases
  ([discussion](https://github.com/microsoft/PowerToys/pull/46377), @CrazyGunman2C4U).

## Key decisions / conventions

- **New WinUI 3 editor is default** since [PR #48245](https://github.com/microsoft/PowerToys/pull/48245);
  the legacy XAML-islands editor (`KeyboardManagerEditorLibrary`) still ships. Keep validation
  behavior consistent across both.
- **Single-key remaps have priority over shortcuts** and suppress the event before shortcut handlers
  run (`HandleKeyboardHookEvent`).
- **All KBM-injected events are tagged** via `dwExtraInfo` (`KEYBOARDMANAGER_SINGLEKEY_FLAG`,
  `_SHORTCUT_FLAG`, `_SUPPRESS_FLAG`) and filtered by `GeneratedByKBM` to prevent self-remapping.

## Open reports (live risk areas — confirm before trusting)

Detection/reliability: [#49307](https://github.com/microsoft/PowerToys/issues/49307) (fails after
sleep/startup), [#48854](https://github.com/microsoft/PowerToys/issues/48854) (loses effect over
time), [#49254](https://github.com/microsoft/PowerToys/issues/49254),
[#48864](https://github.com/microsoft/PowerToys/issues/48864) (inconsistent).
Text/key-to-text: [#48611](https://github.com/microsoft/PowerToys/issues/48611) (plain `h` triggers
mapping and breaks input), [#48900](https://github.com/microsoft/PowerToys/issues/48900) (unexpected
char mid-text). Specific keys/layouts: [#49135](https://github.com/microsoft/PowerToys/issues/49135)
/ [#48882](https://github.com/microsoft/PowerToys/issues/48882) (`/` key),
[#49227](https://github.com/microsoft/PowerToys/issues/49227) (Copilot key),
[#49228](https://github.com/microsoft/PowerToys/issues/49228) (Caps/LCtrl/Win chain). Editor/config:
[#48936](https://github.com/microsoft/PowerToys/issues/48936) (config corrupted rebinding `}`),
[#48945](https://github.com/microsoft/PowerToys/issues/48945) (deleted shortcut still shows),
[#48943](https://github.com/microsoft/PowerToys/issues/48943) (key order),
[#48711](https://github.com/microsoft/PowerToys/issues/48711) /
[#48856](https://github.com/microsoft/PowerToys/issues/48856) (runtime/component required),
[#48921](https://github.com/microsoft/PowerToys/issues/48921) (editor self-edits). Resource:
[#49052](https://github.com/microsoft/PowerToys/issues/49052) (suspected memory leak).
