# Evidence — powertoys-environmentvariables-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [PowerToys] Guard TitleBar windows against an empty window title (star
- **Real fix PR:** [#49069](https://github.com/microsoft/PowerToys/pull/49069)
- **Ground-truth changed file(s):** src/modules/EnvironmentVariables/EnvironmentVariables/EnvironmentVariablesXAML/MainWindow.xaml.cs, src/modules/FileLocksmith/FileLocksmithUI/FileLocksmithXAML/MainWindow.xaml.cs, src/modules/Hosts/Hosts/HostsXAML/MainWindow.xaml.cs, src/modules/ShortcutGuide/ShortcutGuide.Ui/ShortcutGuideXAML/MainWindow.xaml.cs, src/modules/registrypreview/RegistryPreview/RegistryPreviewXAML/MainWindow.xaml.cs, src/settings-ui/Settings.UI/SettingsXAML/Controls/Dashboard/ShortcutConflictWindow.xaml.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.00 | **1.00** |
| Recovered the real fix PR #49069? | no | **yes** |

With the skill, the agent localized: `src/modules/EnvironmentVariables/EnvironmentVariables/EnvironmentVariablesXAML/MainWindow.xaml.cs` (primary).

**Lift: +1.00.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-environmentvariables/ground_truth.diff)
- With-skill answer: `benchmark/results/b1/m-environmentvariables/answer_candidate.md`
- Cold answer: `benchmark/results/b1/m-environmentvariables/answer_baseline.md`
