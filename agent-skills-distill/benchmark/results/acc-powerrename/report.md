# PowerRename UI Sign-off — Acceptance Proof (10/10 fault injection)

**Bottom line:** The declarative, winappcli-driven PowerRename sign-off
([`distilled-skills/powertoys-powerrename-signoff/`](../../../distilled-skills/powertoys-powerrename-signoff/))
catches **10 / 10** distinct, UI-observable behavioral bugs injected one-at-a-time into
the real PowerRename source. Each bug was compiled into the real
`PowerToys.PowerRename.exe`, exercised through the live WinUI 3 UI via `winapp ui`
(non-destructive — Apply never clicked), detected by the intended checklist item, and
then reverted. **Detection rate: 10/10. False positives: 0. Tree left clean.**

All results here come from real winappcli execution against the real, rebuilt app. No
unit-test/reflection bypass, no fabricated screenshots or reads.

## Method

For each of 10 injections the orchestrator
([`run_campaign.py`](./run_campaign.py)) did, automatically:

1. **Inject** one code change into the real source (`git`-tracked file), verified as a
   unique single-occurrence edit.
2. **Rebuild** `PowerRenameUILib\PowerRenameUI.vcxproj` (Release x64) via VsDevCmd +
   msbuild — this cascades to the `lib` where the engine/helpers live (incremental
   rebuild ~85–96 s).
3. **Run the sign-off** ([`scripts/run-signoff.py`](../../../distilled-skills/powertoys-powerrename-signoff/scripts/run-signoff.py))
   — fresh app instance per check, per-session slug resolution, `winapp ui`
   `set-value`/`invoke`/`search` reads of the live preview, plus a `winapp ui
   screenshot` per check.
4. **Record** which checklist items flipped to FAIL and whether the *targeted* item
   caught it.
5. **Revert** the injection (`git checkout --`) and confirm the tree is clean before
   the next injection.

Green baseline before the campaign: **10/10 PASS** (see
[`green` baseline report](../../../distilled-skills/powertoys-powerrename-signoff/assets/baseline-report.md)).

## The 10 injections and which checklist item caught each

| # | Injection (real code change) | File | Bug (UI-observable) | Target checklist item | Caught | All items that flipped FAIL |
|---|------------------------------|------|---------------------|-----------------------|:------:|-----------------------------|
| INJ1 | `res = sourceToUse.replace(pos, searchTerm.length(), replaceTerm);` → `res = sourceToUse;` | `PowerRenameRegEx.cpp` | Literal replace becomes a no-op | `p0-literal-replace-multi` | ✅ | p0-literal-replace-multi, p1-case-sensitive-toggle, p1-match-all-occurrences |
| INJ2 | `isCaseInsensitive = !(m_flags & CaseSensitive)` → `= true` | `PowerRenameRegEx.cpp` | Case-sensitive flag ignored | `p1-case-sensitive-toggle` | ✅ | p1-case-sensitive-toggle |
| INJ3 | `if (!(m_flags & MatchAllOccurrences))` → `if (false)` | `PowerRenameRegEx.cpp` | Always match-all (first-only default broken) | `p1-match-all-occurrences` | ✅ | p1-match-all-occurrences |
| INJ4 | `regex_replace(..., L"$1$0$4")` → `L"$1$0"` | `PowerRenameRegEx.cpp` | Capture-group back-references dropped | `p2-capture-groups` | ✅ | p2-capture-groups |
| INJ5 | `res = RegexReplaceDispatch[...](...)` → `res = sourceToUse;` | `PowerRenameRegEx.cpp` | Regex replace becomes a no-op | `p0-regex-replace` | ✅ | p0-regex-replace, p1-enumerate-counter-padding, p2-capture-groups |
| INJ6 | `enumIndex++` → `enumIndex += 0` | `PowerRenameRegEx.cpp` | Enumeration counter never increments | `p1-enumerate-counter-padding` | ✅ | p1-enumerate-counter-padding |
| INJ7 | `if (flags & Uppercase)` → `if (false && (flags & Uppercase))` | `Helpers.cpp` | Uppercase transform disabled | `p2-uppercase-transform` | ✅ | p2-uppercase-transform |
| INJ8 | `else if (flags & Lowercase)` → `else if (false && ...)` | `Helpers.cpp` | Lowercase transform disabled | `p2-lowercase-transform` | ✅ | p2-lowercase-transform |
| INJ9 | `else if (flags & Titlecase)` → `else if (false && ...)` | `Helpers.cpp` | Title-case transform disabled | `p2-titlecase-transform` | ✅ | p2-titlecase-transform |
| INJ10 | `else if (flags & Capitalized)` → `else if (false && ...)` | `Helpers.cpp` | Capitalize transform disabled | `p2-capitalize-transform` | ✅ | p2-capitalize-transform |

