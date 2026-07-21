# Round 6 Scorecard — B1 issue-fix localization (issue #44980, PowerAccent multi-monitor)

No-source test: predict culprit files/functions + fix from the bug symptom.
Candidate = symptom + distilled_v3 PowerAccent.md (leak-free; holds out fix PR #46593).
Baseline  = symptom only.

| Triager | located_area | fix_matches | total (/2) |
|---------|-------------:|------------:|-----------:|
| Candidate (+ distilled) | 1.0 | 1.0 | **2.0** |
| Baseline (no distilled) | 1.0 | 0.5 | **1.5** |

**Lift +0.5.** Candidate pinpointed the true DPI root-cause functions (GetActiveDisplay,
GetDisplayMaxWidth) + exact physical-vs-DIP scaling; baseline stayed at the downstream UI layer.

## Significance
FIRST positive result. Consistent with the strategy: distilled regression history improves
bug LOCALIZATION (issue-fixing/planning), the skill's natural fit — unlike PR-review recall (B2).
Caveat: n=1. Need independent cross-module B1 trials to confirm robustness.
