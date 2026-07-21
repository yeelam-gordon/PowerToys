# B1 Consolidated — issue-fix localization (3 cases, no-source, Opus, leak-free holdouts)

| Case | Bug | Real culprit | Candidate | Baseline | Lift |
|------|-----|--------------|:---------:|:--------:|:----:|
| #44980 | multi-monitor menu invisible | WindowsFunctions.cs/PowerAccent.cs (DPI) | 2.0 | 1.5 | +0.5 |
| #43971 | Unicode/NBSP not matching | PowerRenameRegEx.cpp SanitizeAndNormalize | 2.0 | 1.5 | +0.5 |
| #44202 | date token before capital fails | Helpers.cpp GetDatedFileName | 0.0 | 1.5 | -1.5 |
| **Mean** | | | **1.33** | **1.50** | **-0.17** |

## Finding (double-edged sword)
- WHERE the distilled knowledge covers the bug's area well, the candidate localizes with
  HIGH PRECISION — near-verbatim, naming the exact fix function (SanitizeAndNormalize,
  GetActiveDisplay/GetDisplayMaxWidth) that the baseline missed. Clear, repeatable value.
- WHERE coverage is thin, the distilled knowledge MISLEADS: on #44202 it anchored the
  candidate onto a confident-but-wrong file/theory (PowerRenameRegEx.cpp $-escaping) while
  the baseline correctly reasoned the defect from the symptom. Score 0.0 vs 1.5.

## Interpretation
Distilled history is a strong localization aid ONLY when (a) its feature->file map is COMPLETE
for the area and (b) it is used as a HYPOTHESIS to verify against source, not as ground truth.
Incomplete coverage causes confident-wrong anchoring — the same failure mode as B2.

## Two concrete skill improvements (shipped)
1. Distillation must produce a COMPREHENSIVE feature->file map in Overview (every user-facing
   sub-feature -> the file/function that implements it), so localization coverage is complete.
   #44202 failed because date-token logic (Helpers.cpp) was not mapped.
2. Usage guidance: treat localization hints as hypotheses to CONFIRM in source; if the symptom
   does not clearly map to a listed area, reason from the symptom (don't force-fit the map).
