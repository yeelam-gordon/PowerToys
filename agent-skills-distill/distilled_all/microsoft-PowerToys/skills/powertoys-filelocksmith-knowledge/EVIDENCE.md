# Evidence — powertoys-filelocksmith-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [File Locksmith] Fix IPC text-mode file I/O corrupting Unicode paths
- **Real fix PR:** [#47361](https://github.com/microsoft/PowerToys/pull/47361)
- **Ground-truth changed file(s):** src/modules/FileLocksmith/FileLocksmithLib/IPC.cpp, src/modules/FileLocksmith/FileLocksmithLibInterop/NativeMethods.cpp

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.67 | **1.00** |
| Recovered the real fix PR #47361? | no | **yes** |

With the skill, the agent localized: `src/modules/FileLocksmith/FileLocksmithLib/IPC.cpp` (primary).

**Lift: +0.33.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)
- Real fix diff: [`ground_truth.diff`](../../../../benchmark/results/b1/m-filelocksmith/ground_truth.diff)
- With-skill answer: [`answer_candidate.md`](../../../../benchmark/results/b1/m-filelocksmith/answer_candidate.md)
- Cold answer: [`answer_baseline.md`](../../../../benchmark/results/b1/m-filelocksmith/answer_baseline.md)
