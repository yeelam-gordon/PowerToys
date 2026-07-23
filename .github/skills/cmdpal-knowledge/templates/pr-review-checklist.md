# CmdPal PR Review Checklist

Apply to a PR touching `src/modules/cmdpal/`. **Read the diff cold first** (see anti-anchoring in
SKILL.md), then use this as a checklist for the areas that the diff actually touches.

## Always
- [ ] All new user-facing strings are in `Strings/en-us/Resources.resw` (no inline literals).
- [ ] A test was added/updated in the relevant `Tests/*.UnitTests` project.
- [ ] No StyleCop violations (e.g. member explicitly initialized to its type default).
- [ ] No culture-dependent value used as a routing/navigation key or dictionary key.

## If it touches ShellPage / MainWindow / Compact mode
- [ ] Validated collapsed **and** expanded.
- [ ] Validated list page **and** content page.
- [ ] Validated forward navigation **and** back navigation (search box stays correct — #49116).
- [ ] Validated keyboard (Down/Tab expand) **and** pointer.
- [ ] Input handling / item actions are gated on the **collapsed** state, not just "compact" (#49113).
- [ ] Non-client window frame re-asserted after focus change if frame was customized (`RedrawWindow`, #49184).

## If it touches the Dock
- [ ] Any item meant to open the palette is backed by a **page command** (`IsPageCommand`, #49089).
- [ ] Re-tested across multiple monitors, each dock edge, and monitor power on/off (#49295, #49086).
- [ ] Does not rebuild/reload the whole Dock for unrelated setting changes (#49168).

## If it touches DI / command-provider registration
- [ ] Provider↔root-page cycles broken with a deferred accessor, not an eager ctor dependency.
- [ ] No redundant factory wrapping when the container can inject a registered accessor (PR #49095).
- [ ] New built-in feature is registered as an `ICommandProvider` in `App.xaml.cs::AddBuiltInCommands`.

## If it touches the extension SDK (`extensionsdk/*.idl`)
- [ ] Change is **additive** — new versioned interface, not a modified existing one (`IToastArgs2`).
- [ ] Backward compatible for already-installed out-of-proc extensions (PR #49260).
- [ ] Toolkit + a sample updated to exercise the new surface.

## If it touches an ext/ provider (Apps, PerfMon, Shell, ...)
- [ ] Allow-lists / discovery regexes extended **with** a unit test (Apps: #49236 / PR #49241).
- [ ] Enabled / soft-disabled / hardware-unavailable states handled (PerfMon: #49154, #49163).
