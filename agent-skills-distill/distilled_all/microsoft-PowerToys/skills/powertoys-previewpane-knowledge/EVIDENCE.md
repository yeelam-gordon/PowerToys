# Evidence — powertoys-previewpane-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Fix Markdown preview crash on UTF-8 files with >2MB size and <1.5M cha
- **Real fix PR:** [#47391](https://github.com/microsoft/PowerToys/pull/47391)
- **Ground-truth changed file(s):** src/modules/previewpane/MarkdownPreviewHandler/MarkdownPreviewHandlerControl.cs, src/modules/previewpane/UnitTests-MarkdownPreviewHandler/MarkdownPreviewHandlerTest.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.50 | **1.00** |
| Recovered the real fix PR #47391? | no | **yes** |

With the skill, the agent localized: `src/modules/previewpane/MarkdownPreviewHandler/MarkdownPreviewHandlerControl.cs` (line 186).

**Lift: +0.50.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-previewpane/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-previewpane/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-previewpane/answer_baseline.md`
