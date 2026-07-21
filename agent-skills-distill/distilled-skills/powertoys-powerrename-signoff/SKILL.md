---
name: powertoys-powerrename-signoff
description: 'Run a winappcli (winapp ui) P0/P1/P2 UI sign-off of the real PowerToys PowerRename WinUI 3 rename window to verify literal & regex search/replace, case sensitivity, match-all-occurrences, enumeration counters, capture groups, and uppercase/lowercase/titlecase/capitalize transforms actually work — and to catch regressions. Non-destructive: reads the live preview, never clicks Apply. Load before releasing PowerRename, after changing src/modules/powerrename, or to smoke-test / regression-check the rename engine and editor. Keywords: PowerRename, PowerToys, bulk rename, UI sign-off, smoke test, regression test, winappcli, winapp ui, UI automation, UIA, P0 P1 P2, regex, capture groups, counter padding, case sensitive, match all occurrences, uppercase lowercase titlecase capitalize, release gate.'
license: Complete terms in LICENSE.txt
---

# PowerToys PowerRename UI Sign-off (winappcli)

Run a prioritized **P0/P1/P2 UI sign-off** of the real, fully-built PowerRename
WinUI 3 rename window (`PowerToys.PowerRename.exe`) through `winapp ui` UI
Automation. **Ten** capability checks verify PowerRename's core behaviors from the
end-user surface and catch engine regressions. **Non-destructive** — every
assertion reads the live *preview*; **Apply is never clicked**, so sample files are
never renamed on disk.

This is the per-module product of the generic
[app-signoff-uia](../../.github/skills/app-signoff-uia/SKILL.md) skill. The
**source of truth is the declarative [signoff-checklist.md](./signoff-checklist.md)**;
[`assets/powerrename.spec.json`](./assets/powerrename.spec.json) is its machine-runnable
mirror, executed by the [runner](./scripts/run-signoff.py).

## When to Use This Skill

- Before releasing / shipping a PowerToys build — gate on PowerRename's P0 checks.
- After changing anything under `src/modules/powerrename/` (the regex engine,
  enumeration, case transforms, the WinUI editor) — confirm no regression.
- To **smoke-test** that search/replace, regex, case sensitivity, match-all,
  counters, capture groups, and the four case transforms still work end-to-end.
