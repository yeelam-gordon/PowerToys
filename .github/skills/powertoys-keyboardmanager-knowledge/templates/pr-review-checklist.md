# Keyboard Manager — PR Review Checklist

Apply after reading the diff cold (see anti-anchoring in SKILL.md). Only check rows whose code
paths the PR actually touches.

## Engine / hook (`KeyboardManagerEngineLibrary`, `common`)

- [ ] **Key-down guards accept both `WM_KEYDOWN` and `WM_SYSKEYDOWN`.** Alt-held delivers the SYS
      variant; a `WM_KEYDOWN`-only check silently drops the remap. (PR #48571, #47192)
- [ ] **Every `SendVirtualInput` call is empty-safe.** Either the input is guaranteed non-empty, or
      the empty early-return in `Input.h` covers it. `SendInput(0,…)` returns 0 = false "blocked". (PR #48571)
- [ ] **Injection-failure passthrough is complete.** On failure `return 0` (pass original through) AND
      record the key-down so the matching key-up passes through (`State::ConsumeSingleKeyRemapInjectionFailed`). (PR #48571)
- [ ] **Modifier state reset before injecting a non-modifier target** (suppress-flag key-up) to avoid
      `WM_SYSKEYDOWN` stamping. (#47191 / PR #47192)
- [ ] **Dummy key precedes any held Win/Alt key-up release** (`SetDummyKeyEvent`) so Start Menu / menu
      bar isn't triggered.
- [ ] **Released modifiers are not re-pressed** after text injection.
- [ ] **All injected events carry a `KEYBOARDMANAGER_*_FLAG`** and `GeneratedByKBM` guards re-entry.
- [ ] **No unbalanced `static` local state** in hook handlers (every set has a reset path). (#46693)
- [ ] **Hook dispatch order / suppression semantics preserved** — returning `1` eats the event for all
      later handlers and the foreground app.
- [ ] **A unit test accompanies the fix** in `KeyboardManagerEngineTest`; use the injection-failure
      mock seam where relevant.

## Mapping serialization (`common/MappingConfiguration.cpp`)

- [ ] Load paths tolerate malformed/legacy JSON without corrupting the file on save.
- [ ] New remap types round-trip through both `Load*` and `SaveSettingsToFile`.

## New WinUI editor (`KeyboardManagerEditorUI`)

- [ ] Synthetic `None`/keycode `0` cannot be selected or persisted (filtered in `GetKeyList`;
      empty placeholder slots skipped in validation). (PR #46377)
- [ ] Reverting an invalid dropdown selection mutates the `ObservableCollection`, not the bound DP. (PR #46377)
- [ ] Disable action has dedicated validation (`ValidateDisableMapping`). (PR #46377)
- [ ] Disabled sentinel uses the shared constant, not scattered `0x100`/`"256"` literals. (PR #46377)
- [ ] `Process` objects from `GetProcessesByName` are disposed (timer handle leak). (PR #46377)
- [ ] Editing surface degrades to read-only when the native mapping service is unavailable. (PR #46377)
- [ ] User-facing strings are localized (from `Resources.resw`), not hard-coded. (PR #46377)

## Cross-cutting

- [ ] If engine behavior changed, the legacy editor validation (`BufferValidationHelpers`) and the new
      editor stay consistent.
- [ ] No new bare relative paths in project files; new deps go through central package management.
