# Evidence — powertoys-cropandlock-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [CropAndLock] theme
- **Real fix PR:** [#38044](https://github.com/microsoft/PowerToys/pull/38044)
- **Ground-truth changed file(s):** src/modules/CropAndLock/CropAndLock/CropAndLock.vcxproj, src/modules/CropAndLock/CropAndLock/main.cpp

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.17 | **1.00** |
| Recovered the real fix PR #38044? | no | **yes** |

With the skill, the agent localized: `src/modules/CropAndLock/CropAndLock/main.cpp` (primary).

**Lift: +0.83.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)