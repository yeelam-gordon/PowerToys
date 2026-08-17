# Using the PowerToys per-module knowledge skills

Shared guidance for how to *use* the `<module>-knowledge` skills during PR review,
planning, and bug-fixing. Each module skill links here instead of repeating this text.

## PR review — read the diff first (anti-anchoring)

**Read the diff cold and form your own list of concerns before opening the skill.** Skimming a
module's Regression Playbooks first can anchor the review on recurring themes and distract from the
PR's actual, concrete issues.

1. Read the diff; list concerns from what actually changed.
2. **Then** cross-check the touched files against the module skill's Module Map, Regression
   Playbooks, and Review Rules — only for the code paths the diff touches (targeted retrieval).
3. Treat the skill as a checklist for the touched area, not a script for the whole review.

These skills are most valuable for **planning, onboarding, and issue-fixing** (where module
familiarity is the bottleneck), and least valuable as a flat pre-read for expert PR review.

## Skill and catalog have different jobs

- `SKILL.md` contains the actionable module map, regression playbooks, review rules, and pitfalls.
- `references/regression-catalog.md` is an evidence ledger: PR/issue links, chronology, reviewer
  decisions, unresolved symptom clusters, and evidence-quality caveats.
- Do not copy a playbook into the catalog. Link from the catalog to the playbook and record only the
  evidence or decision context that is not already actionable in `SKILL.md`.
- When a rule describes a defect that still exists in current source, label it **Known current
  violation**. A guardrail must never imply the code already implements the guard.

For changes that cross module boundaries or touch `src/common/`, also load
[`shared-infrastructure-knowledge`](./shared-infrastructure-knowledge/SKILL.md). Module skills own
feature behavior; the shared skill owns blast-radius and recurring shared-code checks.

## Localizing a bug

Treat a skill's Module Map and Regression Playbooks as **hypotheses to confirm in source**, not
ground truth. Where the feature-to-file map is complete they localize precisely; where it is thin
they can anchor you onto a confident, wrong file. If a symptom does not map cleanly to a listed
area, reason from the symptom and verify against source — do not force-fit the map.

## Freshness

These maps were verified against a specific `microsoft/PowerToys` main snapshot; file paths and
symbol names drift across releases. Always confirm against current source before relying on a path.
