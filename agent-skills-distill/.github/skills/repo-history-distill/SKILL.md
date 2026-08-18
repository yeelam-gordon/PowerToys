---
name: repo-history-distill
description: 'Distill per-module engineering knowledge from ANY GitHub application''s issues, pull requests, and commit history, and EMIT it as an installable per-module Agent Skill. Use when asked to summarize a repo''s history, extract important PR review comments, build a regression history, capture coding conventions, onboard onto a large multi-module app, or generate a knowledge skill for a component. App-agnostic — ask for the target repo/app if unspecified. Mines gh CLI + GitHub API. Keywords: PR comments, code review, regression, conventions, module, component, onboarding, history distillation, skill generation.'
license: Complete terms in LICENSE.txt
---

# Repo History Distiller

Turn a large application's GitHub history (issues, PRs, review comments, commits) into a compact, per-module knowledge base: the decisions, regressions, and conventions that a new contributor would otherwise learn only by reading thousands of threads.

The unit of distillation is the **module** (a.k.a. component / feature area). Large apps are organized this way — each module maps to a directory (e.g. `src/modules/<name>/`, `packages/<name>/`, `apps/<name>/`). **This skill produces one installable Agent Skill per module** (a `SKILL.md` package), so an agent later working on that module auto-discovers and loads its distilled knowledge.

This skill is **app-agnostic**: it works for any GitHub repository. It is not tied to any particular app.

## Using This Knowledge in PR Review (Anti-Anchoring)

**Benchmark-derived warning.** When reviewing a specific PR, do **NOT** read this module file
first and then hunt the diff for its themes — that *anchors* you on recurring concerns and
measurably *lowers* your catch rate on the PR's actual, concrete issues (verified: a distilled
essay reduced a reviewer's recall vs. reading the diff cold).

Correct usage for PR review:
1. **Read the diff first**, cold. Form your own list of concerns from what actually changed.
2. **Then** cross-check against this file's `Regression History` and `Common Practices` — but
   only for the code paths the diff actually touches (targeted retrieval, not the whole essay).
3. Treat the history as a **checklist for the touched area**, not a script for the whole review.

This file is most valuable for **planning, onboarding, and issue-fixing** (where module
familiarity is the bottleneck), and least valuable as a flat pre-read for expert PR review.

**Localizing a bug (benchmark-derived).** When using this file to find a bug's culprit, treat
its `Overview` map and `Regression History` as **hypotheses to confirm in source**, not ground
truth. A benchmark showed distilled knowledge localizes with high precision *where its
feature→file map is complete* (it named the exact fix function by memory) — but where the map
is **thin, it anchored a triager onto a confident, wrong file**. If the symptom does not clearly
map to a listed area, reason from the symptom and verify against source; do not force-fit the map.

## What to Distill: Signal, Sourcing & Discoverability

This section defines what a **correct** distilled entry is. Apply it to every item in
`Key Decisions`, `Important PR Comments`, `Regression History`, and `Common Practices`.

### 1. Signal filter — distill only durable, foundational, recurring lessons
Include an item ONLY if it is a **generalizable lesson tied to a durable quality attribute or a
design-principle violation**. Use this taxonomy (not exhaustive — add adjacent quality attributes):
- **Design/architecture** violations (layering, separation of concerns, API contracts, established patterns)
- **Security** and **privacy/data-protection**
- **Performance & scalability** (latency, allocations, algorithmic complexity, resource/memory use)
- **Reliability & error handling** (failure modes, recovery, leaks, crashes, data integrity/correctness)
- **Concurrency & thread-safety** (races, deadlocks, UI-thread rules)
- **Compatibility** (backward/forward, OS/version, ABI, settings/serialization migration)
- **Accessibility**
- **Globalization/localization** (i18n, encoding, RTL, locale, resource strings)
- **User experience** (consistency, discoverability, expected behavior)
- **Maintainability & testability** (test guardrails, invariants), **observability** (logging/telemetry), **packaging/servicing** (installer, signing, registration, update)

**EXCLUDE (noise):** one-off or author-specific oddities, pure style/formatting/naming nits,
typos/spelling, CI/build-status chatter, "LGTM" — anything not generalizable to future work.

### 2. Sourcing — link generic knowledge, don't rewrite it
If a lesson is **generic and publicly documented** (e.g. OWASP, WCAG, Unicode/CLDR, platform
docs), **link the authoritative reference** instead of restating it. Restating what Copilot
already knows wastes budget and adds anchoring noise.

### 3. Discoverability — a bare link is INERT; attach the app-specific hook
A link alone will never be *discovered/used* by a future agent — it doesn't say when it applies
here. So **every referenced generic rule MUST carry an app-specific discovery hook**:
- **WHY** it's a real problem *in this app* (what concretely breaks),
- **WHERE/WHEN** it applies (the specific feature / file / code path — tie to the Overview map),
- **EVIDENCE** — the real PR/issue where violating it caused a problem here.

