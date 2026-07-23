---
name: keyboardmanager-knowledge
description: 'PowerToys Keyboard Manager module knowledge: feature->file/function map for the low-level keyboard hook, single-key/shortcut/key-to-text/app-specific remap handlers, input injection, mapping serialization, and both editors (legacy XAML-islands + new WinUI3). Recurring regression playbooks (stale AltGr flag -> sticky Ctrl, modifier->non-modifier delivered as WM_SYSKEYDOWN, WM_SYSKEYDOWN dropped while Alt held, empty SendVirtualInput false "blocked" error, UIPI injection failure -> stranded key, multiline text replacement, WinUI None/keycode-0 + binding-revert), maintainer review rules, and pitfalls. Load when planning, fixing, triaging, or reviewing changes under src/modules/keyboardmanager — key/shortcut remapping, key-eating/suppression, per-app remaps, hook timing/races, stuck keys, text replacement, editor validation. Keywords: Keyboard Manager, KBM, key remap, low-level hook, WH_KEYBOARD_LL, SendInput, AltGr, WM_SYSKEYDOWN, stuck modifier, key-to-text, UIPI, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Keyboard Manager Knowledge

Grounded engineering knowledge for the PowerToys **Keyboard Manager (KBM)** module — a global
key/shortcut remapper built on a low-level keyboard hook (`WH_KEYBOARD_LL`) that eats original
key events and injects replacements with `SendInput`. It supports single-key remaps, shortcut
remaps, key-to-text remaps, per-app (app-specific) remaps, and disabling keys. Use this to
localize code fast, avoid the recurring "stuck key / dropped remap / injection timing" traps, and
enforce the conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/keyboardmanager/` and needing prior art.
- Fixing/triaging a KBM bug: a modifier gets stuck, a remap is silently dropped, injected keys
  behave as `Alt+<key>`, key-to-text emits wrong output, or the editor won't open/validate.
- Reviewing a KBM PR against maintainer conventions and the hook/injection regression traps.
- Touching the hook dispatch order, any `Handle*RemapEvent`, input injection, or mapping
  serialization.
- Working on either editor: the legacy XAML-islands editor (`KeyboardManagerEditorLibrary`) or the
  new WinUI 3 editor (`KeyboardManagerEditorUI`, default since PR #48245).

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Engine host process entry | `KeyboardManagerEngine/main.cpp` |
| DLL module interface (enable/disable, settings, GPO) | `dll/dllmain.cpp` |
| Low-level hook install + callback | `KeyboardManagerEngineLibrary/KeyboardManager.cpp` `HookProc` (`SetWindowsHookEx(WH_KEYBOARD_LL,…)`) |
| Hook dispatch order (priority chain) | `KeyboardManagerEngineLibrary/KeyboardManager.cpp` `HandleKeyboardHookEvent` |
| Single-key remap (key→key / key→shortcut / disable) | `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` `HandleSingleKeyRemapEvent` |
| Shortcut remap (incl. AltGr, chords, run-program/URI) | `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` `HandleShortcutRemapEvent` |
| App-specific shortcut remap | `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` `HandleAppSpecificShortcutRemapEvent` |
| Single-key → text remap | `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` `HandleSingleKeyToTextRemapEvent` |
| OS-level (global) shortcut remap wrapper | `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` `HandleOSLevelShortcutRemapEvent` |
| KBM reentrancy guard (skip our own injected events) | `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp` `GeneratedByKBM`; `dwExtraInfo` flags in `common/KeyboardManagerConstants.h` (`KEYBOARDMANAGER_SINGLEKEY_FLAG`, `_SHORTCUT_FLAG`, `_SUPPRESS_FLAG`) |
| Input injection (batched `SendInput`) | `common/Input.h` `SendVirtualInput`; `common/Helpers.cpp` `SetKeyEvent`, `SetDummyKeyEvent`, `SetModifierKeyEvents`, `SendTextInput` |
| Runtime remap state (invoked shortcuts, injection-failed passthrough) | `KeyboardManagerEngineLibrary/State.cpp` / `KeyboardManagerEngineLibrary/State.h` (`ConsumeSingleKeyRemapInjectionFailed`, `CheckShortcutRemapInvoked`) |
| Mapping model / serialization (JSON load+save) | `common/MappingConfiguration.cpp` `LoadSettings`, `SaveSettingsToFile`, `LoadSingleKeyRemaps`, `LoadShortcutRemaps`, `LoadSingleKeyToTextRemaps`, `LoadAppSpecificShortcutRemaps` |
| Shortcut / modifier model | `common/Shortcut.cpp` / `common/Shortcut.h` (`CheckModifiersKeyboardState`), `common/Modifiers.h`, `common/ModifierKey.h`, `common/RemapShortcut.h` |
| Disabled-key sentinel | `src/common/interop/shared_constants.h` `CommonSharedConstants::VK_DISABLED` (`0x100`); WinUI serialization constants in `KeyboardManagerEditorUI/Pages/MainPage.xaml.cs` (`VkDisabled`, `VkDisabledString`) |
| Legacy editor (XAML islands) | `KeyboardManagerEditorLibrary/` `EditKeyboardWindow.cpp`, `EditShortcutsWindow.cpp`, `SingleKeyRemapControl.cpp`, `ShortcutControl.cpp`, `KeyDropDownControl.cpp`, `BufferValidationHelpers.cpp`, `LoadingAndSavingRemappingHelper.cpp`, `Dialog.cpp` |
| New WinUI 3 editor (default) | `KeyboardManagerEditorUI/Pages/MainPage.xaml.cs`, `KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml.cs`, `KeyboardManagerEditorUI/Controls/KeyDropDownButton.xaml.cs`, `KeyboardManagerEditorUI/Helpers/KeyboardHookHelper.cs`, `KeyboardManagerEditorUI/Helpers/ServiceStatusHelper.cs`, `KeyboardManagerEditorUI/Settings/SettingsManager.cs` |
| Engine unit tests | `KeyboardManagerEngineTest/` (project **KeyboardManager.Engine.UnitTests**) |
| Editor unit tests | `KeyboardManagerEditorTest/` |

**Hook dispatch order (critical, `HandleKeyboardHookEvent`):** events flow through
`HandleSingleKeyRemapEvent` → `HandleAppSpecificShortcutRemapEvent` → `HandleSingleKeyToTextRemapEvent`
→ `HandleOSLevelShortcutRemapEvent`. A handler returning `1` **suppresses** the event so later
handlers/foreground app never see it; returning `0` lets it pass through. Single-key remaps have
priority over shortcuts by design. Events tagged `KEYBOARDMANAGER_SUPPRESS_FLAG` are eaten
immediately; events matching `GeneratedByKBM` are ignored to avoid remapping our own injections.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Stale AltGr flag → sticky Ctrl
- **Symptom:** after pressing/releasing AltGr (Right Alt) **without** triggering any shortcut, LCtrl
  becomes permanently stuck for any shortcut remap that uses LCtrl.
- **Where:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp::HandleShortcutRemapEvent`, `static bool isAltRightKeyInvoked`.
- **Root cause:** the flag was set on AltGr (RAlt + LCtrl) but only reset inside a branch that
  required a shortcut to be actively invoked, so a bare AltGr press/release left it `true`
  permanently — blocking modifier restoration at ~13 sites and `break`-ing on LCtrl key-up.
