# Shortcut Guide — Regression Catalog & Conventions

Fuller, progressively-disclosed companion to `SKILL.md`. Everything here is grounded in the
module source, the manifest spec, or the mined PR/issue history. Root:
`src/modules/ShortcutGuide/`.

## Architecture at a glance (v0.100 rewrite)

Shortcut Guide was rewritten from the legacy Win-key-hold overlay into a **WinGet-manifest
interpreter**:

- **C++ module interface** (`ShortcutGuideModuleInterface/dllmain.cpp`) owns the hotkey
  (default **Win+Shift+/**, `VK_OEM_2`), the GPO gate, and the process lifecycle. `OnHotkeyEx`
  **toggles** the UI process; `disable`/`destroy` terminate it.
- **WinUI 3 C# app** (`ShortcutGuide.Ui`, `PowerToys.ShortcutGuide.exe`) is launched via
  `ShellExecute` with args `<powertoys_pid> [telemetry]`. It is single-instance
  (`AppInstance.FindOrRegisterForKey`) and force-exits with `Environment.Exit(0)`.
- **Manifests** live per-user at `%LocalAppData%\Microsoft\WinGet\KeyboardShortcuts`. On each
  launch the app copies bundled `Assets/ShortcutGuide/Manifests/*.yml` there, runs
  `IndexYmlGenerator.exe` to (re)build `index.yml`, then `PowerToysShortcutsPopulator.Populate`
  injects enabled PowerToys modules' current hotkeys into `Microsoft.PowerToys.en-US.yml`.
- **Matching:** `ManifestInterpreter.GetAllCurrentApplicationIds` resolves the foreground
  window's process module name and running background processes against each manifest's
  `WindowFilter` (exact `.exe` or `*`), producing the side-nav list.

## Regression catalog

### R1 — Overlay crash on section navigation (#48448, #48441, #48522, #49131 → PR #48481)
Root cause: reentrant `Activate → Window_Activated → BringToFront` left
`App.TaskBarWindow.AppWindow` null; `SetWindowPosition` threw; the exception propagated into the
async init catch which closes the window as "InitializationFailed". Fix: null-guard the taskbar
window, and wrap selection + positioning in logging try/catch so exceptions never tear down the
overlay. Crash logs also showed a follow-up coreclr access violation from escaping exceptions —
hence the broader hardening.

### R2 — Empty-title startup fault (#49131 empty-then-close, launch crashes #48170/#48638 → PR #49069)
`ResourceLoader.GetString` returns `""` (does not throw) when the resource map can't be resolved.
A WinUI `TitleBar` (with `ExtendsContentIntoTitleBar`) reads `AppWindow.Title` during a deferred
layout pass and faults on empty. Fix: guard with a non-empty literal fallback. This is a **class**
of bug across PowerToys WinUI windows, not SG-only.

### R3 — Literal digit keys mis-rendered (#48461 convention → PR #48757)
A bare number in `Keys:` is a virtual-key code (VK `9`=Tab, `1`=left mouse button, `0`=undefined).
Literal digits must be the `<N>` token; the renderer (`KeyVisual`) strips brackets. PR #48757 was
a data-only sweep of 91 keys across 14 manifests.

### R4 — Manifest token authoring (review push-back #48821, #48959, #48960, #48652)
Recurring maintainer corrections on community manifest PRs:
- No WinGet package → prefix filename **and** `PackageName` with `+`.
- Use spec `<...>` tokens for special keys (`<Delete>`, `<Tab>`, `<Space>`, `<Insert>`,
  `<Escape>`, `<PageUp>`, `<PageDown>`); `Back`/bare `Delete`/bare digits are wrong.
- A modifier alone (e.g. `Shift` with `Keys: [Shift]`) is not a usable shortcut.
- Keep PR description section counts in sync with the manifest.

### R5 — Shell label drift on Copilot+ PCs (#48427 → PR #48439)
Win+Q and Win+S both read "Open search"; Win+Q is Click to Do on Copilot+ PCs. Fix in
`+WindowsNT.Shell.en-US.yml` with a clarifying description.

### R6 — resx→rc PowerShell build reliability (#46618 → PR #46729)
`Exec` invoking `convert-resx-to-rc.ps1` failed because PowerShell profile/module-autoload
warnings hit stderr (treated as errors) and unquoted `$(MSBuildThisFileDirectory)` split on
spaces. Fix: `-NoProfile -NonInteractive`, disable module autoload, quote path args. Touches
`ShortcutGuideModuleInterface.vcxproj` among many.

## Open / recurring themes (not yet fixed at capture time)

- Startup latency — "Showing shortcut-guide takes too long" (#49200): manifest copy + external
  index generation + process enumeration are on the launch path.
- Crash opening Settings from the overlay (#49173); crashes on Windows 10 (#48773).
- Home-screen "Shortcuts" widget label ambiguity / spurious conflicts (#49311, #44830, #44141).
- Can't display remaps for "hard-coded" Windows shortcuts like Win+C (#47950).
- Overlay behavior with a moved/vertical taskbar (#48435).

## Manifest authoring conventions (from `doc/specs/WinGet Manifest Keyboard Shortcuts schema.md`)

- **Save location:** `%LocalAppData%/Microsoft/WinGet/KeyboardShortcuts` (per-user).
- **Filename:** `<PackageId>.<locale>.yml` in this repo's bundled assets (spec's canonical
  extension is `.KBSC.yaml`; PowerToys ships `.<locale>.yml`). No WinGet package → leading `+`.
  `+WindowsNT*` reserved for the OS/shell.
- **Fields:** `PackageName`, `WindowFilter` (exact exe or `*`), `BackgroundProcess` (default
  false), `Shortcuts` → `SectionName` + `Properties` → `Name`, optional `Description`,
  `AdditionalInfo`, `Recommended`, and `Shortcut` (array; supports sequential chords) with
  `Win`/`Ctrl`/`Shift`/`Alt` + `Keys`.
- **Keys:** bare number = virtual-key code; literal digit = `<N>`; special keys = `<...>` tokens;
  quote bracketed tokens for consistency (`"<Enter>"`). A range like `1 - 8` is a free-form label,
  not a key.
- **Special sections:** section names in `<...>` (e.g. `<TASKBAR1-9>`) are special displays;
  interpreters that don't understand a special section omit it.
- **Casing:** sentence case for `Name`/`SectionName`.
- **index.yml:** generated locally (`IndexYmlGenerator`), groups manifests by `(WindowFilter,
  BackgroundProcess)`, `DefaultShellName = "+WindowsNT.Shell"`.

## Key decisions / invariants

- **Toggle activation**, single-instance UI, forced `Environment.Exit(0)` (dispatcher won't quit
  cleanly).
- **GPO checked in two places** (module interface + Program.cs).
- **Index cache** keyed on `index.yml` last-write-time (`ManifestInterpreter.GetCachedIndexYamlFile`).
- **PowerToys shortcuts are dynamic**, injected between `# <Populate start>`/`# <Populate end>`
  from live settings — never hand-edit that region.
- **Robust foreground detection**: `GetAllCurrentApplicationIds` swallows access-denied/exited
  process exceptions; excluded windows short-circuit launch
  (`IsCurrentWindowExcludedFromShortcutGuide`).

## Tests

- `ShortcutGuide.UnitTests/ConvertersTests/ShortcutDescriptionToKeysConverterTests.cs` — key
  conversion (VK codes, `<N>`/special tokens, arrows). Add cases here when changing token handling.
