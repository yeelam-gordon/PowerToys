# Evidence — powertoys-imageresizer-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [Image Resizer] Automatically reload settings changes
- **Real fix PR:** [#45266](https://github.com/microsoft/PowerToys/pull/45266)
- **Ground-truth changed file(s):** src/common/ManagedCommon/IdRecoveryHelper.cs, src/modules/imageresizer/tests/Properties/SettingsTests.cs, src/modules/imageresizer/ui/ImageResizerXAML/MainWindow.xaml.cs, src/modules/imageresizer/ui/Properties/Settings.cs, src/modules/imageresizer/ui/ViewModels/InputViewModel.cs, src/settings-ui/Settings.UI/ViewModels/ImageResizerViewModel.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.67 | **1.00** |
| Recovered the real fix PR #45266? | no | **yes** |

With the skill, the agent localized: `src/modules/imageresizer/ui/Properties/Settings.cs`.

**Lift: +0.33.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-imageresizer/ground_truth.diff)
- With-skill answer: [`answer_candidate.md`](../../../../benchmark/results/b1/m-imageresizer/answer_candidate.md)
- Cold answer: [`answer_baseline.md`](../../../../benchmark/results/b1/m-imageresizer/answer_baseline.md)
