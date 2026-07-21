# Evidence — powertoys-workspaces-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Workspace: Fix an overlay issue for workspace snapshot draw
- **Real fix PR:** [#45183](https://github.com/microsoft/PowerToys/pull/45183)
- **Ground-truth changed file(s):** .github/actions/spell-check/expect.txt, src/modules/Workspaces/WorkspacesEditor/OverlayWindow.xaml.cs, src/modules/Workspaces/WorkspacesEditor/Utils/NativeMethods.cs, src/modules/Workspaces/WorkspacesEditor/ViewModels/MainViewModel.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.17 | **1.00** |
| Recovered the real fix PR #45183? | no | **yes** |

With the skill, the agent localized: `WorkspacesEditor/ViewModels/MainViewModel.cs`.

**Lift: +0.83.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)