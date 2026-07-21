# Evidence — powertoys-advancedpaste-signoff catches real regressions

This sign-off skill is checked in with **fault-injection** proof: real source-level bugs were
planted in the real module, rebuilt, and driven through the declarative winappcli checklist.
Methodology: [`benchmark/INJECTION-BENCHMARK.md`](../../benchmark/INJECTION-BENCHMARK.md).

## Result: **10/10 injected regressions caught**, 0 false positives on the clean build.

Full evidence + per-injection screenshots/reports:
[`benchmark/results/ACCEPTANCE-10x10.md`](../../benchmark/results/ACCEPTANCE-10x10.md).
Per-injection reports + screenshots (`acc-advancedpaste/`, `signoff-advancedpaste/`) are **generated
by running the injection benchmark and are not committed** in this PR.