- **Guardrail:** reset `isAltRightKeyInvoked` on `VK_RMENU` key-up **regardless** of invoked state;
  gate the "set" to key-down only. Beware `static` locals persisting across hook invocations.
  Evidence: issue [#46693](https://github.com/microsoft/PowerToys/issues/46693); fix
  [PR #46672](https://github.com/microsoft/PowerToys/pull/46672).

### Modifier → non-modifier injected as WM_SYSKEYDOWN
- **Symptom:** remapping e.g. Left Alt → Backspace deletes whole **words** instead of characters —
  apps see `Alt+Backspace` because the injected key arrives as `WM_SYSKEYDOWN`, not `WM_KEYDOWN`.
- **Where:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp::HandleSingleKeyRemapEvent` (modifier-state reset before injection).
- **Root cause:** `SendInput` is called inside the hook callback while the original modifier is still
  down, so the OS stamps the injected key with the Alt/system context.
- **Guardrail:** inject a `KEYEVENTF_KEYUP` with `KEYBOARDMANAGER_SUPPRESS_FLAG` to reset the modifier
  state **before** injecting the target (same mechanism used for the Caps Lock case, issue #3397).
  Evidence: issue [#47191](https://github.com/microsoft/PowerToys/issues/47191); fix
  [PR #47192](https://github.com/microsoft/PowerToys/pull/47192).

### WM_SYSKEYDOWN dropped while Alt held (stuck modifiers / dropped key-to-text)
- **Symptom:** a single-key→text (or single-key) remap silently does nothing whenever Alt is down;
  modifiers can get stuck.
- **Where:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp::HandleSingleKeyToTextRemapEvent` (and the single-key path).
- **Root cause:** the key-down guard accepted only `WM_KEYDOWN`; while Alt is held the OS delivers
  `WM_SYSKEYDOWN`, so the remap was skipped.
- **Guardrail:** accept **both** `WM_KEYDOWN` and `WM_SYSKEYDOWN` for key-down; before text injection,
  release held modifiers preceded by a dummy key event, and **never re-press** released modifiers
  (once you inject their key-up, `GetAsyncKeyState` reports them up). Evidence:
  [PR #48571](https://github.com/microsoft/PowerToys/pull/48571).

### Empty SendVirtualInput → false "injection blocked" error
- **Symptom:** a spurious error log on every otherwise-successful shortcut→text remap.
- **Where:** `common/Input.h::SendVirtualInput`; call site in `HandleShortcutRemapEvent` text branch.
- **Root cause:** calling `SendVirtualInput` on an **empty** vector runs `SendInput(0, …)`, which
  returns `0` per the Win32 contract; the new failure-detection logic read `0` as "blocked by UIPI".
- **Guardrail:** early-return `true` for empty input in `SendVirtualInput`, and don't call it when
  there are no key events to inject. Evidence:
  [PR #48571](https://github.com/microsoft/PowerToys/pull/48571) (review by @MuyuanMS).

### UIPI injection failure → key stranded DOWN / eaten
- **Symptom:** against an elevated foreground window, a remapped key produces nothing or leaves the
  physical key stuck down (its down reached the app, its up got swallowed).
- **Where:** `KeyboardManagerEngineLibrary/KeyboardEventHandlers.cpp::HandleSingleKeyRemapEvent`; `KeyboardManagerEngineLibrary/State.cpp`
  `ConsumeSingleKeyRemapInjectionFailed`.
- **Root cause:** when `SendVirtualInput` fails (UIPI blocks injection into a higher-integrity
  window) the handler still ate the original key.
- **Guardrail:** on injection failure `return 0` so the **original** key passes through; record the
  failed key-down so the matching **key-up is also passed through** (key-down and key-up arrive as
  separate hook events). Evidence: `HandleSingleKeyRemapEvent` failure branch +
  [PR #48571](https://github.com/microsoft/PowerToys/pull/48571).

### Multiline text replacement inconsistency
- **Symptom:** multiline key-to-text replacement (introduced in 0.98.0 via Ctrl+V) behaves
  inconsistently across apps.
- **Where:** `HandleShortcutRemapEvent` / `HandleSingleKeyToTextRemapEvent` text path; `Helpers::SendTextInput`.
- **Root cause:** the Ctrl+V multiline approach was unreliable.
- **Guardrail:** revert to per-character/newline injection; emit **Shift+Enter** at newline
  indicators so it works in chat boxes and plain editors. Evidence: fix
  [PR #46794](https://github.com/microsoft/PowerToys/pull/46794) (closes #46498, #46440, #46366).

### New WinUI editor: synthetic "None"/keycode-0 leaks into mappings
- **Symptom:** the manual key picker can save an invalid mapping containing keycode `0` (`"None"`),
  or the disable action persists unvalidated trigger keys.
- **Where:** `KeyboardManagerEditorUI/Controls/KeyDropDownButton.xaml.cs` `GetKeyList`;
  `KeyboardManagerEditorUI/Controls/UnifiedMappingControl.xaml.cs` (`*KeyDown_KeyChanged`, `ValidateDropDownSelection`);
  `KeyboardManagerEditorUI/Pages/MainPage.xaml.cs` (`SaveDisableMapping`, VK_DISABLED handling).
- **Root cause:** `LayoutMap` injects a synthetic `None` (keycode 0) entry for shortcut lists;
  selecting it, or leaving empty placeholder slots, bypassed validation.
- **Guardrail:** filter `KeyCode == 0` out of the picker; skip empty placeholder slots when
  validating; add a dedicated `ValidateDisableMapping`; centralize the disabled sentinel
  (`0x100` / `"256"`) into one constant. Evidence:
  [PR #46377](https://github.com/microsoft/PowerToys/pull/46377).

## Review Rules

Enforce these when reviewing or authoring KBM changes:

- **Test every remap change under both `WM_KEYDOWN` and `WM_SYSKEYDOWN`.** While Alt is held the OS
  delivers the `SYS` variants; a `WM_KEYDOWN`-only guard silently drops the remap (PR #48571, #47192).
- **Guard empty input at every `SendVirtualInput` call site.** `SendInput(0, …)` returns `0`, which
  the failure-detection path reads as "blocked" — early-return `true` on empty (PR #48571).
- **On injection failure, pass the original key through (`return 0`), and pair key-down passthrough
  with key-up passthrough** via `State::ConsumeSingleKeyRemapInjectionFailed`, or you strand the
  physical key DOWN (PR #48571).
- **Reset modifier state before injecting a non-modifier target** (suppress-flag key-up) so the OS
  doesn't stamp the injected key as `WM_SYSKEYDOWN` (#47191 / PR #47192).
- **Precede a held Win/Alt key-up with a dummy key event.** Releasing a lone Win (Start Menu) or Alt
  (menu bar) otherwise triggers its lone-press action; see `SetDummyKeyEvent` usage in the handlers.
- **Never remap KBM's own injected events.** Every injected event must carry a
  `KEYBOARDMANAGER_*_FLAG` in `dwExtraInfo`; guard entry with `GeneratedByKBM`.
- **Beware `static` locals in hook handlers.** State like `isAltRightKeyInvoked` persists across
  every hook call; ensure a reset path exists for every set path (#46693 / PR #46672).
- **New WinUI editor: don't set a bound `DependencyProperty` directly to revert.** Assigning
  `dropDown.KeyName = e.OldKeyName` overwrites the `{Binding}` expression; mutate the
  `ObservableCollection` (`RemoveAt` + `Insert`) so the binding re-reads (PR #46377).
- **Dispose `Process` objects from `GetProcessesByName`.** `ServiceStatusHelper` polls every ~3s;
  undisposed handles leak over time (PR #46377).
- **Centralize sentinels/constants, don't scatter literals** (`VK_DISABLED` = `0x100`/`"256"`) —
  duplicated literals drift (PR #46377).
- **Ship a test with every engine fix.** Add/extend `KeyboardManagerEngineTest`
  (**KeyboardManager.Engine.UnitTests**); PR #48571 added a mockable injection-failure seam
  (`SetSendVirtualInputShouldFail`) so stuck-key behavior is testable.

## Pitfalls

- **Never** assume a key-down is `WM_KEYDOWN` — with Alt held it is `WM_SYSKEYDOWN`; handle both or
  the remap vanishes.
- **Never** call `SendVirtualInput` with an empty vector — `SendInput(0,…)` returns 0 and trips the
  false "blocked by UIPI" path.
- **Never** eat the original key when injection fails — return 0 to pass through, and remember the
  key-down so its key-up passes through too, or the key is stranded down.
- **Never** inject a non-modifier target while the source modifier is still down — the OS delivers it
  as `WM_SYSKEYDOWN` (Alt context) and apps misinterpret it (e.g. Alt+Backspace deletes a word).
- **`static` locals in the hook survive across events** — a set-without-reset (AltGr flag) sticks a
  modifier permanently.
- **Releasing a lone Win/Alt key-up fires Start Menu / menu bar** — a dummy key event must precede it.
- **Two editors exist.** The new WinUI 3 editor (`KeyboardManagerEditorUI`) is default since
  PR #48245; the legacy XAML-islands editor (`KeyboardManagerEditorLibrary`) still ships. Fix the
  right one and mirror validation logic where behavior must match.
- **`KEYBOARDMANAGER_SUPPRESS_FLAG` events are eaten unconditionally** at the top of
  `HandleKeyboardHookEvent` — use it deliberately for state-reset injections.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + open reports.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a KBM PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/keyboardmanager/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/keyboardmanager)
- [Low-Level Keyboard Hook (WH_KEYBOARD_LL)](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc) · [SendInput](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-sendinput) · [UIPI](https://learn.microsoft.com/en-us/windows/win32/winmsg/about-messages-and-message-queues#guidelines)
