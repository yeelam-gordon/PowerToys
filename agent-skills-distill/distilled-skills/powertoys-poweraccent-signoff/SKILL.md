---
name: powertoys-poweraccent-signoff
description: 'Executable P0/P1/P2 sign-off / regression suite for the PowerToys PowerAccent (Quick Accent) module, proven on freshly-built binaries. Verifies overlay candidate data, EXACT accent glyph sets (French/currency/all-languages), native↔managed WinRT LetterKey enum lockstep, DPI/multi-monitor/caret overlay positioning, and launch/enable/single-instance/clean-exit lifecycle via three real executors (module MSTest DLLs, a reflection GlyphDriver over PowerAccent.Common.dll, and the real PowerToys.PowerAccent.exe). Use to sign off, smoke test, acceptance/regression test, or gate a release of Quick Accent, or to catch a removed/reordered glyph, enum drift, or positioning regression. NOTE: the live overlay-summon path (hold letter + activation key) is BLOCKED under RDP by synthetic-input denial and is NOT covered — run on an input-owning console session for that. Keywords: PowerAccent, Quick Accent, sign-off, regression, P0 P1 P2, accent picker, glyph set, enum lockstep, DPI positioning, single-instance mutex, vstest.'
license: Complete terms in LICENSE.txt
---

# PowerAccent (Quick Accent) Sign-off

A **prioritized, executable P0/P1/P2 sign-off suite** for the PowerToys **PowerAccent
(Quick Accent)** module, run against **REAL freshly-built binaries**. It verifies the
behavioral + lifecycle surface that *feeds and hosts* the accent-picker overlay, and
catches removed/reordered glyphs, native↔managed enum drift, positioning regressions,
and lifecycle breakage. Proven: **20/20 GREEN stable across 4 runs, 5/5 injected
regressions caught, 0 false positives.**

This is the module-specific counterpart to the generic `app-signoff-uia` skill: same
report contract (`build_report`/`report_to_markdown`, gated on P0), but the executors
are tailored to Quick Accent's architecture.

## When to Use This Skill

- Sign off, smoke test, or acceptance/regression test **Quick Accent** before a release
- **Gate a build** on P0 (exact French glyph sets + candidate data + launch/enable)
- Catch a **removed or reordered accent glyph**, a **native↔managed `LetterKey` enum
  drift**, a **DPI/multi-monitor/caret positioning** regression, or a **lifecycle**
  break (orphaned hook, second instance not self-exiting)
- Verify a change to `src/modules/poweraccent/**` didn't break the data the overlay renders
- Pair with the `powertoys-poweraccent-knowledge` skill (engineering/review context)

## Architecture Under Test

Quick Accent = a C++/WinRT low-level keyboard hook (`PowerAccentKeyboardService`)
driving a WinUI 3 picker (`PowerAccent.UI`); orchestration/positioning/settings in
`PowerAccent.Core`; language data in the WinRT-free POCO lib `PowerAccent.Common`; the
module DLL (`PowerAccentModuleInterface`) + `Program.cs` handle lifecycle/GPO.
`PowerToys.PowerAccent.exe` installs a global `WH_KEYBOARD_LL` hook, holds the
`"QuickAccent"` single-instance mutex, and waits on `POWERACCENT_EXIT_EVENT`.

## How to Launch/Enable & the Three Executors

Enable/launch **works under automation**: running `PowerToys.PowerAccent.exe` directly
acquires the mutex, materializes `%LOCALAPPDATA%\Microsoft\PowerToys\QuickAccent\settings.json`,
installs the hook, and waits on the named exit event. The suite exercises this for real.

Each check runs against fresh binaries via one of three **real** executors:

| Executor | What it does |
|----------|--------------|
| `vstest` | Runs the module's own MSTest DLLs (`PowerAccent.Common.UnitTests`, `PowerAccent.Core.UnitTests`) once each via `vstest.console`, parses per-test outcomes from a TRX; a check passes iff all its mapped test methods pass. |
| `glyph` | Runs `GlyphDriver.exe`, which **reflection-loads** the freshly-built `PowerAccent.Common.dll` and asserts **EXACT** end-user glyph sets (pins specific accents the data-invariant unit tests would miss — e.g. a removed `é`). |
| `lifecycle` | Launches the real `PowerToys.PowerAccent.exe` and asserts launch/enable (settings materialize), single-instance (2nd instance self-exits on mutex), and clean `POWERACCENT_EXIT_EVENT` shutdown. |

