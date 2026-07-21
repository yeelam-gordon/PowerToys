# Evidence — powertoys-poweraccent-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** [Quick Accent] Remove wpfui
- **Real fix PR:** [#46604](https://github.com/microsoft/PowerToys/pull/46604)
- **Ground-truth changed file(s):** Directory.Packages.props, NOTICE.md, src/modules/poweraccent/PowerAccent.UI/App.xaml, src/modules/poweraccent/PowerAccent.UI/PowerAccent.UI.csproj, src/modules/poweraccent/PowerAccent.UI/Selector.xaml, src/modules/poweraccent/PowerAccent.UI/Selector.xaml.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.67 | **1.00** |
| Recovered the real fix PR #46604? | no | **yes** |

With the skill, the agent localized: `src/modules/poweraccent/PowerAccent.UI/App.xaml` (lines 4, 10, 11).

**Lift: +0.33.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)