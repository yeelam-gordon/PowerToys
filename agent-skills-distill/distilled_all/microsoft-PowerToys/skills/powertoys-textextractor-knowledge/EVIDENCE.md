# Evidence — powertoys-textextractor-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Refactor PadImage to use out param and improve disposal
- **Real fix PR:** [#44906](https://github.com/microsoft/PowerToys/pull/44906)
- **Ground-truth changed file(s):** src/modules/PowerOCR/PowerOCR/Helpers/ImageMethods.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.67 | **1.00** |
| Recovered the real fix PR #44906? | no | **yes** |

With the skill, the agent localized: `src/modules/PowerOCR/PowerOCR/Helpers/ImageMethods.cs`.

**Lift: +0.33.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-textextractor/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-textextractor/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-textextractor/answer_baseline.md`
