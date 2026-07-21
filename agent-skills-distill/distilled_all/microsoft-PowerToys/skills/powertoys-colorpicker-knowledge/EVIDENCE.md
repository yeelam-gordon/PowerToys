# Evidence — powertoys-colorpicker-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [ColorPicker] Fix the main window UI appearing in the zoomed-in view
- **Real fix PR:** [#48762](https://github.com/microsoft/PowerToys/pull/48762)
- **Ground-truth changed file(s):** src/modules/colorPicker/ColorPickerUI/Helpers/AppStateHandler.cs, src/modules/colorPicker/ColorPickerUI/Helpers/WindowCaptureExclusionHelper.cs, src/modules/colorPicker/ColorPickerUI/Helpers/ZoomWindowHelper.cs, src/modules/colorPicker/ColorPickerUI/NativeMethods.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.33 | **1.00** |
| Recovered the real fix PR #48762? | no | **yes** |

With the skill, the agent localized: `src/modules/colorPicker/ColorPickerUI/Helpers/ZoomWindowHelper.cs` (primary).

**Lift: +0.67.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-colorpicker/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-colorpicker/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-colorpicker/answer_baseline.md`
