# Evidence — powertoys-shortcutguide-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [Shortcut Guide] Prevent overlay crash on section navigation (#48448)
- **Real fix PR:** [#48481](https://github.com/microsoft/PowerToys/pull/48481)
- **Ground-truth changed file(s):** src/modules/ShortcutGuide/ShortcutGuide.Ui/Helpers/PinnedShortcutsHelper.cs, src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuideXAML/App.xaml.cs, src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuideXAML/MainWindow.xaml.cs, src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuideXAML/TaskbarWindow.xaml.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.17 | **1.00** |
| Recovered the real fix PR #48481? | no | **yes** |

With the skill, the agent localized: `ShortcutGuide.Ui/ShortcutGuideXAML/MainWindow.xaml.cs`.

**Lift: +0.83.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-shortcutguide/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-shortcutguide/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-shortcutguide/answer_baseline.md`
