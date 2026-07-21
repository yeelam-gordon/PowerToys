# Round 3 Scorecard — B2 PR-recall (PR #46593, PowerAccent technical fix)

| Reviewer | Comments | GT matched (of 16) | Recall |
|----------|----------|--------------------|--------|
| Candidate (WITH distilled v3, leak-free) | 16 | 5 | 0.31 |
| Baseline (WITHOUT) | 18 | 8 | 0.50 |

**Lift: -0.19. Candidate's matched set is a SUBSET of baseline's -> zero unique value.**

## Root-cause finding (critical)
Both reviewers were allowed to read the FULL module source at C:\s\PowerToys. A strong Opus
reviewer with full source access re-derives the module's traps itself, making the distilled
doc redundant and mildly anchoring (candidate produced fewer, more templated comments and
missed the off-screen-offset math the baseline caught by reading the diff freshly).

## Correction -> Round 4
Constrain BOTH reviewers to the DIFF ONLY (no source-clone access) = the realistic reviewer
context. The distilled knowledge should then substitute for the module familiarity the
baseline lacks, and any lift is attributable to the distillation.

## Broader options if round 4 also shows no lift
- Reframe metric: recall on RECURRING-concern GT subset (skill's actual target), not all GT.
- Deliver knowledge as an applied CHECKLIST (second pass) rather than a flat anchoring doc.
- Pivot emphasis to B1 (issue-fix locate+fix), a more natural fit for regression history.
