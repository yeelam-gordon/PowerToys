# Evidence — powertoys-registrypreview-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Improve RegistryPreview validation
- **Real fix PR:** [#31552](https://github.com/microsoft/PowerToys/pull/31552)
- **Ground-truth changed file(s):** src/modules/registrypreview/RegistryPreviewUI/MainWindow.Utilities.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.33 | **1.00** |
| Recovered the real fix PR #31552? | no | **yes** |

With the skill, the agent localized: `RegistryPreviewUI/MainWindow.Utilities.cs`.

**Lift: +0.67.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)