**Detection rate: 10 / 10.** Every injection was caught by its intended item. Three
injections (INJ1, INJ5) additionally tripped collateral items because those checks
share the same code path (e.g. the literal no-op also breaks the literal case-sensitive
and match-all checks; the regex no-op also breaks the regex-based enumerate and capture
checks) — this is expected and does not reduce the count; each bug is still caught by
its designated item. Machine record: [`results.json`](./results.json). Per-injection
detail: `report_INJx.{json,md}`.

## Screenshot-diff evidence

Screenshots are real `winapp ui screenshot` captures. The RDP session was **disconnected
during INJ1–INJ5** and **reconnected from INJ6 onward**; because PowerRename is WinUI 3
(DirectComposition / swap-chain), a disconnected session composites only window chrome.
Consequently:

- **INJ1–INJ5** (`screenshots/INJx/*.png`, ~16 KB): window chrome only. Detection for
  these was via the `winapp ui search`/count-label **reads** (unaffected by
  compositing) — see each `report_INJx.json` step detail for the real search JSON.
- **INJ6–INJ10** (`screenshots/INJx/*.png`, ~60–100 KB): **full client area rendered**,
  giving genuine visual-diff evidence. Example — **INJ7 (uppercase disabled)**:

  | Baseline (green) `p2-uppercase-transform.png` | INJ7 (bug) `p2-uppercase-transform.png` |
  |---|---|
  | `Renamed (4)`; names show `REPORT_2020.LOG`, `SPECIALCASE.TXT`, `TESTCASE1.TXT`, `TESTCASE2.TXT` | `Renamed (0)`; **no** uppercased names — every file keeps original case |

  Baseline: [`distilled-skills/powertoys-powerrename-signoff/assets/screenshots/p2-uppercase-transform.png`](../../../distilled-skills/powertoys-powerrename-signoff/assets/screenshots/p2-uppercase-transform.png).
  Injected: [`screenshots/INJ7/p2-uppercase-transform.png`](./screenshots/INJ7/p2-uppercase-transform.png).
  The same visible "Renamed (0) / no transform" diff holds for INJ8 (lowercase), INJ9
  (title-case), INJ10 (capitalize).

The detection gate is satisfied for all 10 by the `winapp ui` reads; the rendered
screenshots (INJ6–10) provide corroborating visual diffs. This split is reported
honestly rather than re-running INJ1–5 to force pixels.

## Baseline screenshots (green build)

Captured from a **connected** session, all 10 render the full UI and are shipped as the
skill's baselines under
[`assets/screenshots/`](../../../distilled-skills/powertoys-powerrename-signoff/assets/screenshots/)
(one per checklist item, 55–77 KB each). E.g. `p2-uppercase-transform.png` shows
`Renamed (4)` with the four uppercased names.

## Cleanliness & environment

- **Final `git -C C:\s\PowerToys status`:** only `?? src/modules/AdvancedPaste/AdvancedPaste.UnitTests/SignoffTransformTests.cs`
  remains — a **pre-existing** untracked file present *before* this campaign (not
  produced by this work; left untouched). All 10 injected edits were reverted; every
  injection's `reverted_clean` is `true`.
- **Processes:** all PowerRename instances launched by the runner are terminated.
- No `git commit` was made in `SkillForDistill`.

## Confidence

**High.** Real builds (10 successful incremental rebuilds), real winappcli execution
against the real UI, deterministic 10/10 detection with 0 false positives on the green
baseline, and clean revert of every injection. The only caveat is cosmetic: INJ1–5
screenshots are chrome-only due to an RDP disconnect during that window (behavioral
detection for those five is fully backed by the recorded winappcli reads).

## Artifacts

- [`results.json`](./results.json) — full machine record (per-injection code change,
  build status/time, gate, failed checks, revert-clean, screenshot path).
- `report_INJ1.json … report_INJ10.json` (+ `.md`) — per-injection sign-off reports with
  every `winapp ui` step and its real output.
- `screenshots/INJ1 … INJ10/` — per-injection, per-check `winapp ui screenshot` PNGs.
- [`run_campaign.py`](./run_campaign.py) — the injection orchestrator (source of truth
  for the 10 injections).
- `workfiles/` — the four sample files used for the preview.
