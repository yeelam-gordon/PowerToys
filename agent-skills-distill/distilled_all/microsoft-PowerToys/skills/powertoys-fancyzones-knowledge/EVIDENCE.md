# Evidence — powertoys-fancyzones-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Dragging a window near zones leaves drag state stuck and swallows subsequent keystrokes.
- **Real fix PR:** [#48569](https://github.com/microsoft/PowerToys/pull/48569)
- **Ground-truth changed file(s):** .github/actions/spell-check/expect.txt, src/modules/fancyzones/FancyZones/FancyZonesApp.cpp, src/modules/fancyzones/FancyZonesLib/FancyZones.cpp, src/modules/fancyzones/FancyZonesLib/FancyZonesWinHookEventIDs.cpp, src/modules/fancyzones/FancyZonesLib/FancyZonesWinHookEventIDs.h, src/modules/fancyzones/FancyZonesLib/WindowMouseSnap.cpp, src/modules/fancyzones/FancyZonesLib/WindowMouseSnap.h, src/modules/fancyzones/FancyZonesLib/util.cpp

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.33 | **1.00** |
| Recovered the real fix PR #48569? | no | **yes** |

With the skill, the agent localized: `FancyZonesLib/FancyZones.cpp` + `WindowMouseSnap.cpp` (missing `EVENT_OBJECT_DESTROY` subscription → `WindowMouseSnap::Abort()`).

**Lift: +0.67.** With the skill, the agent localized the correct culprit area (perfect 1.00 vs 0.33 cold) AND recovered the exact fix PR #48569.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/48542/ground_truth.diff)
- Task prompt: [`candidate_task.md`](../../../../benchmark/results/b1/48542/candidate_task.md)
- Blind judge scores: [`judge.json`](../../../../benchmark/results/b1/48542/judge.json)
