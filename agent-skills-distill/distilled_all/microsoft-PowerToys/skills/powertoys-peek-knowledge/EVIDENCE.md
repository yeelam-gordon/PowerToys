# Evidence — powertoys-peek-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [Peek] Fix Space key triggering during file rename (#44845)
- **Real fix PR:** [#44995](https://github.com/microsoft/PowerToys/pull/44995)
- **Ground-truth changed file(s):** src/modules/peek/Peek.UI/PeekXAML/MainWindow.xaml.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.17 | **1.00** |
| Recovered the real fix PR #44995? | no | **yes** |

With the skill, the agent localized: `src/modules/peek/Peek.UI/PeekXAML/MainWindow.xaml.cs`.

**Lift: +0.83.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-peek/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-peek/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-peek/answer_baseline.md`
