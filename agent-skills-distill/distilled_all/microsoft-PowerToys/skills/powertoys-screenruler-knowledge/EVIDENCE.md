# Evidence — powertoys-screenruler-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Fixed a memory alignment issue that caused the measure tool to crash on some machines.
- **Real fix PR:** [#41556](https://github.com/microsoft/PowerToys/pull/41556)
- **Ground-truth changed file(s):** src/modules/MeasureTool/MeasureToolCore/ToolState.h

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.17 | **1.00** |
| Recovered the real fix PR #41556? | no | **yes** |

With the skill, the agent localized: `MeasureToolCore/ToolState.h`.

**Lift: +0.83.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)