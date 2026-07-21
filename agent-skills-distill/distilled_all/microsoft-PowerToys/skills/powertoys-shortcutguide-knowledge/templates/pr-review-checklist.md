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
- [ ] Spellcheck: new product/app words added to `.github/actions/spell-check/expected.txt`.
- [ ] PR description's category/section count matches the file (reviewers flag mismatches).

## If the PR touches the overlay UI (`ShortcutGuideXAML/MainWindow.xaml.cs`, TaskbarWindow)

- [ ] `WindowSelector_SelectionChanged` and `SetWindowPosition` keep their **try/catch that logs,
      not closes** — escaping exceptions tear down the overlay (#48448/#48481).
- [ ] `App.TaskBarWindow` and `App.TaskBarWindow?.AppWindow` treated as **possibly null** during
      the reentrant Activate→BringToFront chain.
- [ ] No empty native `Title` assigned with `ExtendsContentIntoTitleBar`; `ResourceLoader.GetString`
      result guarded for `""`/null (#49069).
- [ ] Multi-monitor/DPI math still uses work-area + DPI scale (`DisplayHelper`, `DpiHelper`);
      taskbar-overlap adjustment skipped when the taskbar window isn't observable.

## If the PR touches startup / lifecycle (`Program.cs`, `dllmain.cpp`)

- [ ] GPO-disabled short-circuits in **both** the C++ module and `Program.cs`.
- [ ] Single-instance guard (`AppInstance.FindOrRegisterForKey`) preserved.
- [ ] Manifest copy + `IndexYmlGenerator.exe` + `PowerToysShortcutsPopulator` ordering intact on
      the background thread; failures logged, not fatal to the whole launch.
- [ ] Hotkey toggle semantics preserved (`OnHotkeyEx` terminates a running instance).

## Always

- [ ] Logic change ships a test in `ShortcutGuide.UnitTests`.
- [ ] No bare relative paths in project files; deps go through the repo's central props.
- [ ] End-user-facing strings are localizable (not hard-coded), except deliberate non-empty
      fallbacks like the `"Shortcut Guide"` title guard.
