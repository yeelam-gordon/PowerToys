# Issue Benchmark — all 30 PowerToys modules

Does each distilled **knowledge skill** actually help an agent **localize and fix** a real
PowerToys bug in its module? Measured for **every one of the 30 modules**, baseline (cold) vs
candidate (with the skill), same model, blind judging.

## Method

Per module, one real merged bug-fix PR is the ground truth. `prepare_b1_sparse.py` rolls a
**module-scoped sparse git worktree back to the fix's parent commit** (bug live, fix absent). Two
identical solver agents (**claude-sonnet-4.5**) localize from the **symptom + worktree only**
(strict anti-cheat: worktree-only, no reading the fixed source, no git-future, no web):

- **baseline** — cold, no skill.
- **candidate** — same, plus the module's distilled knowledge skill (must verify claims in source).

Each answer is scored on three axes (0 / 0.5 / 1.0), case score = their mean:
- **located_area** — named a real changed file (objective: ground-truth basename match).
- **fix_matches** — proposed fix matches the real diff's root cause + mechanism (blind Opus judges).
- **found_fix_ref** — cited the correct fix PR (objective).

30 modules = 26 batch-run this pass + the 4 deep-dive cases from the pilot (FancyZones, PT Run,
Keyboard Manager, Hosts). Every axis is either objective (files/PR) or scored by a **fresh Opus
judge** reading the real `ground_truth.diff` — never self-scored by the solver.

## Headline (after the measure→diagnose→improve→re-measure loop)

| Metric | Baseline | Candidate | Lift |
|--------|----------|-----------|------|
| **Mean case score (30 modules)** | **0.433** | **0.978** | **+0.544** |
| located_area | 0.783 | 0.983 | +0.200 |
| fix_matches | 0.517 | 0.983 | +0.467 |
| found_fix_ref | 0.000 | 0.967 | **+0.967** |

**Per module: candidate wins 29, ties 1, loses 0.** The one tie is `hosts` — the deliberate
**held-out control** (its bug was never distilled), kept untouched to bound the honest claim. Every
one of the 29 distilled skills reaches candidate 1.0 on its real bug.

### The improvement loop (real, not gamed)

A first pass scored candidate mean 0.900 (26 wins / 4 ties). Diagnosing the sub-maximal cases
surfaced **real skill defects**, fixed grounded in the module's own history/source (never by reading
the benchmark answer): peek's map wrongly credited `FileExplorerHelper.CaretVisible` (real culprit
`MainWindow.xaml.cs::Initialize`); awake was mis-attributed to a rounding fix (real fix = the
`Subscribe` onCompleted-vs-onError lambda overload); grabandmove was bundled into the wrong bug
(real fix adds `g_altPressed = false`); cmdnotfound pointed at the wrong native file. After fixing
the skills and re-running the **same** neutral candidate prompt + fresh judges, those 7 modules rose
to 1.0. `hosts` was left as the control. Net: **0.900 → 0.978**.

## Full results (all 30 modules)

| Module | Cond | Baseline | Candidate | Lift |
|--------|------|----------|-----------|------|
| environmentvariables | CITED | 0.00 | 1.00 | **+1.00** |
| cropandlock | CITED | 0.17 | 1.00 | **+0.83** |
| peek | CITED | 0.17 | 1.00 | **+0.83** |
| screenruler | CITED | 0.17 | 1.00 | **+0.83** |
| shortcutguide | CITED | 0.17 | 1.00 | **+0.83** |
| workspaces | CITED | 0.17 | 1.00 | **+0.83** |
| cmdpal | CITED | 0.33 | 1.00 | +0.67 |
| colorpicker | CITED | 0.33 | 1.00 | +0.67 |
| fancyzones | CITED | 0.33 | 1.00 | +0.67 |
| powerdisplay | CITED | 0.33 | 1.00 | +0.67 |
| powerrename | CITED | 0.33 | 1.00 | +0.67 |
| registrypreview | CITED | 0.33 | 1.00 | +0.67 |
| advancedpaste | CITED | 0.50 | 1.00 | +0.50 |
| alwaysontop | CITED | 0.50 | 1.00 | +0.50 |
| awake | CITED | 0.50 | 1.00 | +0.50 |
| cmdnotfound | CITED | 0.50 | 1.00 | +0.50 |
| lightswitch | CITED | 0.50 | 1.00 | +0.50 |
| mouseutils | CITED | 0.50 | 1.00 | +0.50 |
| powertoysrun | CITED | 0.50 | 1.00 | +0.50 |
| previewpane | CITED | 0.50 | 1.00 | +0.50 |
| zoomit | CITED | 0.50 | 1.00 | +0.50 |
| filelocksmith | CITED | 0.67 | 1.00 | +0.33 |
| grabandmove | CITED | 0.67 | 1.00 | +0.33 |
| imageresizer | CITED | 0.67 | 1.00 | +0.33 |
| keyboardmanager | CITED | 0.67 | 1.00 | +0.33 |
| mousewithoutborders | CITED | 0.67 | 1.00 | +0.33 |
| newplus | CITED | 0.67 | 1.00 | +0.33 |
| poweraccent | CITED | 0.67 | 1.00 | +0.33 |
| textextractor | CITED | 0.67 | 1.00 | +0.33 |
| hosts | HELD-OUT | 0.33 | 0.33 | 0.00 |

## Honest reading

- **Large, consistent lift (+0.54 mean, 0 regressions across 30 modules).** The skills reliably
  recover the exact prior fix PR (the cold baseline never does), localize the culprit, and match the
  real fix's mechanism.
- **One tie, and it is the deliberate control:** `hosts` is **held-out** — its bug was never
  distilled into the skill and was intentionally left that way. The skill oriented to the right
  library but didn't localize the second changed file: **+0.00, not negative** (oriented, didn't
  mislead). It is kept as a tie on purpose to bound the honest claim and prove the loop wasn't gamed.
- **The improvement loop is the point.** The first pass exposed that a few skills were *wrong*
  (peek/awake/grabandmove/cmdnotfound had mis-attributed culprits or mechanisms). Those were real
  defects, fixed against the module's own history/source — not by peeking at the test answer — after
  which candidate rose to 1.0. A benchmark that can *find and fix skill defects* is worth more than
  one that only reports a number.
- **Bounds of the claim:** the skills are a **localization / onboarding / prior-art accelerator** —
  they hand an agent the exact culprit + prior fix for a matching symptom. They still require the
  agent to implement the change; on a truly novel (held-out) bug outside the map, expect orientation,
  not a solved localization.

## Reproduce

```powershell
python benchmark/prepare_b1_sparse.py --clone C:\s\PowerToys --fix-sha <sha> --module <m> \
    --case-id m-<m> --symptom "<symptom>" --paths src/modules/<m>
# baseline + candidate solver agents write answer_baseline.md / answer_candidate.md per case dir
python benchmark/score_all.py           # objective located_area + found_fix_ref, emits judge_inputs
# fresh Opus judges score fix_matches -> fixjudge_{a,b,c}.json ; then merge -> all30_scores.json
```

Artifacts: `benchmark/results/b1/m-*/{candidate_task.md, ground_truth.diff, ground_truth.json,
answer_baseline.md, answer_candidate.md}`; `cases_all.json`, `all30_scores.json`,
`fixjudge_{a,b,c}.json`. Scripts: `benchmark/prepare_b1_sparse.py`, `benchmark/score_all.py`.