## Sign-off Capabilities (P0/P1/P2)

Full machine-readable spec: [assets/poweraccent.spec.json](./assets/poweraccent.spec.json) (20 checks).

### P0 — release gate (all must pass)
| id | capability | executor |
|----|-----------|----------|
| `candidates-populated` | Accent-capable key → non-empty candidates; unmapped key / no languages → empty | vstest |
| `glyph-fr-a-exact` | French `a` → exactly `[à â á ä ã æ]` in order | glyph |
| `glyph-fr-e-exact` | French `e` → exactly `[é è ê ë €]` in order | glyph |
| `glyph-fr-c-exact` | French `c` → exactly `[ç]` | glyph |
| `lifecycle-launch-enable` | Enabling launches the exe, which stays resident + materializes settings | lifecycle |

### P1 — important
| id | capability | executor |
|----|-----------|----------|
| `candidates-dedup` | Same glyph across languages shown once | vstest |
| `candidates-ordering` | Candidates ordered by language DisplayOrder; single language → only its glyphs | vstest |
| `charset-all-expansion` | "ALL" expands to every language once; stable cached union | vstest |
| `enum-lockstep-native-managed` | Managed `LetterKey` matches native WinRT enum (names + values) | vstest |
| `language-metadata-integrity` | Every language entry well-formed & present in order maps | vstest |
| `language-alphabetical` | Spoken languages alphabetical by display name | vstest |
| `unknown-language-throws` | Unknown language fails fast (no silent-wrong candidates) | vstest |
| `glyph-cur-e-euro` | Currency-only language: `e` → exactly `[€]` | glyph |
| `glyph-all-a-contains-common` | All-languages `a` includes common Latin accents | glyph |
| `lifecycle-single-instance` | 2nd instance detects mutex and self-exits; one resident owner | lifecycle |
| `lifecycle-clean-exit` | `POWERACCENT_EXIT_EVENT` → clean shutdown (hook uninstalled) | lifecycle |

### P2 — nice-to-have
| id | capability | executor |
|----|-----------|----------|
| `positioning-dpi1` | 100% DPI: overlay anchors land at expected coords | vstest |
| `positioning-dpi-scaling` | 150% DPI: overlay footprint scales, stays on-screen | vstest |
| `positioning-multimonitor` | Honors active-monitor origin (offset & negative-origin) | vstest |
| `caret-placement` | Caret-follow: centers above caret, clamps at edges, flips below | vstest |

## Running the Sign-off

Prerequisites: **Python 3.9+**, **.NET SDK** (`dotnet`, for the glyph driver — targets
`net10.0-windows`), **VS 2022+ with `vstest.console.exe`**, and a **built PowerToys**
(`x64\Release` with the PowerAccent test DLLs and `WinUI3Apps\PowerToys.PowerAccent.exe`).

One command — build glyph driver + run all three executors + emit reports:

```powershell
# from scripts/  (add -RebuildTests to also rebuild the MSTest projects first)
./run-signoff.ps1
./run-signoff.ps1 -RebuildTests -Python "C:\Path\to\python.exe"
./run-signoff.ps1 -Skip lifecycle          # skip the real-process checks
```

Or invoke the Python harness directly (glyph driver must already be built):

```powershell
python run_signoff.py --release C:\s\powertoys\x64\Release
python run_signoff.py --help
```

Outputs `results.json` + `report_generated.md`. **Exit code 0 = GATE PASS, 1 = GATE
FAIL (a P0 check failed), 2 = setup error.** Baseline is 20/20; re-run twice to rule out
any transient before treating a flip as a real regression.

To validate the suite itself (proven 5/5): inject a minimal source edit (e.g. remove
`"é"` from the French `VK_E` set, reverse candidate ordering, or change a `LetterKey`
value), rebuild the single module, re-run — the mapped check must flip — then revert and
rebuild clean.

## Coverage & Limits

**The live overlay-summon path is BLOCKED under RDP — and this suite deliberately does
NOT claim to cover it.** This is a feature of the skill (honest scope), not a hidden gap.

- **Why blocked:** summoning the overlay needs synthetic keystrokes (hold a letter +
  press the activation key). Under an **RDP session that does not own the physical input
  queue**, `SendInput`/`keybd_event`/`BlockInput` all return **`ERROR_ACCESS_DENIED`
  (GetLastError = 5)**. It is **not** an elevation or desktop-mismatch issue. `winappcli`
  (v0.4.0) has **no key-send verb**, and its `set-value` writes text directly into the
  control, **bypassing the low-level hook**, so it cannot summon the overlay either.
