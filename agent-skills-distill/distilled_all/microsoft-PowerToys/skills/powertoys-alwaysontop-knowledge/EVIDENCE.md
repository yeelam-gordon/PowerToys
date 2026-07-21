# Evidence — powertoys-alwaysontop-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Always On Top: Dedup the alwaysontop command id in window system menu
- **Real fix PR:** [#45845](https://github.com/microsoft/PowerToys/pull/45845)
- **Ground-truth changed file(s):** src/modules/alwaysontop/AlwaysOnTop/AlwaysOnTop.cpp

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.50 | **1.00** |
| Recovered the real fix PR #45845? | no | **yes** |

With the skill, the agent localized: `src/modules/alwaysontop/AlwaysOnTop/AlwaysOnTop.cpp`.

**Lift: +0.50.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-alwaysontop/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-alwaysontop/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-alwaysontop/answer_baseline.md`
