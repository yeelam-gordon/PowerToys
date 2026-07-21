# B2 — PR-review Recall Rubric

## Setup
1. Select merged PRs per module with **many** substantive review comments (≥8), via
   `gh api /repos/microsoft/PowerToys/pulls/{n}/comments`.
2. Extract the **ground-truth comment set**: dedupe, drop bot/CI/no-op ("/azp run", "LGTM",
   status posts). Each ground-truth item = {topic, concern, file/area}.
3. Reconstruct the PR's **initial diff** (first-push state): `git diff <base>...<pr_first_commit>`.

## Candidate reviewer (fresh Opus agent)
Input: the initial diff + the distilled `<Module>.md`. Produce review comments.
Simulate **rounds**: after round k, reveal which ground-truth items are still unaddressed
(topic-level, not verbatim) and let it review again, up to a cap.

## Matching
A candidate comment **matches** a ground-truth item if a judge agent rates them the same
concern on the same area (semantic, not string). One match per ground-truth item.

## Metrics
- `recall@1`, `recall@2`, `recall@3` = fraction of ground-truth items matched by round.
- `rounds_to_90` = rounds to reach 90% recall (cap = fail).
- `precision` = matched / total candidate comments (penalize spray-and-pray).

**Target:** `recall@3 ≥ 0.8` and `rounds_to_90 ≤ 3`, beating a no-distillation baseline
that historically needed ~10–20 comment rounds.

## Anti-cheat
- The distilled knowledge must NOT contain the specific PR's own comments verbatim for the
  PRs under test — hold those PRs out of the distillation input (train/test split by PR number).
