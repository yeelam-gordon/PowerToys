# Evidence — powertoys-awake-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [Awake] Fix issue with timed mode not expiring correctly
- **Real fix PR:** [#43785](https://github.com/microsoft/PowerToys/pull/43785)
- **Ground-truth changed file(s):** src/modules/awake/Awake/Core/Manager.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.50 | **1.00** |
| Recovered the real fix PR #43785? | no | **yes** |

With the skill, the agent localized: `src/modules/awake/Awake/Core/Manager.cs`.

**Lift: +0.50.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-awake/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-awake/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-awake/answer_baseline.md`
