---
name: cmdpal-knowledge
description: 'PowerToys Command Palette (CmdPal) module knowledge: feature->file/function map across the WinUI 3 host (Microsoft.CmdPal.UI / .ViewModels), the Dock, Toast notifications, Compact/collapsed mode, Settings navigation, the extension SDK, and the built-in ext/* command providers (Apps, Shell, Indexer, PerfMon, WindowWalker, TimeDate...). Recurring regression playbooks (compact/collapsed-mode interactions, Dock page-command semantics, DI circular dependencies, localized-string-as-identifier, multi-monitor Dock, PerfMon soft-disable), maintainer review rules, and Pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/cmdpal. Keywords: CmdPal, Command Palette, launcher, Dock, toast, compact mode, ShellPage, command provider, extension SDK, WinUI 3, DI, breadcrumb, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Command Palette (CmdPal) Knowledge

Grounded engineering knowledge for the PowerToys **Command Palette** module (`src/modules/cmdpal/`)
— a WinUI 3 keyboard launcher with a host app, a pin-capable **Dock**, **toast** notifications, a
**Compact/collapsed** display mode, a **Settings** window, an out-of-proc **extension SDK**, and a
large set of first-party command providers under `ext/`. Use it to localize code fast, avoid known
regression traps, and enforce the conventions that the maintainers already established.

> **Data honesty:** this file is distilled from a **lean, recent slice** of history (a handful of
> merged PRs from July 2026 plus their review threads, and the current open/closed bug list). The
> review-comment corpus is thin, so the highest-confidence signal is the **compact-mode / Dock**
> regression cluster and the DI/localization lessons below. Treat every map row as a **hypothesis to
> confirm in source** (see anti-anchoring). Where a claim is thin, it is flagged.

## When to Use This Skill

- Planning or implementing a change under `src/modules/cmdpal/` and needing prior art.
- Fixing/triaging a CmdPal bug: Dock not opening/positioning, palette not summoning, actions firing
  while collapsed, search box vanishing on back-nav, breadcrumbs broken in non-English UI, PerfMon
  crash on enable, apps/shortcuts missing from results.
- Reviewing a CmdPal PR and checking it against maintainer conventions and regression traps.
- Adding a built-in command provider (`ext/Microsoft.CmdPal.Ext.*`) or touching the extension SDK
  (`extensionsdk/`), Dock, Toast, Compact mode, or Settings navigation.

## Module Map (feature -> file/function)

Localization aid. Confirm in source before acting; the local snapshot may lag `main` (some files
below were added by the July 2026 PRs cited).