The distilled value = **app-specific connection + evidence + link** — NOT the generic text, and
NOT a bare link.

**Template:**
> **\<Quality attribute\> — \<short title\>** ([public ref](url)): In this module, \<why it
> breaks here\>; applies at `\<file/function\>`; violated in \<#PR/issue\>. When touching
> \<trigger\>, check \<the specific thing\>.

**Example (generic — replace with the real app's specifics):**
> **Globalization — Unicode normalization** ([Unicode NFC](https://unicode.org/reports/tr15/)):
> text matching must NFC-normalize and fold NBSP before comparing — in the `<module>` this lives
> in `<file>::<function>`; matching broke for NFD / non-breaking-space inputs (issue #NNNN). When
> touching text matching, verify normalization first.

## Division of Labor: Script Gathers, LLM Distills

**The bundled script (`distill.py`) ONLY gathers and organizes raw data** — PRs, review
comments, conversation, issues. **It does not and must not generate insight.** All
distillation judgment — *which* comments matter, *why* they matter, what decision or
convention or regression they encode — is produced by **you, the LLM**, by reading the raw
JSON directly.

- `fetch` = data gathering (dumb, deterministic, cache-friendly).
- `render` = a **scaffold only**: it emits section headers and a rough candidate list so you
  have a starting point. Its heuristic ordering is a convenience, **not** a ranking to trust.
- **Read the raw `review_comments.json` / `conversation.json` in full** before writing each
  section. Do not let the scaffold's top-N list cause you to miss signal — the insight is
  yours to find, not the script's to pick.

## When to Use This Skill

- User is **planning a feature or bug fix** in a module and wants the relevant prior art, decisions, and best practices "on the spot"
- User is **reviewing a PR** and wants to check it against the conventions and regression traps the maintainers already established
- User is **fixing an issue** and wants to know how similar bugs were fixed before (and what guardrails were added)
- User asks to "distill", "summarize", or "onboard onto" a GitHub app's history
- User wants the **important PR review comments** for a module (design decisions, review pushback, gotchas raised by maintainers)
- User wants a **regression history** — bugs that recurred, what caused them, how they were fixed
- User wants the **common practices / conventions** enforced in a module via review
- User is starting work on an unfamiliar module of a large app and needs context fast

The goal is **spot-on discovery of the best practices related to the app**, sourced from its own history, so planning / fixing / reviewing is grounded in what the maintainers already decided.

## Inputs — Ask If Not Specified

This skill is app-agnostic. Before mining you need three inputs; **if the user has not provided them, ASK — never assume a repository or app**:

1. **Target repository** — `owner/name` on GitHub. If only a local clone is given, derive it from the git remote and confirm with the user.
2. **The app / what it is** — enough context to write accurate, discoverable skill descriptions.
3. **The module map** — which directories map to which modules (see Core Concept below). Offer to draft one with `--map-modules` from a local clone, then confirm before mining.

Do not default to any particular repo or app.

## Prerequisites

- **GitHub CLI** authenticated: run `gh auth status`; if not logged in, `gh auth login`. The scripts call `gh api`, so auth is mandatory (raises the rate limit from 60 → 5000 req/hr).
- **Python 3.9+** for the distiller script.
- The target repo does **not** need to be cloned — everything is fetched via the GitHub API. A local clone only helps for the optional `--map-modules` directory scan.

## Core Concept: Module Map

Before mining, define a **module map** — how top-level directories/paths correspond to named modules. Many repos keep each module under a common root (e.g. `src/modules/<name>/`). Supply it as JSON:

```json
{
  "ModuleA": ["src/modules/module-a/"],
  "ModuleB": ["src/modules/module-b/"]
}
```

Generate a starting map from a local clone with `scripts/distill.py --map-modules <repo_dir>`, then edit it. See [module-map.md](./references/module-map.md).

## Step-by-Step Workflow

Run these in order. Each writes to `--out` (default `./distilled/<owner>-<repo>/`).

1. **Confirm auth & scope**: `gh auth status` and confirm the repo `owner/name` and the module map.
2. **Fetch** raw history per module — merged PRs touching the module's paths, their review comments, and issues labeled as bugs/regressions:
   `python scripts/distill.py fetch --repo <owner>/<repo> --modules module-map.json --out ./distilled`
3. **Distill** the raw data into a per-module markdown knowledge file. The script's `render`
   only scaffolds section headers + candidates; **you (the LLM) read the raw JSON in full and
   write every insight** (which comments matter, why, decisions, regressions, conventions):
   `python scripts/distill.py render --out ./distilled` produces skeletons; then replace all
   bracketed prompts with grounded, analytical prose.
4. **Review** each module file for the four sections: Decisions, Important PR Comments, Regression History, Common Practices.

See [distill-workflow.md](./references/distill-workflow.md) for the full data model and gh/GraphQL query recipes.

## What "Important" Means (Copilot's Judgment)

The script gathers candidates; **you decide what is signal**. A PR review comment is *important* when it:

- States a **design decision or rejected alternative** ("we can't use a global hook here because…")
- Encodes a **convention** the maintainer enforces ("all settings must round-trip through the JSON schema")
- Warns of a **regression trap** ("this broke DPI scaling last time — add a test")
- Explains **non-obvious why**, not just *what*

Skip nitpicks (formatting, typos, "LGTM"). Prefer comments from maintainers/CODEOWNERS and comments with many reactions or long threads.

## Output: a Per-Module TARGET SKILL

The distillation does **not** just write a plain notes file — it **emits an installable Agent Skill** per module, ready to drop into the target app's `.github/skills/`. Each module skill follows the Agent Skills format (see [agent-skills.instructions.md](../../instructions/agent-skills.instructions.md)) so it is auto-discovered when an agent works on that module. **The distilled result IS a skill.**

Produce `distilled/<owner>-<repo>/skills/<repo>-<module>-knowledge/`:

| File / section | Purpose |
|----------------|---------|
| `SKILL.md` frontmatter | `name` + keyword-dense `description` = WHAT the module is + WHEN to load (planning / fixing / reviewing) + keywords — this is the discovery mechanism |
| `SKILL.md` › `## When to Use This Skill` | Concrete, module-specific triggers |
| `SKILL.md` › `## Module Map` | Comprehensive **feature→file/function** map — the localization aid (gaps cause confident-wrong triage) |
| `SKILL.md` › `## Regression Playbooks` | Rule-by-rule per recurring regression class: **Symptom → Where → Root cause → Guardrail** + PR/issue links |
| `SKILL.md` › `## Review Rules` | Enforced conventions (imperative); generic rules **link** the public ref + carry the app-specific discovery hook |
| `SKILL.md` › `## Gotchas` | Highest-signal "never do X because Y" warnings |
| `SKILL.md` › `## Using This Skill in PR Review` | The anti-anchoring note (read the diff first, then cross-check the touched area) |
| `templates/pr-review-checklist.md` | A checklist an agent applies to a PR in this module |
| `templates/bug-triage.md` | Symptom → likely file/function via the Module Map |
| `references/regression-catalog.md` | Fuller regression list (progressive disclosure; keeps SKILL.md < 500 lines) |
| `LICENSE.txt` | Apache-2.0 |

Every section must obey the Signal / Sourcing / Discoverability rules above. The `render` script scaffolds structure; **you (the LLM) write all insight** grounded in the raw data + source.

## Gotchas

- **Never** rely on GitHub search to filter PRs by file path — the search API does **not** support path filtering for PRs. Enumerate commits for the path (`GET /repos/{o}/{r}/commits?path=<dir>`) and resolve their PRs via `GET /commits/{sha}/pulls`, or use the GraphQL `associatedPullRequests`. The script already does this; don't "simplify" it to `gh search prs`.
- **Regression labels are repo-specific.** Repos vary: some use `Regression`/`bug`, others use area labels like `Area-<Module>` / `Product-<Module>`. Pass `--regression-labels` / area labels explicitly; do not assume a label named `regression` exists.
- **Review comments ≠ issue comments.** Line-level review feedback lives at `/pulls/{n}/comments`; the PR conversation lives at `/issues/{n}/comments`. Fetch **both** — decisions are often in the conversation, gotchas in the line comments.
- **Rate limits bite on big apps.** Large apps can have 10k+ PRs. Always scope with `--since` (e.g. last 18 months) and `--max-prs`, and let the script's built-in backoff handle `403 rate limit` — do not remove it.
- **Bot noise.** Filter out comments from `dependabot`, `github-actions`, and known CI bots before distilling, or they drown the signal. The script strips a default bot list; extend it with `--ignore-users`.
- **Squash-merged repos** collapse many commits to one on `main`. Use the PR's own commits/files, not just `main`'s history, to attribute changes to a module.

## Troubleshooting

| Issue | Solution |
|-------|----------|
| `gh: Bad credentials` / 401 | Run `gh auth login`; verify with `gh auth status`. |
| `403 API rate limit exceeded` | Ensure authenticated (5000/hr). Narrow with `--since` / `--max-prs`; the script backs off automatically. |
| A module comes back empty | Its paths in the module map are wrong. Verify with `gh api "/repos/{o}/{r}/commits?path=<dir>&per_page=1"`. |
| Too much low-value noise | Add reviewers-only mode `--maintainers-only`, raise `--min-reactions`, or extend `--ignore-users`. |
| GraphQL `RATE_LIMITED` | GraphQL has a separate points budget; the script falls back to REST. Re-run the single failed module. |

## References

- [distill-workflow.md](./references/distill-workflow.md) — data model, gh/GraphQL query recipes, JSON schemas
- [module-map.md](./references/module-map.md) — how to build and validate a module map (worked example)
- [distill.py](./scripts/distill.py) — the fetch/render CLI (`--help` for usage)
