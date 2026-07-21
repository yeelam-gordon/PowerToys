---
name: app-signoff-uia
description: 'Drive any running Windows desktop app through winappcli (winapp ui) UI Automation to discover end-user capabilities and execute a prioritized P0/P1/P2 sign-off / regression suite that verifies features actually work and catches regressions. Use when asked to author or run a UI sign-off, smoke test, acceptance suite, capability check, or regression test for a Windows app (Win32, WinForms, WPF, WinUI 3, Electron); to inspect a live app''s UI tree/selectors; or to gate a release on P0 checks. Keywords: UI automation, UIA, winappcli, winapp ui, sign-off, smoke test, regression, acceptance test, P0 P1 P2, capability, end-user, release gate, Windows app.'
license: Complete terms in LICENSE.txt
---

# App Sign-off via UI Automation (winappcli)

Turn a running Windows desktop app into a **prioritized, executable sign-off suite**.
This skill inspects the REAL app from an end-user's perspective (via `winapp ui`),
distills what it can do into P0/P1/P2 capability checks, and executes those checks
against the live app to verify features work and to catch regressions.

It is **generic** — nothing about any specific app is hardcoded. Every element is
addressed by a winappcli selector discovered at runtime.

## Inputs — Ask If Not Specified

This skill is app-agnostic. Before authoring or running a sign-off you need:

1. **The target app** — which running Windows app to sign off (process name / window / how to launch it). **If the user has not said which app, ASK — do not assume.**
2. **What matters** — the end-user capabilities to gate on (or let `discover` propose them, then confirm priorities with the user).

Do not default to any particular app (the bundled Calculator spec is only an illustrative example).

## When to Use This Skill

- User asks to **sign off**, **smoke test**, or **acceptance/regression test** a
  Windows desktop app's UI
- User wants to **verify a feature actually works** by driving the real app, not a mock
- User wants to **catch UI regressions** across builds with a repeatable suite
- User asks to **discover what an app can do** from the end-user surface (its buttons,
  fields, menus) and turn that into checks
- User wants a **release gate** where all P0 checks must pass
- User needs to **inspect a running app's UI tree / find selectors** to automate it
- Works for Win32, WinForms, WPF, WinUI 3, and Electron apps

Optionally seed discovery with public docs / the app's GitHub to learn intended
features first, then confirm each against the live app.

## Prerequisites

- **winappcli** on PATH (`winapp ui status`), v0.4.0+. Confirm with
  `winapp ui --cli-schema` (dumps the full command structure as JSON).
- **Python 3.9+** for `scripts/signoff.py`.
- The **target app must be running** and visible to UI Automation.

## Core Workflow

The unit of work is a **capability check**: an ordered list of `winapp ui` steps
ending in an `expect` assertion on a read (`get-value` / `get-property` / `wait-for`).
Checks are tagged **P0 / P1 / P2**; **all P0 must pass** or the release gate FAILs.

1. **Locate the app/window.** `winapp ui list-windows` → get the process name and
   HWND. For packaged/UWP apps the real window is owned by `ApplicationFrameHost` —
   target it with `-w <HWND>` (see Gotchas).
2. **Discover capabilities.** Read the live UI tree and generate a starter spec:
   ```
   python scripts/signoff.py discover --window <HWND> --out spec.json
   ```
   This runs `winapp ui inspect -i --json`, applies a no-op/noise filter (drops
   window chrome + disabled/offscreen elements), and emits one P2 stub per
   invokable element.
3. **Author the spec.** Turn stubs into meaningful checks: chain steps (invoke
   buttons, set values), then assert on a read. Assign priorities — keep P0 to the
   truly critical smoke path. Seed from docs/GitHub if available. Schema:
   [references/capability-spec.md](./references/capability-spec.md).
4. **Run the sign-off.**
   ```
   python scripts/signoff.py run --spec spec.json --window <HWND> \
       --report-json report.json --report-md report.md
   ```
   Exit code `0` = gate PASS, `1` = gate FAIL (a P0 check failed), `2` = error.
5. **Iterate.** If a check that should pass fails, fix the selector or assertion
   (re-inspect the live app) and re-run until green. Commit the spec as the app's
   regression suite; re-run it on every build.

## Output: Package the Sign-off as a SKILL

Emit an installable Agent Skill whose CORE is a **declarative Markdown checklist** that an agent
executes by driving **winappcli** — plus any app-specific winappcli business logic that makes those
checks possible. The goal: given (1) the checklist, (2) this skill's app-specific logic, and (3)
how to use winappcli (this parent skill), Copilot knows how to sign the app off — no opaque runner
required.

Produce `<app>-signoff/`:

| File / section | Purpose |
|----------------|---------|
| `signoff-checklist.md` | **The declarative checklist** — the source of truth. One item per capability, grouped **P0/P1/P2**. Each item states, declaratively: **Check** (the capability in plain language), **Drive** (the exact `winapp ui …` steps/selectors to exercise it), **Verify** (the expected read via `winapp ui get-value`/`get-property`/`wait-for` **and a `winapp ui screenshot` of the resulting page/state**). Human-readable; no hidden logic. |
| `assets/screenshots/` | **Baseline screenshots** (`winapp ui screenshot`) of every page, settings pane, and behavior the checklist covers — captured on a known-good build. The sign-off compares against these to catch visual regressions that a property read alone would miss. Without these, the sign-off cannot actually verify UI behavior. |
| `SKILL.md` › `## How to Sign Off` | The procedure: launch/attach the app, work the checklist item by item via winappcli, capture a screenshot per item, record PASS/FAIL, gate on P0. |
| `SKILL.md` › `## App-Specific winappcli Logic` | The SPECIAL business logic needed to drive THIS app via winappcli — non-obvious launch, window recovery, runtime selector resolution, required state setup. This is what lets a generic winappcli user actually sign this app off. |
| `SKILL.md` › `## Coverage & Limits` | Honest: any capability that CANNOT be driven via winappcli in the current environment, and why (still document the winappcli steps for when it can be run). |
| `assets/<app>.spec.json` | OPTIONAL machine-runnable encoding of the checklist for `signoff.py` (deterministic re-runs) — must mirror `signoff-checklist.md` exactly. |
| `LICENSE.txt` | Apache-2.0 |

