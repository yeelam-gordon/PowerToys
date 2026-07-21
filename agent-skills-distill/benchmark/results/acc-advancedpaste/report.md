# AdvancedPaste winappcli Sign-off — Acceptance (Fault Injection) Report

## Bottom line

The declarative, winappcli-driven AdvancedPaste sign-off catches **10 / 10**
injected, UI-observable bugs by driving the **real** AdvancedPaste app end-to-end
through winappcli — no unit-test / reflection / in-process bypass anywhere. Every
injection changed one source line, was rebuilt, caught by the sign-off, and
reverted; `git -C C:\s\PowerToys status` is **clean**.

**Detection rate: 10 / 10.**

## How each check was driven and verified (real execution)

For every injection the harness: killed AP → patched one source line → **rebuilt
the AdvancedPaste project** (MSBuild x64 Release, ~55 s) → summoned the **real AP
window** via the named-pipe ShowUI controller → set the clipboard → **`winapp ui
invoke` the real paste-format action** → **`winapp ui screenshot`** the window →
**verified the transform output** → reverted via `git checkout`.

**Verification of the produced output — note on the environment.** The task's
intended check is "paste into a target editor, then `winapp ui get-value` the
target." AdvancedPaste performs that paste with `SendInput(Ctrl+V)`. In this
session **synthetic input is denied**: `SendInput` returns 0 with `GetLastError =
5 (ERROR_ACCESS_DENIED)` and `GetForegroundWindow() == 0` — the signature of an
**Active-but-locked session (secure desktop)**. I am not elevated and cannot
unlock it; I polled for input recovery for **>10 minutes** with no change.

Because AdvancedPaste **writes the transformed result to the clipboard before it
issues the (denied) paste keystroke**, the sign-off verifies the *exact bytes
AdvancedPaste produces to paste* by reading that clipboard — still a real,
end-to-end exercise of the app's transform through its real UI action. The 3
non-paste checks (CHK-07/08/09) are pure UIA reads and are unaffected. When the
workstation is unlocked, `run_signoff.ps1` performs the literal
paste-into-Notepad + `get-value` variant instead (`run_signoff_clip.ps1` is the
locked-session variant used here).

## Synthetic input status (a)

- Worked earlier in the day (baseline pastes landed in Notepad).
- Now **LOCKED**: `SendInput` → `ERROR_ACCESS_DENIED (5)`, `GetForegroundWindow()
  == 0`, on `WinSta0\Default` (verified), session `Active` but on the secure
  desktop. Reconnecting RDP did not clear it; only an interactive unlock would.
- The sign-off was made robust to this exactly as instructed (poll, then use the
  real produced output) so the campaign still ran end-to-end against the real app.

## The 10 injections (b) — all caught

| # | Inj | Check | Pri | File | Injected bug | Produced output (real, from AP) | Result |
|---|-----|-------|-----|------|--------------|---------------------------------|--------|
| 1 | I1 | CHK-01 | P0 | Helpers/TransformHelpers.cs | plain-text output gets `_INJ` appended | `BoldHello_INJ` | **CAUGHT** |
| 2 | I2 | CHK-02 | P0 | Helpers/MarkdownHelper.cs | markdown returns raw HTML | `<h1>Title</h1><p><b>bold</b></p>` | **CAUGHT** |
| 3 | I3 | CHK-03 | P0 | Helpers/JsonHelper.cs | every CSV cell becomes `"X"` | `[["X","X"],["X","X"]]` | **CAUGHT** |
| 4 | I4 | CHK-04 | P1 | Helpers/JsonHelper.cs | XML→JSON emits empty | `["<note><to>Tove</to>…</note>"]` (fallback) | **CAUGHT** |
| 5 | I5 | CHK-05 | P1 | Helpers/JsonHelper.cs | `IsJson` always false | `["{\"k\":123}"]` (not passthrough) | **CAUGHT** |
| 6 | I6 | CHK-06 | P1 | Helpers/JsonHelper.cs | JSON line-fallback drops all lines | `[]` | **CAUGHT** |
| 7 | I7 | CHK-07 | P1 | ViewModels/OptionsViewModel.cs | AI gating guard returns `true` | `InputTxtBox IsEnabled=True` (expect False) | **CAUGHT** |
| 8 | I8 | CHK-08 | P2 | Helpers/ClipboardItemHelper.cs | preview Content hard-coded `"__CORRUPT__"` | preview `found=False` | **CAUGHT** |
| 9 | I9 | CHK-09 | P2 | Models/PasteFormats.cs | markdown flagged non-core (drops from list) | `plain=True md=False json=True` | **CAUGHT** |
| 10 | I10 | CHK-10 | P2 | Helpers/MarkdownHelper.cs | markdown strips all `*` (bold lost) | `# Title\n\nbold` (no `**`) | **CAUGHT** |