- **What is therefore NOT covered here** (all require an input-owning session):
  actual hook interception of a real keypress; the ~300 ms `InputTime` render defer;
  key-up commit/insert (backspace-base + insert-glyph); Shift/arrow navigation *through
  the visible overlay*; press-and-hold mode; excluded-app / game-mode suppression.
- **What IS covered** (the surface that determines overlay correctness): candidate data +
  exact glyph sets, native↔managed enum lockstep, DPI/multi-monitor/caret positioning
  math, and full launch/enable/single-instance/clean-exit lifecycle.
- **How to run it fully:** execute this suite on the **interactive console session
  (session 1, input-owning)**, not an RDP session. A complete `SendInput` trigger
  harness (`keydriver.py`, `edithost.py` in the original proof folder) is ready to drive
  the overlay and can then be UIA-verified via the generic `app-signoff-uia` skill.

## Gotchas

- **`PowerAccent.Common.dll` is a WinRT-free POCO** — the glyph driver reflection-loads
  it directly. **Never** add `Common.Dotnet.CsWinRT.props` to it "to fix" a load error;
  it's deliberately excluded (`verifyCommonProps.ps1`).
- **The module's own unit tests are data-invariant** — they verify *structure* (dedup,
  ordering, well-formedness), not *specific glyphs*. A removed accent like `é` passes
  vstest but **fails the glyph driver**. Keep the `glyph-*` checks — they are the only
  ones that pin exact end-user output.
- **Keep enums in lockstep or activation silently breaks.** `enum-lockstep-native-managed`
  guards `LetterKey` names+values across the C++/WinRT `.idl` ↔ managed `.cs` boundary; a
  drift makes the wrong physical key trigger the wrong candidates.
- **Set `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0`** for the glyph driver (the runner
  does this) — invariant globalization can alter culture-sensitive string comparisons.
- **Run the console/full-globalization build for glyphs** — the csproj sets
  `InvariantGlobalization=false` on purpose.
- **A wrong enum value that collides** (e.g. `VK_A`→`VK_B`'s `0x42`) trips analyzer-as-
  error (`CA1069`/`CA2244`) and the **rebuild fails** — the harness then runs stale
  binaries. Use a non-colliding wrong value when validating that check.
- **`get-value`/overlay text is never asserted here** — there is no overlay in this
  scope. Do not add UIA overlay assertions to this suite; they belong on the console run.
- **Collateral flips are real, not false positives.** Removing `.Distinct()` breaks both
  dedup *and* ordering; dropping a DPI factor shifts both scaling *and* multi-monitor
  anchors — these are genuine consequences of one defect.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `GlyphDriver.exe not built` | Run `run-signoff.ps1` (it builds it) or `dotnet build scripts/glyphdriver`. |
| `vstest.console.exe not found` | Pass `--vstest <path>` or install VS test platform; or `--skip vstest`. |
| Glyph check fails with mangled accents | Ensure `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=0` and the non-invariant build. |
| Lifecycle check hangs / leaves a process | Runner signals `POWERACCENT_EXIT_EVENT` and force-terminates on exit; re-run. |
| Overlay/interception checks "missing" | Expected — blocked under RDP; run on the console session (see Coverage & Limits). |
| Regression not detected after edit | Confirm the single module actually rebuilt (fresh DLL timestamp); the harness runs whatever is in `x64\Release`. |

## References

- [assets/poweraccent.spec.json](./assets/poweraccent.spec.json) — the 20-check P0/P1/P2 spec.
- [scripts/run-signoff.ps1](./scripts/run-signoff.ps1) — build + run orchestrator (`-RebuildTests`, `-Skip`).
- [scripts/run_signoff.py](./scripts/run_signoff.py) — the harness (`--help`); vstest/glyph/lifecycle executors + gated report.
- [scripts/glyphdriver/](./scripts/glyphdriver/) — reflection glyph-assertion driver over `PowerAccent.Common.dll`.
- Companion skill `powertoys-poweraccent-knowledge` — engineering/regression/review context.
- Generic skill `app-signoff-uia` — the UIA/winappcli sign-off framework (use it for the overlay on a console session).
- Source: `src/modules/poweraccent/` in [microsoft/PowerToys](https://github.com/microsoft/PowerToys).
