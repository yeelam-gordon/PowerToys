# Evidence — powertoys-powertoysrun-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** VS Code Workspaces: UNC/network workspace paths not opened / missing.
- **Real fix PR:** [#48922](https://github.com/microsoft/PowerToys/pull/48922)
- **Ground-truth changed file(s):** src/modules/launcher/Plugins/Community.PowerToys.Run.Plugin.VSCodeWorkspaces/WorkspacesHelper/VSCodeWorkspacesApi.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.50 | **1.00** |
| Recovered the real fix PR #48922? | no | **yes** |

With the skill, the agent localized: `Plugins/Community.PowerToys.Run.Plugin.VSCodeWorkspaces/WorkspacesHelper/VSCodeWorkspacesApi.cs`.

**Lift: +0.50.** With the skill, the agent localized the affected code (perfect 1.00 vs 0.50 cold) AND recovered the exact fix PR #48922.

## Raw artifacts (auditable)