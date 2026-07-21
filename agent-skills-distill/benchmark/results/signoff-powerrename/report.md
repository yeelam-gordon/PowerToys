# PowerRename — winappcli UI Sign-off Proof (Skill-2)

**Real execution. Honest numbers. Fail-fast.**
Target: the real, fully-built PowerToys module **PowerRename**
(`C:\s\powertoys\x64\Release\WinUI3Apps\PowerToys.PowerRename.exe`, WinUI 3).
Skill under proof: `.github/skills/app-signoff-uia` (`signoff.py`), driven through
`winapp ui` UI Automation.

---

## (a) How PowerRename UI was launched

PowerRename is normally a shell-extension rename window that Explorer starts by
piping the selected paths over a named pipe. Reading the app's own entry point
(`PowerRenameUILib/PowerRenameXAML/App.xaml.cpp` → `OnLaunched`) shows a second,
automation-friendly path: if the command line contains **no `\\.\pipe\` token** but
**does** contain file-path arguments, the app parses those args
(`ParseCommandLineArgs`) and populates `g_files` directly. This is the same entry
point the shipped `PowerRenameUITest` suite uses.

So the UI is launched deterministically, with no Explorer / shell interaction:

```
PowerToys.PowerRename.exe "…\testCase1.txt" "…\testCase2.txt" "…\SpecialCase.txt" "…\report_2020.log"
```

A temp working folder (`workfiles\`) holds four deterministic sample files. The
window appears as `HWND … "PowerRename" [WinUIDesktopWin32WindowClass]` and is
driven with `winapp ui` by HWND. **Apply is never clicked** — every assertion reads
the live *preview*, so the sample files are never renamed on disk (non-destructive).

### Automation specifics discovered against the live app
- **Reads use `winapp ui search <text>`** — it matches by accessible name and needs
  no hashed selectors. Renamed results show up as `Text` elements
  (e.g. `Renamed1.txt`); the counters show as `(0)`/`(1)`/`(2)` labels.
- **Two text boxes have per-session slugs.** `Search for` / `Replace with` resolve to
  `txt-textbox-XXXX`, and the hash changes on every launch (verified `ff97` → `0fe9`).
  The harness resolves them at runtime via `search` (type `Edit`). All other controls
  use stable `x:Name` automationIds (`checkBox_regex`, `checkBox_case`,
  `checkBox_matchAll`, `toggleButton_upperCase`, …).
- **Checkboxes expose only the Toggle pattern** (no deterministic set-state), so to
  keep checks independent, the harness (`run_powerrename_signoff.py`) launches a
  **fresh PowerRename instance per check** — each check begins from the default
  (all-off) flag state. The harness reuses the skill's own engine
  (`signoff.execute_check` / `build_report` / `report_to_markdown`).
- **Gotcha fixed during authoring:** `search` exits non-zero on 0 matches, and
  `signoff.py`'s `contains` is case-insensitive by default. Absence is therefore
  asserted *positively* via the renamed-count label, and the uppercase check uses
  `ci:false`. Without this, the clean build showed 2 false failures — corrected, then
  re-verified green.

## (b) Capability spec (P0 / P1 / P2)

Priorities were chosen from the distilled regression history
(`distilled_v3/microsoft-PowerToys/PowerRename.md`) to cover the module's real,
regression-prone capabilities. Full spec: [`powerrename.spec.json`](./powerrename.spec.json).

| Priority | Check | Real capability (distilled ground) |
|----------|-------|------------------------------------|
| **P0** | `p0-literal-replace-multi-preview` | Literal search/replace renames all matched files + preview updates — the entry point for ~every rename bug (`Replace()`). `testCase`→`Renamed` ⇒ `Renamed1.txt`, `Renamed2.txt`. |
| **P0** | `p0-literal-replace-distinct-file` | Literal replace on a distinct file. `Special`→`General` ⇒ `GeneralCase.txt`. |
| **P1** | `p1-regex-replace` | Regex-vs-literal dispatch (`RegexReplaceDispatch`). `^test.*\.txt$`→`matched.txt`: literal count `(0)` → regex on → `matched.txt`, count `(2)`. |
| **P1** | `p1-case-sensitive-toggle` | Case-sensitive flag path. `testcase1`→`match1`: default (ci) count `(1)` → Case sensitive on → count `(0)`. |
| **P1** | `p1-enumerate-counter-padding` | Enumeration counter token `${padding=2}` (regression class B). ⇒ `img_00`, `img_01`. |
| **P2** | `p2-capture-groups` | Regex capture-group rewrite `$1/$2` (regression class A). `^(testCase)(\d)\.txt$`→`$2_$1` ⇒ `1_testCase`, `2_testCase`. |
| **P2** | `p2-uppercase-transform` | Case transform (`GetTransformedFileName`). Uppercase toggle ⇒ `TESTCASE1.TXT` (asserted case-exact). |
| **P2** | `p2-match-all-occurrences` | Match-all-occurrences flag (issue #37845). `t`→`f`: first-only `festCase1.txt` → match-all on → `fesfCase1.fxf`. |

## (c) Green baseline — confirmed ✅

Two consecutive runs on the clean build, **both 8/8 PASS**, identical results — no
flakiness. Gate rule: all P0 must pass.

| Run | Gate | Passed |
|-----|------|--------|
| baseline_run1 | ✅ PASS | 8/8 |
| baseline_run2 | ✅ PASS | 8/8 |

Example green report: [`example_report_green_baseline.md`](./example_report_green_baseline.md).

## (d) Regression detection

Five minimal behavioral regressions were injected one at a time into the PowerRename
engine, the **PowerRenameUI** project was rebuilt (`msbuild … Release x64`, exe
mtime advanced each time), signoff re-run, then the change **reverted**.

| # | File → change | Intended check | Detected | Gate | Checks failed |
|---|---------------|----------------|----------|------|---------------|
| **R1** | `PowerRenameRegEx.cpp`: simple-replace `res = sourceToUse.replace(...)` → `res = sourceToUse;` | `p0-literal-replace-multi-preview` | ✅ | **FAIL** | both P0 + `p1-case` + `p2-match-all` (all literal-path; regex checks stayed green) |
| **R2** | `PowerRenameRegEx.cpp`: `isCaseInsensitive = !(m_flags & CaseSensitive)` → `= true` | `p1-case-sensitive-toggle` | ✅ | PASS | `p1-case-sensitive-toggle` (isolated) |
| **R3** | `Enumerating.cpp`: padding regex `padding=(\d+)` → `padding=(z+)` | `p1-enumerate-counter-padding` | ✅ | PASS | `p1-enumerate-counter-padding` (isolated) |
| **R4** | `PowerRenameRegEx.cpp`: group rewrite `L"$1$0$4"` → `L"$1$0"` | `p2-capture-groups` | ✅ | PASS | `p2-capture-groups` (isolated) |
| **R5** | `PowerRenameRegEx.cpp`: `if (!(m_flags & MatchAllOccurrences))` → `|| true` | `p2-match-all-occurrences` | ✅ | PASS | `p2-match-all-occurrences` (isolated) |

- **Detection rate: 5 / 5 = 100 %.** Every regression flipped its intended check
  PASS → FAIL.
- R1 is an engine-wide literal break, so it also fails the other literal-path checks —
  expected, honest collateral; the regex-path checks (regex, enumerate, capture,
  uppercase) stayed green, localizing the fault. R2–R5 each flipped **exactly one**
  check.
- Example regression report: [`example_report_regression_R2_case.md`](./example_report_regression_R2_case.md).

### False-positive rate

Across **3 clean-build runs** (baseline_run1, baseline_run2, and the post-revert
clean rebuild) × 8 checks = **24 clean check-executions, 0 false failures →
false-positive rate 0/24 = 0 %.**

## (e) Blockers / downshifts

**None.** UIA automation of the PowerRename rename window is fully feasible. Two
issues were encountered and solved without downshifting:
1. Per-session hashed slugs for the two text boxes → resolved at runtime via `search`.
2. `search` non-zero exit on 0 matches + case-insensitive `contains` → absence
   asserted via count labels; uppercase uses `ci:false`.

## (f) Confidence: **HIGH**

Real WinUI 3 app driven end-to-end via `winapp ui`; deterministic launch; stable,
repeatable green baseline (2×); 100 % regression detection with real rebuilds; 0 %
false positives across 24 clean executions. PowerToys tree left clean
(`git diff --stat` empty), final clean rebuild GREEN 8/8.

---

### Reproduce

```powershell
# baseline (clean build already present)
$base='C:\s\Demo\SkillForDistill\benchmark\results\signoff-powerrename'
$py='C:\Users\yeelam\AppData\Local\Programs\Python\Python312\python.exe'
$files = @("testCase1.txt","testCase2.txt","SpecialCase.txt","report_2020.log") |
         ForEach-Object { Join-Path "$base\workfiles" $_ }
& $py "$base\run_powerrename_signoff.py" --spec "$base\powerrename.spec.json" `
      --files $files --report-json "$base\out.json" --report-md "$base\out.md"

# a regression cycle (inject in source, then):
& "$base\build_and_run.ps1" -Tag R2   # builds PowerRenameUI, relaunches, re-runs signoff
```

### Files in this folder
- `powerrename.spec.json` — the P0/P1/P2 capability spec (runtime slug placeholders).
- `run_powerrename_signoff.py` — per-check fresh-launch harness (reuses `signoff.py`).
- `build_and_run.ps1` — rebuild PowerRenameUI + re-run signoff.
- `results.json` — machine-readable proof summary (this run).
- `baseline_run1/2.{md,json}`, `clean_final.{md,json}` — clean-build reports (all 8/8).
- `regression_R1..R5.{md,json}` — per-regression reports.
- `example_report_green_baseline.md`, `example_report_regression_R2_case.md` — examples.
- `workfiles\` — deterministic sample files opened in PowerRename.
