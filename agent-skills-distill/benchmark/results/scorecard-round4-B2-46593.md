# Round 4 Scorecard — B2 diff-only (PR #46593)

| Reviewer (Opus, diff-only) | Comments | GT matched (of 16) | Recall |
|----------|----------|--------------------|--------|
| Candidate (+ distilled) | 15 | 8 | 0.50 |
| Baseline (no distilled)  | 18 | 8 | 0.50 |

Unique catches: candidate 0, baseline 0. **Exact tie, no anchoring harm.**
(GT has duplicate concern clusters: scrollbar x4, CsWin32 x4, off-screen x3 -> ~5-6 distinct concerns.)

## Consolidated finding across B2 rounds 2-4
With an OPUS reviewer, distilled module knowledge yields NO recall lift — a strong model
re-derives the same concerns from the diff. The distillation is redundant for an expert reviewer.

## Strategy reframe -> Round 5 (leveling-up test)
The distillation's real value = bringing a NON-expert up to expert level. Use a WEAKER model
(claude-haiku-4.5) as reviewer:
 - weak baseline (haiku, diff only) vs weak candidate (haiku, diff + distilled).
 - If candidate >> baseline and approaches Opus's 8/16, the distillation demonstrably transfers expertise.
Also score on DISTINCT concern clusters (dedupe GT) for a cleaner metric.
