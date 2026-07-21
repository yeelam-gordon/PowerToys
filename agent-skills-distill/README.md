# Distilled PowerToys Agent Skills

Installable **GitHub Copilot Agent Skills** distilled from PowerToys' own engineering history,
each shipped with `EVIDENCE.md` proving it finds a real historical issue.

## Layout

| Path | What it is |
|------|-----------|
| `.github/skills/repo-history-distill/` | **Generator skill** — mines any GitHub repo's issues/PRs/commits into per-module knowledge skills. |
| `.github/skills/app-signoff-uia/` | **Generator skill** — drives any Windows app via winappcli into a declarative sign-off/regression suite. |
| `distilled-skills/` | The 3 hand-authored + proven skills: `powertoys-{powerrename,poweraccent,advancedpaste}-{knowledge,signoff}`. |
| `distilled_all/microsoft-PowerToys/skills/` | The 27 sub-agent-distilled per-module **knowledge** skills (alwaysontop … zoomit). |
| `benchmark/` | The evaluation harness + reports that prove skill value. |

Each knowledge skill = `SKILL.md` (frontmatter + Module Map + Regression Playbooks + Review Rules
+ Gotchas), `references/regression-catalog.md`, `templates/`, and `EVIDENCE.md`. Sign-off skills add
`signoff-checklist.md` (declarative P0/P1/P2 checks) + winappcli driving logic.

## Evidence (why these are worth checking in)

- **Issue benchmark** — [`benchmark/ISSUE-BENCHMARK.md`](benchmark/ISSUE-BENCHMARK.md): on real
  historical bugs, an agent *with* the skill vs *cold* (same model, blind judges) scores
  **0.43 → 0.98 mean (+0.54 lift); 29/30 modules reach 1.0**, recovering the exact fix PR the cold
  baseline never finds. `hosts` is kept as a held-out control (0 lift — the skill didn't mislead).
- **Injection benchmark** — [`benchmark/INJECTION-BENCHMARK.md`](benchmark/INJECTION-BENCHMARK.md):
  planting real source-level faults and running the sign-off skills catches **10/10** for
  PowerRename and AdvancedPaste, 0 false positives on the clean build.
- **Grounding** — [`benchmark/results/TESTING-REPORT.md`](benchmark/results/TESTING-REPORT.md):
  873 citations across the 30 knowledge skills, 99.9% verified live against GitHub.

## Reproduce the benchmark

```powershell
# Issue localization (per module): roll a worktree back to a fix's parent, solve with/without the skill, judge.
python benchmark/prepare_b1_sparse.py --clone <PowerToys clone> --fix-sha <sha> --module <m> --case-id m-<m>
python benchmark/score_all.py
# Sign-off fault injection:
pwsh benchmark/... (see INJECTION-BENCHMARK.md)
```

> This folder is an additive check-in for a Copilot code-review quality pass; it does not modify
> any PowerToys source.
