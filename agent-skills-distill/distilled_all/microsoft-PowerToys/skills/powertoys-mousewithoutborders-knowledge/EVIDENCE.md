# Evidence — powertoys-mousewithoutborders-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [MouseWithoutBorders] Use per-connection random salt and IV for encrypted streams
- **Real fix PR:** [#48742](https://github.com/microsoft/PowerToys/pull/48742)
- **Ground-truth changed file(s):** src/modules/MouseWithoutBorders/App/Core/Encryption.cs, src/modules/MouseWithoutBorders/MouseWithoutBorders.UnitTests/Core/EncryptionTests.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.67 | **1.00** |
| Recovered the real fix PR #48742? | no | **yes** |

With the skill, the agent localized: `src/modules/MouseWithoutBorders/App/Core/Encryption.cs`.

**Lift: +0.33.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)