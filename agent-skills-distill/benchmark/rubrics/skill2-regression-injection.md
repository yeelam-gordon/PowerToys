# B3 — Regression-injection Rubric (Skill 2)

## Setup
1. A runnable target app with a UI (for the harness proof, a simple app is fine; for the
   PowerToys modules, the module's UI once built). The Skill-2 sign-off spec defines the
   P0/P1/P2 checks derived from real end-user capabilities.
2. Establish a **green baseline**: `signoff.py` passes all P0 (and ideally all) checks on the
   clean build.

## Injection
Dispatch **10 sub-agents**, each injects exactly ONE regression into an isolated copy:
- Break a specific user-facing behavior (e.g., disable a button handler, swap an operator,
  off-by-one in output, drop a settings round-trip, break clipboard read, etc.).
- Each records: {id, file, description, which capability it should break, expected failing check}.
- Regressions must be **behavioral** (observable via UI), not build breaks (those are trivial).

## Detection run
For each injected build, run `signoff.py`. A regression is **caught** if the sign-off report
flips the expected check (or any check) from PASS→FAIL for that build.

## Metrics
- `detection_rate` = caught / 10. **Target: 10/10.**
- `false_positive_rate` = checks that fail on the CLEAN build / total checks. Target: 0.
- `precision` = for each caught regression, did the FAILING check correspond to the broken
  capability (not an unrelated flake)?

## Value statement
The distilled sign-off suite is "good" when it catches regressions a naive smoke test would
miss — i.e., coverage tied to real end-user capabilities and priorities, proven at 10/10
with zero false positives on green.

## Anti-cheat / rigor
- Injecting sub-agents must not see the sign-off spec (else they dodge covered paths) —
  OR deliberately let them, to test worst-case coverage; record which mode was used.
- Re-run each build twice to rule out UI-automation flakiness before scoring a catch/miss.