Rules:
- **Everything is winappcli-driven and declarative.** Do **NOT** bypass the UI with unit-test DLLs,
  reflection drivers, or in-process harnesses — the sign-off must exercise the app the way an end
  user does, through winappcli. If a capability truly cannot be reached via winappcli in the current
  environment, say so in **Coverage & Limits** (don't silently substitute a behavioral test).
- The Markdown checklist is the source of truth; any optional JSON spec must match it exactly.

The result **IS a skill** — drop it into the target repo's `.github/skills/` so an agent auto-loads
it when signing off that app.

## The Runner (`scripts/signoff.py`)

| Mode | Purpose |
|------|---------|
| `discover` | Inspect a running app → emit a starter capability-spec skeleton. |
| `run` | Execute a spec → JSON + Markdown report, grouped by priority, gated on P0. |

Key options (both modes accept `-a/--app`, `-w/--window`, `--winapp`, `--timeout`,
`--retries`):

| Option | Applies to | Description |
|--------|-----------|-------------|
| `--spec <file>` | run | Capability spec JSON. |
| `--report-json` / `--report-md` | run | Write reports (always UTF-8). |
| `--step-pause <s>` | run | Sleep between steps (default 0.15) to reduce UIA flakiness. |
| `--gate-only` | run | Print just `PASS`/`FAIL` to stdout. |
| `--out <file>` | discover | Write skeleton spec (else stdout). |
| `--include-noise` | discover | Keep window chrome / disabled / offscreen elements. |

Run `python scripts/signoff.py --help` (or `run --help` / `discover --help`) for full usage.

## Assertions

Each read step carries an `expect` object; all keys present must hold:
`contains`, `not_contains`, `equals`, `regex`, `exit_code`, `found`, plus `ci`
(case-insensitive, default true). The runner extracts a comparable string from the
winapp JSON (prefers `text`, then `value`/`name`). Full schema and examples:
[references/capability-spec.md](./references/capability-spec.md).

## Gotchas

- **Packaged/UWP apps expose no window under their own process.** `CalculatorApp`
  reports `Window: (none)`; the real window is owned by `ApplicationFrameHost`.
  **Never** rely on `-a <name>` for these — it finds 0 elements. Get the HWND from
  `winapp ui list-windows` and target with `-w <HWND>`.
- **Prefer `automationId` selectors over hash-suffixed slugs.** `num1Button` is
  stable across restarts; `btn-num1button-fec5` may not be. Both work as selectors.
- **`get-value` returns the full accessible name, not a bare value.** Calculator's
  display reads `"Display is 3"`, not `"3"`. Assert with `contains`, not `equals`,
  unless you've confirmed the exact string.
- **Transient `element_not_found` is real.** Verbs fired while the app is
  mid-animation or regaining foreground can fail spuriously. The runner retries
  transient failures (`--retries`, default 2, 0.4 s backoff); raise `--step-pause`
  for stubborn apps. **Never** treat a single transient failure as a real regression
  — re-run first.
- **Emoji/Unicode crash a cp1252 console.** The runner reconfigures stdout/stderr to
  UTF-8; if you print winapp output yourself, do the same or you'll hit
  `'charmap' codec can't encode`.
- **Keep P0 tiny.** P0 is the release gate — put only must-work capabilities there,
  or flaky non-critical checks will block every build.
- **`invoke` vs `click`.** `invoke` uses UIA patterns and works headlessly; `click`
  simulates the mouse and needs the element on-screen. Use `click --double/--right`
  only for elements without InvokePattern (column headers, custom list items).

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `Target app required` | Pass `-a`/`--app` or `-w`/`--window`; run `winapp ui list-windows`. |
| `Found 0 elements` / `Window: (none)` | Packaged app — target the `ApplicationFrameHost` HWND with `-w`. |
| `element_not_found` on a valid selector | App mid-transition; rely on retries, raise `--step-pause`, or re-inspect for the current selector. |
| Assertion fails but display looks right | `get-value` returns `"Display is N"`; use `contains`, not `equals`. |
| `'charmap' codec can't encode` | Console is cp1252; runner forces UTF-8 — update winappcli/Python or set `[Console]::OutputEncoding`. |
| `screenshot` is black over RDP | Add `--capture-screen`; ensure the window is on-screen. |
| Gate FAILs on a flaky non-critical check | Re-tag it P1/P2 so it doesn't block the P0 gate; re-run to rule out a transient. |

## References

- [references/winappcli-recipes.md](./references/winappcli-recipes.md) — `winapp ui`
  verb cheat-sheet, selector/slug workflow, result shapes, gotchas.
- [references/capability-spec.md](./references/capability-spec.md) — capability-spec
  JSON schema, priorities, `expect` assertions, full example.
- [templates/capability-spec.template.json](./templates/capability-spec.template.json)
  — ready-to-edit 5-check P0/P1/P2 example (real Calculator selectors).
- [scripts/signoff.py](./scripts/signoff.py) — the `discover`/`run` CLI (`--help`).
