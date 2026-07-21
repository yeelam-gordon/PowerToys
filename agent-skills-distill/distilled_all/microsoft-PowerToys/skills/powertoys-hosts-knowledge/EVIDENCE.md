# Evidence — powertoys-hosts-knowledge finds a real issue

This skill is checked in with proof it surfaces a **real, historical** PowerToys bug in its
module. Methodology + full 30-module results: [`benchmark/ISSUE-BENCHMARK.md`](../../../../benchmark/ISSUE-BENCHMARK.md)
(scored blind, baseline vs with-skill, same model).

## The real bug (held-out)
- **Symptom:** IP address column enormously wide and not resizable; layout unusable.
- **Real fix PR:** [#32788](https://github.com/microsoft/PowerToys/pull/32788)
- **Ground-truth changed file(s):** src/modules/Hosts/Hosts/HostsXAML/MainWindow.xaml, src/modules/Hosts/HostsUILib/HostsMainPage.xaml

## What happened when an agent used THIS skill (vs cold)

| | Cold baseline | With this skill |
|---|---|---|
| Case score (mean of 3 axes) | 0.33 | **0.33** |
| Recovered the real fix PR #32788? | no | **no** |

With the skill, the agent localized: `src/modules/Hosts/HostsUILib/HostsMainPage.xaml` (the entries-list column definition) — the primary changed file; it did not identify the second changed file (`MainWindow.xaml`).

**Generalization control** (this exact bug was deliberately NOT distilled into the skill). **Lift: +0.00.** Honest null: the skill oriented to the right area but this novel bug was outside its map — it did not mislead.

## Raw artifacts (auditable)