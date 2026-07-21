# Evidence — powertoys-lightswitch-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [Light Switch] Fix Light Switch start up logic
- **Real fix PR:** [#45304](https://github.com/microsoft/PowerToys/pull/45304)
- **Ground-truth changed file(s):** src/modules/LightSwitch/LightSwitchService/LightSwitchService.cpp, src/modules/LightSwitch/LightSwitchService/LightSwitchStateManager.cpp, src/modules/LightSwitch/LightSwitchService/LightSwitchStateManager.h

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.50 | **1.00** |
| Recovered the real fix PR #45304? | no | **yes** |

With the skill, the agent localized: `src/modules/LightSwitch/LightSwitchService/LightSwitchStateManager.cpp`.

**Lift: +0.50.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)