# Evidence — powertoys-cmdnotfound-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [CommandNotFound] Log and error handling
- **Real fix PR:** [#30745](https://github.com/microsoft/PowerToys/pull/30745)
- **Ground-truth changed file(s):** src/modules/cmdNotFound/CmdNotFound/CmdNotFound.csproj, src/modules/cmdNotFound/CmdNotFound/WinGetCommandNotFoundFeedbackPredictor.cs, src/modules/cmdNotFound/CmdNotFoundModuleInterface/dllmain.cpp

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.50 | **1.00** |
| Recovered the real fix PR #30745? | no | **yes** |

With the skill, the agent localized: `src/modules/cmdNotFound/CmdNotFound/WinGetCommandNotFoundFeedbackPredictor.cs`.

**Lift: +0.50.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)