- To **regression-check** a suspected PowerRename bug against the real app.
- To reproduce or extend the proven sign-off (10/10 green baseline, **10/10 injected
  regressions caught, 0 false positives** — see [References](#references)).

## Prerequisites

- **winappcli** on PATH (`winapp ui status`), v0.4.0+.
- **Python 3.9+**.
- A **built** `PowerToys.PowerRename.exe` (Release x64). Default location:
  `<powertoys-root>\x64\Release\WinUI3Apps\PowerToys.PowerRename.exe`.
- The generic **app-signoff-uia** skill available (the runner reuses its
  `signoff.py` engine); auto-detected, or pass `--signoff <path>`.

## App-Specific winappcli Logic

PowerRename normally opens as an Explorer shell-extension window that receives the
selected paths over a **named pipe**. It also has a deterministic, automation-friendly
entry point used by its own UI tests: in `App.xaml.cpp::OnLaunched`, if the command
line contains **no `\\.\pipe\` token** but **does** contain file-path arguments, the
app parses those args and populates the file list directly.

Launch it that way — no Explorer / shell interaction, fully deterministic:

```
PowerToys.PowerRename.exe "…\testCase1.txt" "…\testCase2.txt" "…\SpecialCase.txt" "…\report_2020.log"
```

The window appears as `HWND … "PowerRename" [WinUIDesktopWin32WindowClass]`. Drive it
with `winapp ui` **by HWND** (`-w <HWND>`). **Never click Apply** — read the preview.

### Automation specifics (learned against the live app)

- **Reads use the `winapp ui search <text>` verb**, which matches by accessible name
  (no hashed selector needed). Renamed results appear as `Text` elements
  (e.g. `Renamed1.txt`); the renamed counters appear as `(0)` / `(1)` / `(2)` labels.
- **The two text boxes have per-session hashed slugs.** `Search for` / `Replace with`
  resolve to `txt-textbox-XXXX`, and the hash **changes on every launch** (observed
  `ff97` → `0fe9`). The runner resolves them at runtime via `search` (type `Edit`) and
  substitutes the spec placeholders `__SEARCH_SLUG__` / `__REPLACE_SLUG__`.
- **All other controls use stable `x:Name` automationIds:** `checkBox_regex`,
  `checkBox_case`, `checkBox_matchAll`, `toggleButton_upperCase`,
  `toggleButton_lowerCase`, `toggleButton_titleCase`, `toggleButton_capitalize`
  (invoked via the UIA Toggle pattern).
- **Checkboxes/toggles expose only the UIA Toggle pattern** (no deterministic
  set-state), so the runner launches a **fresh PowerRename instance per check** —
  every check starts from the default (all-flags-off) state.
- **Absence must be asserted positively.** `search` exits non-zero on 0 matches, and
  the engine's `contains` is case-insensitive by default. So "no longer matches" is
  asserted via the renamed-**count label** (e.g. `(0)`), and the uppercase check uses
  `ci: false` to require an exact-case `TESTCASE1.TXT`.

## Sign-off Capabilities (P0/P1/P2)

Priorities map to PowerRename's real, regression-prone capabilities (from the
distilled module history). Declarative source: [signoff-checklist.md](./signoff-checklist.md);
machine mirror: [assets/powerrename.spec.json](./assets/powerrename.spec.json).

| Priority | Check id | Verifies |
|----------|----------|----------|
| **P0** | `p0-literal-replace-multi` | Literal search/replace renames all matched files + preview/count update: `testCase`→`Renamed` ⇒ `Renamed1.txt`, `Renamed2.txt`, count `(2)`. The core rename path. |
| **P0** | `p0-regex-replace` | Regex-vs-literal dispatch: `^test.*\.txt$`→`matched.txt` — literal count `(0)`, enable regex ⇒ `matched.txt`, count `(2)`. |
| **P1** | `p1-case-sensitive-toggle` | Case-sensitive flag: `testcase1`→`match1` matches by default (count `(1)`), enable Case sensitive ⇒ count `(0)`. |
| **P1** | `p1-match-all-occurrences` | Match-all flag: literal `t`→`f` first-only `festCase1.txt` → enable ⇒ `fesfCase1.fxf`. |
| **P1** | `p1-enumerate-counter-padding` | Enumeration counter token `img_${padding=2}` ⇒ zero-padded, incrementing `img_00`, `img_01`. |
| **P2** | `p2-capture-groups` | Regex capture-group rewrite `^(testCase)(\d)\.txt$`→`$2_$1` ⇒ `1_testCase`, `2_testCase`. |
| **P2** | `p2-uppercase-transform` | Uppercase toggle ⇒ `TESTCASE1.TXT` (asserted case-exact, `ci:false`). |
| **P2** | `p2-lowercase-transform` | Lowercase toggle ⇒ `specialcase.txt` (`ci:false`). |
| **P2** | `p2-titlecase-transform` | Title-case toggle ⇒ `Report_2020.log` (`ci:false`). |
| **P2** | `p2-capitalize-transform` | Capitalize toggle ⇒ `Specialcase.txt` (`ci:false`, distinct from `SpecialCase.txt`). |

**Gate rule:** all **P0** checks must pass or the sign-off FAILs (exit code 1).

- **Enumerate counter quirk:** `img_${padding=2}` produces `img_00`/`img_01`
  **without** toggling `enumItems` — invoking the enumerate toggle actually makes the
  token literal. Do not toggle it for the counter check.
- **Transforms need no search term:** the engine duplicates the source name when the
  replace result is null and a transform flag is set, so a toggle-only run (empty
  Search for/Replace with) still previews `TESTCASE1.TXT` etc.

## How to Sign Off

1. **Build the module** (Release x64) so `PowerToys.PowerRename.exe` is up to date —
   e.g. `msbuild src\modules\powerrename\PowerRenameUILib\PowerRenameUI.vcxproj
   /p:Configuration=Release /p:Platform=x64` after a VS dev-cmd environment.
2. **Run the sign-off** with the bundled spec. The runner auto-generates the four
   sample files, then for each check launches a fresh instance, resolves slugs by
   HWND, drives the preview, and reads assertions:
   ```powershell
   python scripts\run-signoff.py --powertoys-root C:\s\powertoys `
       --report-json out.json --report-md out.md
   ```
   Or point at a specific exe / files:
   ```powershell
   python scripts\run-signoff.py --exe <path\PowerToys.PowerRename.exe> `
       --files f1.txt f2.txt SpecialCase.txt report_2020.log `
       --report-json out.json --report-md out.md
   ```
3. **Read the gate.** Exit `0` = PASS (all P0 passed), `1` = FAIL, `2` = error. The
   Markdown report groups results by priority. Run `python scripts\run-signoff.py --help`
   for all options.
4. **Iterate.** If a check that should pass fails, re-inspect the live app for the
   current selector (`winapp ui inspect -i --json -w <HWND>`) and re-run — a single
   transient `element_not_found` is not a regression (the runner retries).

## Gotchas

- **NEVER click Apply.** Every check reads the live *preview* only; clicking Apply
  performs real, irreversible renames on the sample files. The sign-off is
  non-destructive by design — keep it that way.
- **NEVER launch with a `\\.\pipe\` argument.** That triggers the Explorer named-pipe
  path instead of the UI-test file-args path, and the window won't populate from your
  files. Pass **only file paths** on the command line.
- **The two text-box slugs are per-session and hashed** (`txt-textbox-XXXX`) — never
  hardcode them. The runner resolves `Search for` / `Replace with` at runtime via
  `search` (type `Edit`). All other controls use stable `x:Name` automationIds.
- **Assert absence via the count label, not a failed search.** `winapp ui search`
  exits non-zero on 0 matches and `contains` is case-insensitive, so "stopped
  matching" must be checked as `(0)`; the uppercase check needs `ci: false`.
- **One fresh app per check.** Checkbox/toggle controls have no set-state (Toggle
  pattern only); reusing an instance leaks flag state across checks and causes false
  results. The runner already relaunches per check — don't batch checks into one
  instance.
- **RDP / headless input constraints.** UIA `invoke`/`set-value` work headlessly, but
  screenshots over RDP may be black — prefer the read verbs (`search`), not visual
  checks. Raise `--step-pause`/`--settle` if the WinUI window is slow to settle.

## Coverage & Limits

- **What is covered (all winappcli-driven, non-destructive):** literal replace, regex
  vs literal dispatch, case sensitivity, match-all-occurrences, enumeration counter +
  padding, regex capture groups, and the four text-format transforms
  (upper/lower/title/capitalize). These ten map 1:1 to the checklist items and each is
  independently regression-tested (see proof below).
- **Detection is read-based, not pixel-based.** Verdicts come from `winapp ui search`
  / `get-value` reads of the live preview (accessible names + renamed-count labels).
  This is robust and headless-safe. The screenshots are supplementary visual evidence.
- **Screenshot rendering depends on session connection (honest note).** PowerRename is
  WinUI 3 (DirectComposition / swap-chain). The shipped baselines were captured with
  real `winapp ui screenshot` calls from a **connected** session and render the full
  client area (preview columns, checkboxes, transform popup). When the interactive
  session is **RDP-disconnected**, that surface is not composited, so `winapp ui
  screenshot` (and `PrintWindow` / `CopyFromScreen`, with or without `--capture-screen`)
  capture only window chrome. This does **not** affect the behavioral verdicts (they are
  read-based). Re-capture from a connected session if a baseline ever comes back blank.
- **Not covered:** actually committing renames (Apply is intentionally never clicked),
  file-conflict/undo dialogs, drag-drop file selection, and settings-page options
  outside the rename window. Add checklist items + a connected-session screenshot pass
  to extend coverage.

## Proof of Detection (acceptance)

An automated injection campaign (`benchmark/results/acc-powerrename/`) injects **10
distinct UI-observable bugs** into the real PowerRename source one at a time, rebuilds
the module (VsDevCmd + msbuild `PowerRenameUI.vcxproj`, Release x64), runs this
sign-off, records which checklist item flipped to FAIL, then reverts. Result:
**10/10 detected, 0 false positives**; the tree is left clean. See
`benchmark/results/acc-powerrename/report.md`.

## References

- Declarative checklist (source of truth): [signoff-checklist.md](./signoff-checklist.md)
- Generic sign-off skill (engine, verbs, spec schema):
  [../../.github/skills/app-signoff-uia/SKILL.md](../../.github/skills/app-signoff-uia/SKILL.md)
- Module knowledge (feature→file map, regression playbooks):
  [../powertoys-powerrename-knowledge/SKILL.md](../powertoys-powerrename-knowledge/SKILL.md)
- Acceptance proof (10 injections, 10/10 caught): `benchmark/results/acc-powerrename/report.md`
- Prior sign-off (launch method, 8-check baseline, 5 regressions): `benchmark/results/signoff-powerrename/report.md`
- Bundled capability spec: [assets/powerrename.spec.json](./assets/powerrename.spec.json)
- Baseline screenshots: `assets/screenshots/` (written at sign-off run time)
- Runner: [scripts/run-signoff.py](./scripts/run-signoff.py)
