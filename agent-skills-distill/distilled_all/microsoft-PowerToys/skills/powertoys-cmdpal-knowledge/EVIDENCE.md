# Evidence — powertoys-cmdpal-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** CmdPal: Stop item action being executed when CmdPal is compact and col
- **Real fix PR:** [#49182](https://github.com/microsoft/PowerToys/pull/49182)
- **Ground-truth changed file(s):** src/modules/cmdpal/Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.33 | **1.00** |
| Recovered the real fix PR #49182? | no | **yes** |

With the skill, the agent localized: `src/modules/cmdpal/Microsoft.CmdPal.UI/Pages/ShellPage.xaml.cs`.

**Lift: +0.67.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-cmdpal/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-cmdpal/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-cmdpal/answer_baseline.md`
