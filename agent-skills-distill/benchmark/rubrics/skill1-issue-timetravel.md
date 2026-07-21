# B1 — Issue-fix Time-Travel Rubric

## Setup
1. Select N fixed issues per module that reference a fixing PR/commit. Find via the
   distilled `Regression History` links or `gh issue list --state closed`.
2. For each, resolve the **fix commit** and its **parent** in your local PowerToys clone (`<powertoys-root>`).
3. Create an isolated worktree at the parent: `git worktree add <dir> <parent_sha>`.
   The bug is now "live" and unfixed.

## Candidate task (fresh Opus agent, isolated context)
Give ONLY: the issue title/body (fix hidden) + the distilled `<Module>.md`.
Ask it to: (a) name the culprit file(s)/function, (b) describe the fix, (c) cite the
historical fix PR/commit if the distilled knowledge lets it.

## Baseline
Same task, same issue, but WITHOUT the distilled knowledge (repo + issue only).

## Scoring (per issue, 0–1 each; mean)
| Metric | 1.0 | 0.5 | 0 |
|--------|-----|-----|---|
| `located_area` | names the exact file/function changed by the fix | right directory/module | wrong |
| `fix_matches` | approach matches the real diff's strategy | partially | contradicts |
| `found_fix_ref` | cites the real fix PR/commit | cites a related PR | none/wrong |

**Skill value = mean(skill) − mean(baseline).** Positive and ≥0.2 = the distillation helps.

## Anti-cheat
- Strip any text that names the fix commit from the issue body before handing to the agent.
- Use a fresh sub-agent per issue (no cross-contamination).
- Verify `found_fix_ref` against the actual git log, not the agent's assertion.
