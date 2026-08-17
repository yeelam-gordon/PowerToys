# Keyboard Manager — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

This file owns historical evidence: regressions, chronology, review decisions, open issue clusters,
and confidence caveats. `SKILL.md` owns the current module map, review rules, and operational
guidance. Read a diff cold before consulting this ledger, and verify claims against current source.

## Regression evidence

### KBM-E1 — Stale AltGr state caused sticky Ctrl

- **Observed:** A bare AltGr press/release could leave LCtrl effectively stuck for shortcut remaps.
- **Source:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp`,
  `HandleShortcutRemapEvent`, `static bool isAltRightKeyInvoked`.
- **Finding:** The flag was set for AltGr (RAlt + LCtrl), but reset only while a shortcut was
  actively invoked. The stale value blocked modifier restoration at roughly 13 sites and broke on
  LCtrl key-up.
- **Decision:** Set only on key-down; reset on `VK_RMENU` key-up regardless of invoked state.
- **Chronology/evidence:** [issue #46693](https://github.com/microsoft/PowerToys/issues/46693) →
  [PR #46672](https://github.com/microsoft/PowerToys/pull/46672).

### KBM-E2 — Modifier remap target inherited Alt/system context

- **Observed:** LAlt → Backspace deleted whole words because the target arrived as
  `WM_SYSKEYDOWN`.
- **Source:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp`,
  `HandleSingleKeyRemapEvent`.
- **Finding:** `SendInput` ran inside the hook while the source modifier was still down.
- **Decision:** Inject a suppress-tagged `KEYEVENTF_KEYUP` before the target, matching the Caps Lock
  mechanism from issue #3397.
