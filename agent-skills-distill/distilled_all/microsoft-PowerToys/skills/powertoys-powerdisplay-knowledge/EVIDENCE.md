# Evidence — powertoys-powerdisplay-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [PowerDisplay] Fix false-positive crash detection on cooperative shutdown
- **Real fix PR:** [#48173](https://github.com/microsoft/PowerToys/pull/48173)
- **Ground-truth changed file(s):** .github/actions/spell-check/expect.txt, src/modules/powerdisplay/PowerDisplay.Lib.UnitTests/CrashDetectionScopeTests.cs, src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs, src/modules/powerdisplay/PowerDisplay.Lib/Services/IProcessExitHook.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.33 | **1.00** |
| Recovered the real fix PR #48173? | no | **yes** |

With the skill, the agent localized: `src/modules/powerdisplay/PowerDisplay.Lib/Services/CrashDetectionScope.cs` (entire class).

**Lift: +0.67.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)