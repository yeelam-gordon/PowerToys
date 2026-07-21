# SkillForDistill Benchmark

Outcome-based evaluation that measures **how good the distilled skills actually are** —
not whether the files exist, but whether the distilled knowledge changes engineering
outcomes. Both skills are generic; these benchmarks use fixed targets as a measuring stick.

Fixtures: `microsoft/PowerToys` modules **PowerRename, PowerAccent, AdvancedPaste**.
Point the harness at your own local PowerToys clone (referred to below as `<powertoys-root>`).

## Headline results (see the two benchmark reports)

- **[ISSUE-BENCHMARK.md](./ISSUE-BENCHMARK.md)** — knowledge skills, **all 30 modules**, real fixed
  bugs, time-travel localization, baseline vs. candidate (same model), blind Opus judges:
  **mean case score 0.43 → 0.98 (+0.54 lift) after a measure→diagnose→improve→re-measure loop;
  candidate wins 29 / ties 1 (held-out control) / loses 0**; recovered the exact fix PR 29/30
  (baseline 0/30). Each skill ships an `EVIDENCE.md` with its own proof.
- **[INJECTION-BENCHMARK.md](./INJECTION-BENCHMARK.md)** — sign-off skills, real source-level fault
  injection + rebuild + live winappcli: **PowerRename 10/10, AdvancedPaste 10/10**, 0 false positives
  on clean; see **[results/ACCEPTANCE-10x10.md](./results/ACCEPTANCE-10x10.md)**.
- **[results/TESTING-REPORT.md](./results/TESTING-REPORT.md)** — grounding of all 30 knowledge skills:
  **873 citations, 872 valid live (99.9%)**, 2 defects caught & fixed.

## The three evaluations

### B1 — Issue-fix time-travel (Skill 1)
Pick historically-**fixed** issues. Roll the working tree back to **before** the fix
(`git checkout <parent-of-fix-commit>`). The candidate (a fresh agent with ONLY the
distilled module knowledge, pretending the bug is unfixed) must:
1. **Locate** the culprit file/area,
2. Propose a fix consistent with how it was actually fixed,
3. Identify the historical fix commit/PR ("rollback to historical time").

Score = weighted mean over issues of {located_area, fix_matches_approach, found_fix_ref}.
Baseline = same task **without** the distilled knowledge. Skill wins if it beats baseline.
See `rubrics/skill1-issue-timetravel.md`.

### B2 — PR-review recall (Skill 1)
Take merged PRs that received many review comments historically. Reset to the PR's
**first commit** and have a reviewer agent (armed with distilled knowledge) review it.
Measure **recall** of the real historical review comments (semantic match) and the
**round count** to reach them. Target: surface the substantive comments in **≤2–3 rounds**
vs the historical **10–20**. Report recall@round and rounds-to-90%-recall.
See `rubrics/skill1-pr-recall.md`.

### B3 — Regression injection (Skill 2)
Recover real past regressions AND inject **10 fresh regressions** via 10 sub-agents into a
runnable app. The Skill-2 winappcli sign-off suite must **catch all 10** (and pass a clean
build). Score = detection rate (caught / injected) + false-positive rate on the clean build.
See `rubrics/skill2-regression-injection.md`.

## Scorecard
`run_b1.py` + `score_all.py` drive the B1 issue benchmark and emit per-module scores; the
B3 injection benchmark is driven from each sign-off skill's `scripts/` runner (see
[INJECTION-BENCHMARK.md](./INJECTION-BENCHMARK.md)). Each round: run → score → record
failures → improve skill → re-run (fail-fast). Every 5–10 rounds: meta-analyze failures and
replan strategy. Per-round `results/scorecard-*.json` summaries are generated locally and not
committed.

## Running
```
# B1 — issue-fix localization (per module)
python benchmark/prepare_b1_sparse.py --clone <powertoys-root> --fix-sha <sha> --module <m> --case-id m-<m>
python benchmark/score_all.py
# B3 — injection sign-off: see INJECTION-BENCHMARK.md
```
Requires `gh` authenticated, Python 3.12, a local PowerToys clone, and (for B3) winappcli.