| Sub-feature | Implementation (project · file · symbol) |
|---|---|
| App bootstrap + **built-in command-provider registration (DI)** | `Microsoft.CmdPal.UI/App.xaml.cs` `AddBuiltInCommands` (registers every `ICommandProvider`; `IRootPageService` → `PowerToysRootPageService`) |
| Root page / home service | `Microsoft.CmdPal.UI.ViewModels/IRootPageService.cs`; `Microsoft.CmdPal.UI/PowerToysRootPageService.cs`. Deferred-accessor split — `Microsoft.CmdPal.UI.ViewModels/IRootPageAccessor.cs`, `Microsoft.CmdPal.UI/DeferredRootPageAccessor.cs` (added by [PR #49095](https://github.com/microsoft/PowerToys/pull/49095)) |
| Built-in utility commands and dock-home band | `Microsoft.CmdPal.UI.ViewModels/Commands/BuiltInsCommandProvider.cs` (`IRootPageAccessor`, `GetDockBands`); toolkit `WrappedDockItem` |
| Main host window + HWND frame (compact) | `Microsoft.CmdPal.UI/MainWindow.xaml.cs`, `NativeMethods.txt` (P/Invoke incl. `RedrawWindow`) |
| **ShellPage** — search box, key handling, **Compact/collapsed mode** | `Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs` `UpdateCompactModeForCurrentPage`, `HandleExpandCompactOnUiThread`, `ShellPage_OnPreviewKeyDown`, `Receive(ExpandCompactModeMessage)`; XAML `Pages/ShellPage.xaml` |
| Compact-mode setting + toast-position setting | `Microsoft.CmdPal.UI.ViewModels/SettingsModel.cs` (`CompactMode`), `SettingsViewModel.cs` |
| Search bar control + context menu | `Microsoft.CmdPal.UI/Controls/SearchBar.xaml(.cs)` |
| XAML bind transformers (visibility/converters) | `Microsoft.CmdPal.UI/Helpers/BindTransformers.cs`; `Converters/` |
| **Dock** (pin-capable band host) | `Microsoft.CmdPal.UI/Dock/DockControl.xaml.cs` `InvokeItem`, `IsPageCommand`; `DockWindow*.cs`, `DockWindowManager.cs`, `PinToDockDialogContent.xaml.cs` |
| Dock band view-models | `Microsoft.CmdPal.UI.ViewModels/Dock/` |
| **Toast** notification window | `Microsoft.CmdPal.UI/ToastWindow.xaml(.cs)` `PositionWindow`; VM `Microsoft.CmdPal.UI.ViewModels/ToastViewModel.cs`, `Messages/ShowToastMessage.cs` |
| Command dispatch pipeline | `Microsoft.CmdPal.UI.ViewModels/Messages/PerformCommandMessage.cs`, `ShellViewModel.cs` |
| **Settings window + breadcrumb navigation** | `Microsoft.CmdPal.UI/Settings/SettingsWindow.xaml.cs` `Navigate`, `BreadCrumbs`; pages under `Settings/` |
| Theme / monitor services | `Microsoft.CmdPal.UI/Services/ThemeService.cs`, `MonitorService.cs`, `WindowThemeSynchronizer.cs` |
| Extension host / caching / state | `Microsoft.CmdPal.UI.ViewModels/Services/` `WinRTExtensionService.cs`, `DefaultCommandProviderCache.cs`, `SettingsService.cs`, `PersistenceService.cs` |
| **Extension SDK** (WinRT contract + toolkit) | `extensionsdk/Microsoft.CommandPalette.Extensions/*.idl`; `extensionsdk/Microsoft.CommandPalette.Extensions.Toolkit/` (`ToastArgs.cs`, `CommandResult.cs`) |
| Built-in providers | `ext/Microsoft.CmdPal.Ext.*` — Apps, Shell, Calc, Indexer, PerformanceMonitor, WindowWalker, WebSearch, ClipboardHistory, TimeDate, WindowsSettings/Services/Terminal, Registry, System, RemoteDesktop, WinGet, Bookmark |
| Apps provider — Win32/UWP/**internet-shortcut** discovery | `ext/Microsoft.CmdPal.Ext.Apps/Programs/Win32Program.cs` (`InternetShortcutURLPrefixes` allow-list regex, `InternetShortcutProgram`) |
| Keyboard activation (global hotkey) | `CmdPalKeyboardService/`, `CmdPalModuleInterface/` |
| Tests | `Tests/*.UnitTests` (per-ext + `Microsoft.CmdPal.UI.ViewModels.UnitTests`, `Microsoft.CmdPal.UITests`) |

**Architecture note:** first-party features are just `ICommandProvider`s registered in
`App.xaml.cs::AddBuiltInCommands`, the same contract third-party extensions implement via the SDK.
Extensions run **out-of-process** over the WinRT `Microsoft.CommandPalette.Extensions` interfaces;
new API surface must be added **additively** (versioned interfaces, e.g. `IToastArgs` →
`IToastArgs2`, backward compatible both ways — PR #49260).

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Compact/collapsed mode interaction bugs (hottest cluster)
- **Symptom:** in the newer Compact display mode, several distinct breakages — actions fire on a
  hidden selected item while collapsed; the search box disappears when navigating **back** from a
  Content page to a list; no keyboard way to expand; a separator/border stays visible when collapsed;
  the HWND frame reappears after focus loss.
- **Where:** `Pages/ShellPage.xaml.cs` (`UpdateCompactModeForCurrentPage`, `HandleExpandCompactOnUiThread`,
  `ShellPage_OnPreviewKeyDown`); `MainWindow.xaml.cs` (frame paint); `ShellPage.xaml` +
  `Helpers/BindTransformers.cs` (conditional visibility).
- **Root cause:** collapsed mode hides UI but state/handlers still behaved as if expanded; DWM
  repaints the non-client frame after focus change; visibility bindings didn't account for collapsed.
- **Guardrail:** for **any** ShellPage/MainWindow change, test the full compact matrix — collapsed vs
  expanded × list vs content page × forward-nav vs back-nav × keyboard (Down/Tab) vs pointer. Gate
  input handling and visibility on the *collapsed* state, not just "compact". Evidence:
  actions-while-collapsed [#49113](https://github.com/microsoft/PowerToys/issues/49113) →
  [PR #49182](https://github.com/microsoft/PowerToys/pull/49182); back-nav search box
  [#49116](https://github.com/microsoft/PowerToys/issues/49116); expand shortcuts
  [PR #49177](https://github.com/microsoft/PowerToys/pull/49177); frame repaint
  [PR #49184](https://github.com/microsoft/PowerToys/pull/49184) (`RedrawWindow`); separator
  [#49312](https://github.com/microsoft/PowerToys/issues/49312) → [PR #49313](https://github.com/microsoft/PowerToys/pull/49313);
  open follow-up [#49283](https://github.com/microsoft/PowerToys/issues/49283). Compact defaults **off**
  ([PR #49186](https://github.com/microsoft/PowerToys/pull/49186)).

### Dock "page command" summon semantics
- **Symptom:** the Dock's **Open Command Palette** button (or a pinned item) stops summoning the
  palette after a refactor.
- **Where:** `Dock/DockControl.xaml.cs` `InvokeItem` → `IsPageCommand`.
- **Root cause:** the Dock only shows the palette for **page commands**; `IsPageCommand` returns
  false for a plain invokable command returning `CommandResult.GoHome()`, so a home/open item wired
  as a non-page command silently no-ops the summon. The former `GoHomeDockCommand` was the historical
  example; current built-ins expose the root page through `IRootPageAccessor` and `WrappedDockItem`.
- **Guardrail:** when a Dock item must open the palette, back it with a **page command** (root page)
  or add an explicit summon path — don't assume an invokable command surfaces the window. Evidence:
  [#49089](https://github.com/microsoft/PowerToys/issues/49089) → [PR #49095](https://github.com/microsoft/PowerToys/pull/49095).

### DI circular dependency (command providers ↔ root page)
- **Symptom:** startup DI resolution fails or is refactored around a cycle between
  `BuiltInsCommandProvider` and `IRootPageService`.
- **Where:** `App.xaml.cs::AddBuiltInCommands`; `BuiltInsCommandProvider.cs`;
  `Microsoft.CmdPal.UI.ViewModels/IRootPageAccessor.cs`,
  `Microsoft.CmdPal.UI/DeferredRootPageAccessor.cs` (the deferred-accessor pair, added by
  [PR #49095](https://github.com/microsoft/PowerToys/pull/49095); the DI seam is
  `IRootPageService.cs` → `PowerToysRootPageService.cs`).
- **Root cause:** a provider needed the root page *only* to open the palette, creating a cycle with
  the service that builds providers.
- **Guardrail:** break provider→root-page cycles with a **deferred accessor** (`IRootPageAccessor` /
  `DeferredRootPageAccessor` — a lazy `Func<>` resolved at call time), not an eager constructor
  dependency. If you register the accessor in DI, let the container inject it rather than
  double-wrapping the factory (redundant registration was called out in review). Evidence:
  [PR #49095](https://github.com/microsoft/PowerToys/pull/49095#discussion_r3538570672).

### Localized string used as a navigation identifier (i18n)
- **Symptom:** Settings breadcrumbs / page navigation break in **non-English** locales.
- **Where:** `Settings/SettingsWindow.xaml.cs` `Navigate`, `BreadCrumbs`, `Crumb.Data`.
- **Root cause:** localized page **titles** leaked in as navigation keys, so the `Navigate()` switch
  didn't match once the UI language changed.
- **Guardrail:** navigation tags / keys must be **culture-invariant constants**, never localized
  display strings. Keep the user-visible title separate from the routing identifier. This is a
  generic i18n rule ([globalization: don't key on localized text](https://learn.microsoft.com/en-us/globalization/localizability/develop-world-ready-apps));
  it bites here in Settings routing. Evidence:
  [#45855](https://github.com/microsoft/PowerToys/issues/45855) → [PR #49253](https://github.com/microsoft/PowerToys/pull/49253).

### Apps provider — internet-shortcut allow-list drift
- **Symptom:** launcher/store shortcuts (Origin/EA, UPlay, Xbox) don't appear as apps in results.
- **Where:** `ext/Microsoft.CmdPal.Ext.Apps/Programs/Win32Program.cs` `InternetShortcutURLPrefixes`
  regex + `InternetShortcutProgram`.
- **Root cause:** `.url` internet shortcuts are only surfaced when their target protocol matches a
  hard-coded allow-list (originally just `steam://` and `com.epicgames.launcher://`).
- **Guardrail:** adding a store means extending the allow-list regex **and** adding a unit test in
  `Tests/Microsoft.CmdPal.Ext.Apps.UnitTests`; prefer a `[GeneratedRegex]` source-generated pattern
  for readability. Evidence: [#49236](https://github.com/microsoft/PowerToys/issues/49236) →
  [PR #49241](https://github.com/microsoft/PowerToys/pull/49241).

### Dock (and host) rebuild on every *unrelated* settings change — record reference-equality
- **Symptom:** editing any setting — theme, an alias, an unrelated provider — tore down and recreated
  the Dock windows, re-registered hotkeys, and rebuilt the backdrop (visible churn/flicker), even
  though nothing the Dock consumes had changed.
- **Where:** `Microsoft.CmdPal.UI.ViewModels/Settings/DockSettings.cs` (the `record`) +
  `Microsoft.CmdPal.UI.ViewModels/Settings/EquatableList`1.cs`; guards in
  `ViewModels/Dock/DockViewModel.cs::UpdateSettings`, `UI/Dock/DockWindow.xaml.cs::SettingsChangedHandler`,
  `UI/Dock/DockWindowManager.cs::OnSettingsChanged`, `UI/MainWindow.xaml.cs` (`_lastAppliedSettings`,
  `MainWindowSettingsComparer`).
- **Root cause:** the `DockSettings` record held `ImmutableList<DockBandSettings>` fields, and
  `ImmutableList<T>` implements **reference** equality. After settings reloaded from disk the rebuilt
  list was a fresh instance, so the record compared **unequal** even for identical content — firing
  every subscriber's full hot-reload path on every `SettingsChanged`.
- **Guardrail:** back record collection properties with a **structural-equality** wrapper
  (`EquatableList<T>`) so record `==` compares by content, then guard each expensive settings-changed
  handler with a value check (`if (_settings == args.DockSettings) return;`). Add equality unit tests
  (`Tests/…UnitTests/DockSettingsEqualityTests.cs`, `EquatableListTests.cs`). **Note the
  `SettingsModel` "LOAD BEARING" comment** — a subscriber that reacts selectively must be updated when
  a new live-reacting setting is added. Evidence:
  [#49168](https://github.com/microsoft/PowerToys/issues/49168) →
  [PR #49171](https://github.com/microsoft/PowerToys/pull/49171).

### Dock stability / multi-monitor / lifecycle (open cluster — verify in source)
- **Symptom:** open Dock bugs — wrong monitor reported as display 1, offset when the palette is opened
  from the Dock, "Pin to Dock" pins to a hidden Dock, Dock disappears after a monitor is powered off,
  frequent crashes.
- **Where:** `Dock/DockWindow*.cs`, `DockWindowManager.cs`, `Services/MonitorService.cs`.
- **Root cause:** not yet distilled (still open). **Do not force-fit a fix location** — reason from the
  symptom and confirm in source.
- **Guardrail:** treat monitor geometry, DPI, and taskbar/edge state as first-class inputs; re-test
  Dock across multiple monitors, power events, and each edge. Evidence (open):
  [#49295](https://github.com/microsoft/PowerToys/issues/49295), [#49264](https://github.com/microsoft/PowerToys/issues/49264),
  [#49205](https://github.com/microsoft/PowerToys/issues/49205), [#49086](https://github.com/microsoft/PowerToys/issues/49086),
  [#49281](https://github.com/microsoft/PowerToys/issues/49281).

### PerformanceMonitor — soft-disabled single-metric Dock bands vanish
- **Symptom:** after PerfMon soft-disables itself (it disables after repeated startup crashes), the
  single-metric Dock bands (CPU / Memory / Network / GPU) disappeared entirely instead of showing a
  disabled placeholder, so the user couldn't tell why their bands were gone.
- **Where:** `ext/Microsoft.CmdPal.Ext.PerformanceMonitor/PerformanceMonitorCommandsProvider.cs`
  (`BandMetrics` array, `SetDisabledState`); `PerformanceMonitorDisabledPage.cs` (now takes a per-band
  `id`); `PerformanceWidgetsPage.cs::GetBandId` (stable band ids).
- **Root cause:** the soft-disabled path emitted only the single aggregate disabled page, so each
  per-metric Dock band had no placeholder to render.
- **Guardrail:** when soft-disabling, emit a **matching disabled placeholder band for each
  single-metric band** with the same stable id (`GetBandId`), so the band persists and tells the user
  it's disabled. Evidence: [#49159](https://github.com/microsoft/PowerToys/issues/49159) →
  [PR #49162](https://github.com/microsoft/PowerToys/pull/49162). (The **battery** Dock band is still
  missing — slated to move to a separate extension; [#49163](https://github.com/microsoft/PowerToys/issues/49163) open.)

### PerformanceMonitor — enable crash / battery / units (open — verify in source)
- **Symptom:** enabling Performance Monitor immediately crashes CmdPal; battery indicator
  unavailable; Bytes vs Binary-bytes units render identically.
- **Where:** `ext/Microsoft.CmdPal.Ext.PerformanceMonitor/`.
- **Root cause:** not yet distilled (open).
- **Guardrail:** exercise the enabled / soft-disabled / hardware-unavailable states and verify unit
  formatting when touching PerfMon or its Dock bands. Evidence (open):
  [#49154](https://github.com/microsoft/PowerToys/issues/49154),
  [#49163](https://github.com/microsoft/PowerToys/issues/49163),
  [#49071](https://github.com/microsoft/PowerToys/issues/49071).

## Review Rules

Enforce these when reviewing or authoring CmdPal changes:

- **Test the compact matrix.** Any `ShellPage`/`MainWindow` change must be validated collapsed **and**
  expanded, list **and** content pages, forward **and** back navigation, keyboard **and** pointer.
  This is the module's #1 regression source (#49113, #49116, #49312).
- **Dock summon = page command.** A Dock item that should open the palette must be a page command (or
  add an explicit summon); `IsPageCommand` gates it (PR #49095).
- **Break DI cycles with a deferred accessor**, not an eager ctor dependency; don't double-wrap a
  factory you already registered ([PR #49095](https://github.com/microsoft/PowerToys/pull/49095#discussion_r3538570672)).
- **Never key navigation/routing on localized strings.** Use invariant constants for tags/keys; keep
  titles separate (PR #49253).
- **Evolve the extension SDK additively.** New WinRT interface members break out-of-proc extensions —
  add a new versioned interface (`IToastArgs2`) instead of changing an existing one; keep it
  backward compatible both directions (PR #49260). Changes live in
  `extensionsdk/Microsoft.CommandPalette.Extensions/*.idl`.
- **New built-in feature = new `ICommandProvider`** registered in `App.xaml.cs::AddBuiltInCommands`;
  follow the existing provider pattern rather than special-casing the host.
- **Ship a test with each ext/ fix.** Per-extension suites exist under `Tests/*.UnitTests`
  (Apps, Shell, Calc, Indexer, WindowWalker, TimeDate, ...). Extend the matching one.
- **All user-facing strings localizable.** New UI text goes in `Strings/en-us/Resources.resw`, not
  inline literals (standing PR-checklist item; enforced across CmdPal UI PRs).
- **Respect StyleCop.** Don't explicitly initialize a member to its type default (`= false`/`= null`)
  — StyleCop (SA1101/SA1642-family defaults) flags it ([PR #49186](https://github.com/microsoft/PowerToys/pull/49186#discussion_r3539014488)).

## Pitfalls

- **Never** assume a Dock invokable command shows the palette — only **page commands** summon it
  (`DockControl.IsPageCommand`); `CommandResult.GoHome()` alone won't (#49089).
- **Never** let collapsed compact mode keep firing item actions or navigation — the user can't see
  the selection; gate on the collapsed state (#49113).
- **Never** use a localized title as a navigation key — it silently breaks routing in other locales
  (#45855).
- **Compact defaults to OFF.** `SettingsModel.CompactMode` shipped `true` by mistake once; the
  intended default is `false` (PR #49186). Local source may still show `true` if it lags `main`.
- **DWM will repaint the non-client frame** after focus changes — a one-time frame tweak isn't
  enough; re-assert with `RedrawWindow` (PR #49184). User32 window-frame hacks are fragile; re-test
  focus-in/out.
- **The Dock used to rebuild on *every* settings change** — the `DockSettings` record held
  `ImmutableList` fields (reference equality), so it compared unequal after a settings reload and
  re-ran every subscriber's hot-reload. Guard settings-changed handlers with a value check and back
  record collection fields with `EquatableList<T>` (fixed in
  [PR #49171](https://github.com/microsoft/PowerToys/pull/49171)); still avoid heavy work in
  settings-changed paths (#49168).
- **Extensions are out-of-proc over WinRT** — a breaking `.idl` change silently breaks installed
  third-party extensions; version additively (PR #49260).
- **Soft-disabled vs hard-disabled PerfMon are different states** — Dock bands and battery indicator
  must handle the soft-disabled/hardware-unavailable cases, not just enabled/disabled (#49163, #49154).

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression + issue list, key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a CmdPal PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/cmdpal/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/cmdpal)
- [Command Palette docs](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/overview) ·
  [Extension SDK spec](https://github.com/microsoft/PowerToys/blob/main/src/modules/cmdpal/doc/initial-sdk-spec/initial-sdk-spec.md) ·
  [RedrawWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-redrawwindow)