Each injected output is distinct from the clean baseline and pinpoints the seeded
fault, confirming the transform genuinely executed in the real app.

## Detection rate (c)

**10 / 10 injections caught** via real winappcli-driven execution, with per-run
rebuilds and reverts. Clean-build baseline = **10/10 PASS** (`baseline_clip.json`);
post-revert clean run = **10/10 PASS** (`clean_final.json`); every injected build
flipped exactly its mapped check to FAIL.

## Screenshots are real (d)

- Per-injection AP-window screenshots under `screenshots/inj-I1 … inj-I10/`
  (10 PNGs each, ~29–38 KB — real window content, not black frames).
- `screenshots/inj-I9/chk09-format-list.png` visibly shows the action list with
  **"Paste as plain text" (Ctrl+1)** and **"Paste as JSON" (Ctrl+2)** but **no
  "Paste as markdown"** — direct visual proof of that regression.
- Clean baselines in `screenshots/baseline_clip/` are mirrored into the skill's
  `assets/screenshots/`.

## Files delivered (e)

**Skill — `distilled-skills/powertoys-advancedpaste-signoff/`** (old MSTest /
reflection harness and `templates/SignoffTransformTests.cs` removed):

- `SKILL.md` — frontmatter (name, WHAT/WHEN); `## How to Sign Off`;
  `## App-Specific winappcli Logic` (ShowUI pipe summon, SendInput 40-byte INPUT
  struct, foreground-before-ShowUI, paste-to-target pattern **and** the
  locked-workstation produced-clipboard variant, invoke-by-name, clipboard-via-
  file); `## Coverage & Limits`; `## References`. (<500 lines.)
- `signoff-checklist.md` — declarative, 10 items P0/P1/P2, each Check / Drive /
  Verify.
- `assets/screenshots/*.png` — baseline images; `assets/advancedpaste.spec.json` —
  machine-runnable mirror.
- `scripts/` — `run_signoff.ps1` (paste-to-target), `run_signoff_clip.ps1`
  (locked-session produced-clipboard), `ap_controller.ps1`, `input_helpers.ps1`,
  `set_clipboard.ps1`, `verify_input.ps1`, `wait_input.ps1`, and the injection
  acceptance harness (`injections.ps1`, `run_injections.ps1`,
  `run_injections_clip.ps1`, `run_injections_uia.ps1`).
- `LICENSE.txt`.

**Proof — `benchmark/results/acc-advancedpaste/`:** `report.md` (this file),
`results.json` (10/10 table + produced outputs), `results_clip.json`,
`results_uia.json`, `baseline_clip.json/.md`, `clean_final.json/.md`,
`inj-I1..I10.json/.md`, and `screenshots/` (baseline_clip + inj-I1..I10 +
uia-injections).

## Cleanup

- All injections reverted via `git checkout`; `git -C C:\s\PowerToys status` clean.
- AP / controller processes started by the run are terminated. No git commit made
  in SkillForDistill.

## Confidence (f)

- **Detection — high, and proven 10/10 now.** Every injected fault was caught by
  driving the real app, with concrete distinct produced outputs and real
  screenshots, plus green clean-build baseline and post-revert runs.
- **One honest caveat:** verification read AdvancedPaste's produced clipboard
  (the bytes it would paste) rather than `get-value` on a pasted target, because
  the workstation is locked and `SendInput` is denied (outside the agent's
  control). This exercises the same real transform code path through the same real
  UI action; only the final keystroke delivery is substituted. `run_signoff.ps1`
  performs the literal paste-to-target verification once the session is unlocked.
