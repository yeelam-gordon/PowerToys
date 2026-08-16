---
name: shortcutguide-knowledge
description: 'PowerToys Shortcut Guide module knowledge: the WinGet-manifest-based (v0.100+) keyboard-shortcut overlay — feature->file/function map, per-app YAML manifest authoring rules (+ prefix, <N>/special-key tokens, WindowFilter), recurring crash/regression playbooks (overlay crash on section navigation, empty-title startup fault, digit-key mis-render), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/ShortcutGuide — manifests, overlay window, hotkey/process activation, index generation, taskbar-number window, multi-monitor positioning, PowerToys dynamic shortcuts. Keywords: Shortcut Guide, keyboard shortcuts, WinGet manifest, KBSC, overlay, WindowFilter, taskbar shortcuts, WinUI 3, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Shortcut Guide Knowledge

Grounded engineering knowledge for the PowerToys **Shortcut Guide** module — rewritten in
v0.100 into a **WinGet-manifest-driven** overlay. A C++ module interface owns the hotkey and
launches a separate WinUI 3 (C#) process (`PowerToys.ShortcutGuide.exe`) that reads per-app
keyboard-shortcut YAML manifests from `%LocalAppData%\Microsoft\WinGet\KeyboardShortcuts`, then
shows a side-nav overlay of shortcuts for the foreground app, Windows shell, and PowerToys.
Use this to localize code fast, avoid known crash traps, and enforce the manifest & review
conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/ShortcutGuide/` and needing prior art.
- Authoring or reviewing a **per-app shortcut manifest** (`*.en-US.yml`): filename/PackageName,
  key tokens, WindowFilter, sections.
- Fixing/triaging a Shortcut Guide bug: overlay crashes on launch or on section click, opens
  empty and closes, shows the old version, wrong/missing shortcut labels, slow to appear,
  taskbar-number shortcuts missing.
- Reviewing a Shortcut Guide PR against maintainer conventions and regression traps.
- Touching the overlay window positioning, hotkey/process activation, index generation, or the
  PowerToys dynamic-shortcut populator.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see
anti-anchoring below). Root: `src/modules/ShortcutGuide/`.

| Sub-feature | Implementation (file · symbol) |
|---|---|
| Hotkey + process lifecycle (C++ module) | `ShortcutGuideModuleInterface/dllmain.cpp` `ShortcutGuideModule::OnHotkeyEx` (start if inactive, then signal `triggerEvent`), `StartProcess`, `disable` (TerminateProcess) |
| Default hotkey + settings parse | `dllmain.cpp` `ParseSettings` — default **Win+Shift+/** (`MOD_SHIFT\|MOD_WIN` + `VK_OEM_2`); reads `properties.open_shortcutguide` |
| GPO enable/disable gate | `dllmain.cpp` `gpo_policy_enabled_configuration`; **also** re-checked in `Program.cs Main` |
| UI process entry / startup | `ShortcutGuide.Ui/Program.cs` `Main` — arg parse `<pid> [telemetry]`, single-instance `AppInstance.FindOrRegisterForKey`, `IsCurrentWindowExcludedFromShortcutGuide`, `Environment.Exit(0)` |
| Manifest copy + index build (bg thread) | `Program.cs` `CopyAndIndexGenerationThread` — copies bundled `Assets/ShortcutGuide/Manifests/*.yml` to per-user dir, runs `IndexYmlGenerator.exe`, then `PowerToysShortcutsPopulator.Populate` |
| Manifest load + fallback + cache | `Helpers/ManifestInterpreter.cs` `GetShortcutsOfApplication` (`.{lang}.yml`→`.en-US.yml`), `GetCachedIndexYamlFile` (mtime-keyed cache), `PathOfManifestFiles` (`%LocalAppData%\Microsoft\WinGet\KeyboardShortcuts`) |
| Which apps to show (foreground+bg match) | `ManifestInterpreter.GetAllCurrentApplicationIds` — foreground window process module + background processes vs `WindowFilter`; `*` = default shell |
| index.yml generation | `ShortcutGuide.IndexYmlGenerator/IndexYmlGenerator.cs` `CreateIndexYmlFile` — groups by `(WindowFilter, BackgroundProcess)`; `DefaultShellName = "+WindowsNT.Shell"` |
| PowerToys dynamic shortcuts | `Helpers/PowerToysShortcutsPopulator.cs` `Populate` — rewrites `Microsoft.PowerToys.en-US.yml` between `# <Populate start>`/`# <Populate end>` from enabled modules' hotkeys |
| Single overlay host, positioning, and close lifecycle | `ShortcutGuideXAML/OverlayWindow.xaml.cs` `ShowOverlay`, `RepositionToCursorMonitor`, `CloseAnimated`, `UpdateTaskbarPaneLayout` |
| Main shortcut pane and navigation | `ShortcutGuideXAML/Controls/MainPaneControl.xaml.cs` `Open`, `InitializeNavItemsAsync`, `WindowSelector_SelectionChanged`, `Hide` |
| Multi-monitor / DPI / taskbar layout | `OverlayWindow.RepositionToCursorMonitor`, `ApplyMainPaneAlignment`, `UpdateTaskbarPaneLayout`; `Helpers/DisplayHelper.cs`, `Helpers/DpiHelper.cs`, `Helpers/TasklistPositions.cs` |
| Taskbar number-key (`Win+1..9`) pseudo-window | `Controls/TaskbarPaneControl.*`, `Controls/TaskbarIndicator.*`, `Helpers/TasklistPositions.cs`, `TasklistButton.cs` — hosted inside `OverlayWindow`; shown for a selected `<TASKBAR1-9>` section or directly by Win-key `TaskbarIndicators` mode |
| Key rendering (tokens → visual) | `Converters/ShortcutDescriptionToKeysConverter.cs`, `Controls/KeyVisual.xaml.cs` (`<N>`/special-token strip, VK code→name), `Controls/KeyCharPresenter.*` |
| Shortcut list page | `ShortcutGuideXAML/Pages/ShortcutsPage.xaml.cs`; `ViewModels/ShortcutListItem*.cs` |
| Data models | `Models/ShortcutFile.cs`, `ShortcutCategory.cs`, `ShortcutDescription.cs`, `ShortcutEntry.cs`, `IndexFile.cs` |
| Icon extraction for nav items | `Helpers/IconHelper.cs`, `Helpers/NavItemIconHelper.cs` |
| Home-screen "pinned" shortcuts (Settings) | `Helpers/PinnedShortcutsHelper.cs` |
| Manifest data (shipped) | `ShortcutGuide.Ui/Assets/ShortcutGuide/Manifests/*.yml` |
| Manifest spec (authoritative) | `doc/specs/WinGet Manifest Keyboard Shortcuts schema.md` |

**Activation model (critical):** the C++ module keeps one event-driven UI process. `OnHotkeyEx`
starts the process only when inactive, then signals `triggerEvent`; `App.ListenForLaunchedEvents`
shows or hides the persistent `OverlayWindow`. Win-key hold is an active path:
`keep_track_of_pressed_win_key()` returns true, the configured hold duration is exposed by
`milliseconds_win_key_must_be_pressed()`, and `App` selects Off, taskbar-indicators-only, or full
Shortcut Guide behavior from settings. Module disable still terminates the process.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Historical dual-window navigation crash; preserve the single-overlay replacement
- **Historical symptom:** the former `MainWindow` + `TaskbarWindow` design could close while
  switching sidebar sections because reentrant activation temporarily exposed a null taskbar
  `AppWindow`. PR #48481 hardened that removed architecture.
- **Current design:** `OverlayWindow` hosts `MainPaneControl` and `TaskbarPaneControl` in one XAML
  tree and one dispatcher. `MainPaneControl.WindowSelector_SelectionChanged` publishes taskbar
  visibility; `OverlayWindow.UpdateTaskbarPaneLayout` positions the pseudo-window without activating
  a second native window.
- **Guardrail:** do not reintroduce separate native-window activation or taskbar-window ownership.
  Keep initialization failures explicit through `MainPaneControl.InitializationFailed`.
  `TaskbarPaneControl.UpdateTasklistButtons` catches enumeration failure and returns no layout when
  no buttons are available; do not claim broader layout failures are contained without adding
  explicit handling.
  Historical evidence: issues
  [#48448](https://github.com/microsoft/PowerToys/issues/48448),
  [#48441](https://github.com/microsoft/PowerToys/issues/48441),
  [#48522](https://github.com/microsoft/PowerToys/issues/48522); fix
  [PR #48481](https://github.com/microsoft/PowerToys/pull/48481).

### Empty native window title faults the process at startup
- **Symptom:** process crashes on launch (0xc0000005 access violations, "V2 crashes on launch").
- **Where:** `OverlayWindow` constructor `Title` assignment (any WinUI window using
  `ExtendsContentIntoTitleBar`).
- **Root cause:** `ResourceLoader.GetString("Title")` returns an **empty string** (it does not
  throw) when the resource map can't be resolved; the WinUI `TitleBar` control reads the empty
  `AppWindow.Title` during a deferred layout pass and faults.
- **Guardrail:** never assign an empty native title — fall back to a non-empty literal
  (`"Shortcut Guide"`) when the resolved string is null/empty. Evidence:
  [PR #49069](https://github.com/microsoft/PowerToys/pull/49069) and current source. Reports #48170
  and #48638 belong to different manifest/overlay-rewrite paths and are not title-fallback evidence.

### Literal digit keys render as the wrong key
- **Symptom:** a number-key shortcut (e.g. "switch to last tab", `9`) renders wrong or blank.
- **Where:** manifest `Keys:` arrays; interpreted by `ShortcutDescriptionToKeysConverter` /
  `KeyVisual.xaml.cs`.
- **Root cause:** per spec a **bare number is a virtual-key code**, not a character — VK `9` is
  Tab, VK `1` is the left mouse button, VK `0` is undefined.
- **Guardrail:** author a literal digit as the `<N>` token (e.g. `"<9>"`); the renderer strips the
  brackets to show the digit. Evidence: fix
  [PR #48757](https://github.com/microsoft/PowerToys/pull/48757) (91 keys across 14 manifests);
  convention introduced in #48461.

### Manifest key-token authoring errors
- **Symptom:** special keys don't render as intended (bare `Delete`, `Back`, `0`, or a
  modifier-only "shortcut").
- **Where:** bundled manifest YAML under `Assets/ShortcutGuide/Manifests/`.
- **Root cause:** keys not authored with the spec's `<...>` tokens; a modifier alone
  (`Shift` with empty `Keys`) is not a usable shortcut.
- **Guardrail:** use spec tokens — `<Delete>`, `<Tab>`, `<Space>`, `<Insert>`, `<Escape>`,
  `<PageUp>`, `<PageDown>`, `<Enter>`, arrows, `<N>`; every shortcut needs a real key, not just a
  modifier. Evidence: review push-back on PRs
  [#48821](https://github.com/microsoft/PowerToys/pull/48821),
  [#48959](https://github.com/microsoft/PowerToys/pull/48959),
  [#48960](https://github.com/microsoft/PowerToys/pull/48960),
  [#48652](https://github.com/microsoft/PowerToys/pull/48652).

### Wrong shortcut label (Copilot+ PC / OS drift)
- **Symptom:** Win+Q shows "Open search" instead of "Open Click to Do" on Copilot+ PCs
  (both Win+Q and Win+S were labeled "Open search").
- **Where:** shell manifest `+WindowsNT.Shell.en-US.yml`.
- **Root cause:** Windows reassigns shell shortcuts by SKU/hardware; static manifest text drifts.
- **Guardrail:** disambiguate duplicated labels and add `AdditionalInfo`/description for
  conditional shortcuts. Evidence: issue
  [#48427](https://github.com/microsoft/PowerToys/issues/48427); fix
  [PR #48439](https://github.com/microsoft/PowerToys/pull/48439).

### resx→rc localization build breaks (module-interface vcxproj)
- **Symptom:** localization/resource build fails or hides warnings on paths with spaces.
- **Where:** `ShortcutGuideModuleInterface/*.vcxproj` (and repo-wide) `Exec` invoking
  `convert-resx-to-rc.ps1`.
- **Root cause:** PowerShell profile/module auto-load writes warnings to stderr which `Exec`
  treats as errors; unquoted `$(MSBuildThisFileDirectory)` splits on spaces.
- **Guardrail:** invoke with `-NoProfile -NonInteractive`, disable module auto-loading rather than
  globally silencing warnings, and quote path args. Evidence: issue
  [#46618](https://github.com/microsoft/PowerToys/issues/46618); fix
  [PR #46729](https://github.com/microsoft/PowerToys/pull/46729).

### Manifest refresh leaves a stale or partially generated index
- **Known current violation:** `CopyAndIndexGenerationThread` logs a bundled-manifest copy exception
  but continues into index generation. A partial copy can therefore publish an index assembled from
  mixed old/new manifest files. Treat the fail-fast copy requirement below as an existing defect,
  not current behavior.
- **Symptom:** the overlay opens empty, shows an old application list, or omits newly shipped
  shortcuts after a manifest update.
- **Where:** `Program.cs::CopyAndIndexGenerationThread` and
  `ManifestInterpreter.GetCachedIndexYamlFile`.
- **Root cause:** launch performs an ordered external pipeline: copy bundled manifests, run
  `IndexYmlGenerator.exe`, then populate dynamic PowerToys shortcuts. Continuing after a copy/start/
  non-zero-exit failure can consume a stale or partial `index.yml`. The generator deletes the
  existing index before parsing, so malformed input can also leave no prior index to fall back to;
  the interpreter caches the deserialized `IndexFile`, keyed by `index.yml` last-write time.
- **Guardrail:** preserve copy → successful index generation → populate ordering. Treat
  `Process.Start`, wait, and exit-code failures as terminal for that refresh and log the failed
  stage. Generate to a temporary file and atomically replace the accepted index, or otherwise
  preserve/restore the previous valid index on failure. Invalidate/re-read the cache whenever the
  accepted `index.yml` changes; validate every shipped YAML file before merge so one corrupt
  manifest cannot poison the launch pipeline.
  This playbook is grounded in the current pipeline and cache implementation; do not attribute
  unrelated navigation/positioning crash reports to manifest corruption.

## Review Rules

Enforce these when reviewing or authoring Shortcut Guide changes:

- **Manifest filename == PackageName == WinGet package id.** If the app has **no** WinGet package,
  prefix **both** the filename and `PackageName` with `+` (e.g. `+Godot.Godot`,
  `+Microsoft.OutlookForWindows`, `+WindowsNT.Notepad`). `+WindowsNT` is **reserved for the OS**.
  ([spec §2.3](https://github.com/microsoft/PowerToys/blob/main/doc/specs/WinGet%20Manifest%20Keyboard%20Shortcuts%20schema.md); PRs
  [#48821](https://github.com/microsoft/PowerToys/pull/48821),
  [#48959](https://github.com/microsoft/PowerToys/pull/48959)).
- **Use spec key tokens, never bare specials or bare digits.** Literal digit → `<N>`; special keys
  → `<Delete>` etc.; no modifier-only shortcuts (#48757, #48652).
- **Sentence case** for `Name` and `SectionName` — capitalize first word + proper/feature nouns
  only (spec §3.1).
- **`WindowFilter` supports only an exact exe name or `*`** — no other wildcard patterns; matching
  strips `.exe` and is case-insensitive (`ManifestInterpreter.GetAllCurrentApplicationIds`, spec).
- **Preserve the single-overlay architecture.** Keep `MainPaneControl` and `TaskbarPaneControl`
  hosted in `OverlayWindow`; do not restore cross-window activation or `App.TaskBarWindow`
  ownership. Taskbar enumeration/layout failure should hide the taskbar pane, while app-list
  initialization failure is surfaced through `InitializationFailed` (#48481 is historical evidence).
- **Never assign an empty native `Title`** to a WinUI window with `ExtendsContentIntoTitleBar`;
  `ResourceLoader.GetString` returns `""` (not an exception) on failure — guard it (#49069).
- **Treat manifest refresh as a transaction.** When a diff changes copy/index/populate behavior,
  do not populate or publish a new index after copy, child-process start, wait, YAML parse, or
  non-zero-exit failure. **Current source continues after a copy exception**; do not file that
  existing defect against an unrelated edit unless the diff introduces, worsens, or claims to
  fix it. Refresh the mtime-keyed cache only from the accepted `index.yml`.
- **Validate YAML before merge.** Runtime index generation is not an adequate validator because one
  malformed shipped manifest can invalidate the shared generated index.
- **New manifest = add spellcheck words** to `.github/actions/spell-check/expect.txt` (raised on
  #48652), and keep the `<N>`/special-key convention.
- **Ship a test with logic changes.** Suite: `ShortcutGuide.UnitTests`
  (`ConvertersTests/ShortcutDescriptionToKeysConverterTests.cs`).

## Pitfalls

- **The hotkey signals a persistent process.** `OnHotkeyEx` starts the process only when needed and
  always signals `triggerEvent`; `App` toggles the overlay visibility. Process termination belongs
  to module disable or actual window closure, not each hotkey toggle.
- **Manifests are copied to the per-user dir on every launch** (`%LocalAppData%\Microsoft\WinGet\
  KeyboardShortcuts`) and the index is rebuilt by a **separate exe** (`IndexYmlGenerator.exe`) on
  a background thread. A **single corrupt `.yml`** makes index generation exit non-zero and the
  shared index cannot be refreshed. Validate YAML before shipping a manifest; do not attribute
  unrelated empty-close reports to corrupt YAML without diagnostics.
- **The index cache is keyed on file modification time** (`GetCachedIndexYamlFile`); an unchanged
  mtime serves the cached, possibly stale, index within a session.
- **Taskbar indicators have two entry paths.** A selected app exposes them through a
  `<TASKBAR1-9>` section; Win-key `TaskbarIndicators` mode opens the taskbar pane directly. Do not
  require the manifest token for the settings-driven taskbar-only path (#44474).
- **`Environment.Exit(0)` is deliberate** — the WinUI/WinRT dispatcher thread doesn't quit
  cleanly; don't "fix" it into a graceful shutdown without verifying the process actually exits.
- **GPO is checked twice** (C++ `gpo_policy_enabled_configuration` **and** `Program.cs`); a
  policy-disabled state must short-circuit both.
- **Elevated / access-denied foreground processes** can't be inspected for their module name —
  `GetAllCurrentApplicationIds` swallows `Win32Exception`/`InvalidOperationException`; don't
  assume a match always resolves an executable path (icon extraction may be skipped).
- **`PowerToysShortcutsPopulator` rewrites `Microsoft.PowerToys.en-US.yml` in place** between the
  `# <Populate start>`/`# <Populate end>` markers — don't hand-edit that region; add per-module
  hotkeys through the populator so enabled/disabled state is respected.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + manifest conventions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Shortcut Guide PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/ShortcutGuide/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/ShortcutGuide)
- Manifest spec: [`doc/specs/WinGet Manifest Keyboard Shortcuts schema.md`](https://github.com/microsoft/PowerToys/blob/main/doc/specs/WinGet%20Manifest%20Keyboard%20Shortcuts%20schema.md)
- [Virtual-Key Codes](https://learn.microsoft.com/windows/win32/inputdev/virtual-key-codes) · [WinGet manifest schema](https://github.com/microsoft/winget-pkgs/blob/master/doc/manifest/README.md)
