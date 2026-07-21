# Evidence — powertoys-poweraccent-signoff catches real regressions

This sign-off skill is checked in with **fault-injection** proof: real source-level bugs were
planted in the real module, rebuilt, and driven through the executable P0/P1/P2 sign-off suite
(vstest / glyph / lifecycle executors).
Methodology: [`benchmark/INJECTION-BENCHMARK.md`](../../benchmark/INJECTION-BENCHMARK.md).

## Result: **5/5 (glyph/lifecycle; overlay RDP-limited) injected regressions caught**, 0 false positives on the clean build.

Full evidence + per-injection screenshots/reports:
[`benchmark/results/ACCEPTANCE-10x10.md`](../../benchmark/results/ACCEPTANCE-10x10.md) and
`benchmark/results/signoff-poweraccent/`.
