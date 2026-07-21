# Evidence — powertoys-mouseutils-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (cited)
- **Symptom:** Ripple effect for Mouse Highlighter
- **Real fix PR:** [#48232](https://github.com/microsoft/PowerToys/pull/48232)
- **Ground-truth changed file(s):** .github/actions/spell-check/expect.txt, src/modules/MouseUtils/MouseHighlighter/MouseHighlighter.cpp, src/modules/MouseUtils/MouseHighlighter/MouseHighlighter.h, src/modules/MouseUtils/MouseHighlighter/dllmain.cpp, src/settings-ui/Settings.UI.Library/MouseHighlighterProperties.cs, src/settings-ui/Settings.UI/SettingsXAML/Views/MouseUtilsPage.xaml, src/settings-ui/Settings.UI/Strings/en-us/Resources.resw, src/settings-ui/Settings.UI/ViewModels/MouseUtilsViewModel.cs

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.50 | **1.00** |
| Recovered the real fix PR #48232? | no | **yes** |

With the skill, the agent localized: `src/modules/MouseUtils/MouseHighlighter/MouseHighlighter.cpp`.

**Lift: +0.50.** The skill did not just exist — an agent using it found the real culprit and the prior fix.

## Raw artifacts (auditable)