- **Chronology/evidence:** [issue #47191](https://github.com/microsoft/PowerToys/issues/47191) →
  [PR #47192](https://github.com/microsoft/PowerToys/pull/47192).

### KBM-E3 — Alt-held key-to-text dropped events and stranded modifiers

- **Observed:** Key-to-text could do nothing while Alt was held; modifier release/re-press handling
  could trigger Start/menu UI or leave modifiers down.
- **Source:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp`,
  `HandleSingleKeyToTextRemapEvent`; `common/Input.h`; `common/Helpers.cpp`.
- **Finding:** The guard accepted only `WM_KEYDOWN`; lone Win/Alt releases lacked a preceding dummy
  event; re-pressing released modifiers could strand them.
- **Decision:** Accept `WM_KEYDOWN` and `WM_SYSKEYDOWN`; release only held modifiers, precede
  Win/Alt release with a dummy event, never re-press released modifiers, and route
  `SendTextInput` through `InputInterface` while preserving per-character flush.
- **Chronology/evidence:** [PR #48571](https://github.com/microsoft/PowerToys/pull/48571).
- **Test evidence:** `MockedInput::SetSendVirtualInputShouldFail`;
  `RemappedKey_ShouldPassOriginalKeyThrough_WhenInjectionFails`;
  `HandleSingleKeyToTextRemapEvent_ShouldFireAndReleaseAlt_WhenAltIsHeld`.

### KBM-E4 — Empty injection batch reported a false UIPI failure

- **Observed:** Successful shortcut-to-text remaps emitted an “injection blocked” error.
- **Source:** `common/Input.h`, `SendVirtualInput`; text branch of
  `HandleShortcutRemapEvent`.
- **Finding:** `SendInput(0, …)` returns 0, which failure detection interpreted as UIPI blocking.
- **Decision:** Return `true` for an empty batch and omit redundant empty-batch calls.
- **Chronology/evidence:** Maintainer review by @MuyuanMS on
  [PR #48571](https://github.com/microsoft/PowerToys/pull/48571).

### KBM-E5 — UIPI failure swallowed a physical key pair

- **Observed:** Against a higher-integrity foreground window, a remapped key was eaten or left down.
- **Source:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp`,
  `HandleSingleKeyRemapEvent`; `KeyboardManagerEngineLibrary/State.cpp`,
  `ConsumeSingleKeyRemapInjectionFailed`.
- **Finding:** Injection failure still suppressed the original event; down/up are separate hook
  callbacks.
- **Decision:** Return 0 on failed injection, record the failed key-down, and pass its matching
  key-up through.
- **Chronology/evidence:** Existing source failure branch, hardened by
  [PR #48571](https://github.com/microsoft/PowerToys/pull/48571).

### KBM-E6 — Multiline text replacement was inconsistent across apps

- **Observed:** The Ctrl+V multiline path introduced in 0.98.0 behaved inconsistently.
- **Source:** `Helpers::SendTextInput`; text paths in the remap handlers.
- **Decision:** Restore per-character injection and emit Shift+Enter for newline indicators.
- **Chronology/evidence:** 0.98.0 regression →
  [PR #46794](https://github.com/microsoft/PowerToys/pull/46794), closing
  [#46498](https://github.com/microsoft/PowerToys/issues/46498),
  [#46440](https://github.com/microsoft/PowerToys/issues/46440), and
  [#46366](https://github.com/microsoft/PowerToys/issues/46366).

### KBM-E7 — WinUI editor manual-selection hardening

- **Source:** `KeyboardManagerEditorUI/Pages/MainPage.xaml.cs` (`SaveDisableMapping`,
  `VkDisabled`, `VkDisabledString`); `Controls/UnifiedMappingControl.xaml.cs`
  (`*KeyDown_KeyChanged`, `ValidateDropDownSelection`);
  `Controls/KeyDropDownButton.xaml.cs` (`GetKeyList`);
  `Helpers/ServiceStatusHelper.cs`; `Settings/SettingsManager.cs`;
  `Helpers/KeyboardHookHelper.cs`; `src/common/interop/shared_constants.h`.
- **Accepted changes:** Filter synthetic `None`/keycode 0; skip empty placeholder slots in
  `ValidateDropDownSelection`; add `ValidateDisableMapping`; centralize `VK_DISABLED` and WinUI
  `VkDisabled`/`VkDisabledString`; revert bound values through
  `ObservableCollection.RemoveAt`+`Insert`; dispose polled `Process` handles; use read-only mode
  when the service is down; localize dialog titles; broaden hook-init `catch`; select ComboBox items
  by Tag rather than index.
- **Review decision:** Maintainers declined validation that forbade modifier-only or “protected”
  shortcuts such as Win+L because those are legitimate remap/disable cases.
- **Chronology/evidence:** [PR #46377](https://github.com/microsoft/PowerToys/pull/46377),
  including @CrazyGunman2C4U discussion.

## Decision ledger

| Decision | Evidence | Consequence |
|---|---|---|
| New WinUI 3 editor is the default; legacy XAML-islands editor still ships. | [PR #48245](https://github.com/microsoft/PowerToys/pull/48245) | Keep validation behavior aligned across `KeyboardManagerEditorUI` and `KeyboardManagerEditorLibrary`. |
| Single-key remaps precede shortcut handlers. | `HandleKeyboardHookEvent` | A handler returning 1 suppresses later handlers; priority changes are behavioral changes. |
| KBM injection is tagged and self-filtered. | `dwExtraInfo`; `KEYBOARDMANAGER_SINGLEKEY_FLAG`, `_SHORTCUT_FLAG`, `_SUPPRESS_FLAG`; `GeneratedByKBM` | New injection paths must preserve reentrancy protection. |

## Open evidence clusters

These are reports, not established causes; confirm status and reproduce before relying on them.

| Cluster | Reports |
|---|---|
| Detection/reliability | [#49307](https://github.com/microsoft/PowerToys/issues/49307) after sleep/startup; [#48854](https://github.com/microsoft/PowerToys/issues/48854) loses effect over time; [#49254](https://github.com/microsoft/PowerToys/issues/49254); [#48864](https://github.com/microsoft/PowerToys/issues/48864) inconsistent behavior |
| Text/key-to-text | [#48611](https://github.com/microsoft/PowerToys/issues/48611) plain `h` triggers mapping/breaks input; [#48900](https://github.com/microsoft/PowerToys/issues/48900) unexpected character mid-text |
| Keys/layouts | [#49135](https://github.com/microsoft/PowerToys/issues/49135), [#48882](https://github.com/microsoft/PowerToys/issues/48882) `/`; [#49227](https://github.com/microsoft/PowerToys/issues/49227) Copilot key; [#49228](https://github.com/microsoft/PowerToys/issues/49228) Caps/LCtrl/Win chain |
| Editor/config | [#48936](https://github.com/microsoft/PowerToys/issues/48936) corrupted config rebinding `}`; [#48945](https://github.com/microsoft/PowerToys/issues/48945) deleted shortcut remains; [#48943](https://github.com/microsoft/PowerToys/issues/48943) key order; [#48711](https://github.com/microsoft/PowerToys/issues/48711) runtime/component report closed completed July 27, 2026; [#48856](https://github.com/microsoft/PowerToys/issues/48856) runtime/component required; [#48921](https://github.com/microsoft/PowerToys/issues/48921) editor self-edits |
| Resource use | [#49052](https://github.com/microsoft/PowerToys/issues/49052) suspected memory leak |

## Caveats

- Fixed entries describe historical failure modes, not proof that current code still has them.
- Open reports may be duplicates, stale, environment-specific, or missing a verified root cause.
- Source paths and symbols are retained for localization but must be checked after refactors.
