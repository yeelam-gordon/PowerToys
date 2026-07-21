# B2 Meta-Analysis — Does distilled module knowledge improve PR review?

## Question
Does giving a reviewer the distilled per-module knowledge raise recall of the REAL
maintainer review comments on a held-out PR?

## Method
Test PR: PowerToys #46593 (Quick Accent UI/DPI/shift fixes). Ground truth = 16 real reviewer
comments (PR author excluded, bots/spelling filtered) ≈ 6 distinct concern clusters.
Candidate = reviewer WITH distilled_v3 PowerAccent.md (which HOLDS OUT #46593).
Baseline = same reviewer WITHOUT it. Fresh Opus judge, semantic matching, equal rigor.

## Results
| Round | Reviewer model | Context | Candidate recall | Baseline recall | Lift |
|------:|----------------|---------|-----------------:|----------------:|-----:|
| 2 | Opus | +source | 2/10 (diff PR #48891) | 2/10 | 0 (tie) |
| 3 | Opus | +source | 5/16 | 8/16 | -3 (baseline wins) |
| 4 | Opus | diff-only | 8/16 | 8/16 | 0 (tie, 0 unique each) |
| 5 | Haiku (junior) | diff-only | 1/16 | 3/16 | -2 (distilled HURT) |

## Finding
Distilled module knowledge gave **no recall lift** in any condition, and **reduced** recall for
the junior model by **anchoring** it on the cheat-sheet's recurring themes (DPI docs, generation
guards, surrogate pairs) instead of the concrete diff-visible issues (scrollbar, CsWin32) the
baseline caught. Strong models re-derive the concerns from the diff; the flat doc is redundant
for them and distracting for weak ones.

## Threats to validity (honest caveats)
- **n = 1 PR** (single module). Not yet generalized across PRs/modules.
- **Held-out bias AGAINST candidate**: removing #46593 stripped the very shift-state/off-screen
  lessons this PR is about, weakening the distilled doc on exactly the tested concerns.
- **Self-evident diff**: #46593's changes telegraph their own risks; even haiku pattern-matched them.
- **GT duplicates** inflate raw recall; distinct-cluster scoring (round 5) shows the same ranking.
- **Judge subjectivity** on 1-2 borderline semantic matches.

## Interpretation
PR-review-comment RECALL is likely the WRONG value metric for this skill. History distillation
should help most where module familiarity is the bottleneck: (a) LOCATING a culprit + prior fix
for a bug (planning / issue-fixing), (b) speed/onboarding (time-to-context), and (c) targeted,
DIFF-AWARE retrieval ("this PR touches the shift path -> here are the 3 past bugs there +
the exact guardrail tests"), NOT a flat module essay that anchors.

## Recommended strategy pivot
1. Move the value proof to **B1 (issue-fix time-travel)**: does distilled regression history help
   an agent LOCATE the culprit + match the historical fix, vs baseline? (natural fit).
2. Redesign Skill-1 USAGE for review: diff-aware targeted retrieval, not a whole-module dump;
   add an explicit anti-anchoring instruction ("read the diff first, THEN cross-check history").
3. Keep B2 as a secondary metric but measure DISTINCT-cluster recall and use held-in knowledge.
