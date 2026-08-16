# Shortcut Guide — PR Review Checklist

Read the diff **cold** first (form your own concerns), then use this checklist only for the
files the PR actually touches. Source root: `src/modules/ShortcutGuide/`.

## If the PR adds/edits a manifest (`Assets/ShortcutGuide/Manifests/*.yml`)

- [ ] **Filename & `PackageName` match the WinGet package id.** No WinGet package → both are
      prefixed with `+` (e.g. `+Godot.Godot`). `+WindowsNT` only for OS components.
- [ ] Locale suffix present (`.en-US.yml`); a localized `.{lang}.yml` falls back to `.en-US.yml`.
- [ ] **No bare digit keys** — literal digits use the `<N>` token (`"<9>"`), not `9`
      (bare number = virtual-key code).
- [ ] **Special keys use spec `<...>` tokens** (`<Delete>`, `<Tab>`, `<Space>`, `<Insert>`,
      `<Escape>`, `<PageUp>`, `<PageDown>`, `<Enter>`, arrows) — not `Delete`/`Back`/etc.
- [ ] **No modifier-only shortcuts** (e.g. `Shift` with empty `Keys`); every entry has a real key.
- [ ] `Name` / `SectionName` in **sentence case** (feature/proper nouns kept capitalized).
- [ ] `WindowFilter` is an exact `.exe` name or `*` — no other wildcards.
- [ ] `<TASKBAR1-9>` section present only if the app genuinely has Win+number taskbar shortcuts.
- [ ] Spellcheck: new product/app words added to `.github/actions/spell-check/expect.txt`.
- [ ] PR description's category/section count matches the file (reviewers flag mismatches).

## If the PR touches the overlay UI (`OverlayWindow`, `MainPaneControl`, `TaskbarPaneControl`)

- [ ] `MainPaneControl` and `TaskbarPaneControl` remain hosted in the single `OverlayWindow`; no
      second native taskbar window or cross-window activation is introduced.
- [ ] App-list initialization failure still raises `InitializationFailed`; taskbar enumeration
      failure/no-buttons returns no layout and hides the taskbar pane. Broader layout exceptions
      need explicit handling rather than assumed containment.
- [ ] No empty native `Title` assigned with `ExtendsContentIntoTitleBar`; `ResourceLoader.GetString`
      result guarded for `""`/null (#49069).
- [ ] `RepositionToCursorMonitor` and `UpdateTaskbarPaneLayout` keep physical/DIP conversions,
      mixed-DPI behavior, and moved/vertical taskbar-edge alignment correct.

## If the PR touches startup / lifecycle (`Program.cs`, `dllmain.cpp`)

- [ ] GPO-disabled short-circuits in **both** the C++ module and `Program.cs`.
- [ ] Single-instance guard (`AppInstance.FindOrRegisterForKey`) preserved.
- [ ] Manifest copy + `IndexYmlGenerator.exe` + `PowerToysShortcutsPopulator` ordering intact on
      the background thread; copy/start/non-zero-exit failures log the stage and abort that refresh
      rather than indexing mixed old/new manifests.
- [ ] Index generation preserves the previous valid `index.yml` until a complete replacement is
      ready; current delete-before-parse behavior can leave no fallback after malformed input.
- [ ] `OnHotkeyEx` starts the process only when inactive and signals `triggerEvent`; `App` toggles
      the persistent overlay. Win-key hold settings and disable-time process termination remain coherent.

## Always

- [ ] Logic change ships a test in `ShortcutGuide.UnitTests`.
- [ ] No bare relative paths in project files; deps go through the repo's central props.
- [ ] End-user-facing strings are localizable (not hard-coded), except deliberate non-empty
      fallbacks like the `"Shortcut Guide"` title guard.
