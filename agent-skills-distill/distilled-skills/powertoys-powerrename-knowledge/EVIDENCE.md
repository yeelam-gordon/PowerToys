# Evidence — powertoys-powerrename-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [PowerRename] Fix Unicode characters and non-breaking spaces not being correctly normalized before matching
- **Real fix PR:** [#43972](https://github.com/microsoft/PowerToys/pull/43972)
- **Ground-truth changed file(s):** .github/actions/spell-check/allow/code.txt, src/modules/powerrename/lib/PowerRenameRegEx.cpp, src/modules/powerrename/unittests/CommonRegExTests.h

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.33 | **1.00** |
| Recovered the real fix PR #43972? | no | **yes** |

With the skill, the agent localized: `src/modules/powerrename/lib/PowerRenameRegEx.cpp` (Replace method, lines ~415-560).

**Lift: +0.67.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)