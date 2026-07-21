# AdvancedPaste — Skill-2 (winappcli/UIA) Sign-off Proof

**Module:** PowerToys `src/modules/AdvancedPaste` · **Built:** `C:\s\PowerToys\x64\Release`
**Skill:** `.github/skills/app-signoff-uia` (reuses `scripts/signoff.py` `build_report` / `report_to_markdown`)
**Date:** 2026-07-05 · **Desktop owner:** this session · **Result: GATE = PASS, 9/9 (clean), all 5 injected regressions detected, 0 false positives.**

---

## (a) How AdvancedPaste was launched / driven

Two real surfaces were exercised — the window **and** the behavioral core:

### The real window WAS launched directly (no global hotkey)
AdvancedPaste's window is **not** shown on plain `.exe` launch. Source proof
(`Program.cs`, `AdvancedPasteXAML/App.xaml.cs`): `OnLaunched` only parses `arg1`
(runner PID to watch) and `arg2` (a **named pipe** name), then
`NamedPipeProcessor.ProcessNamedPipeAsync` connects as a pipe **client** and reads
UTF-16 lines; the window appears only when it receives the `"ShowUI"` message
(`shared_constants.h: ADVANCED_PASTE_SHOW_UI_MESSAGE = L"ShowUI"`), which the C++
module normally sends in response to the **Win+Shift+V** global hotkey.

Over RDP the global hotkey cannot be synthesized (input-queue ownership / ACCESS_DENIED,
same blocker documented in the PowerAccent proof). **Workaround that succeeded:** I
impersonated the Runner side of the protocol — created a `NamedPipeServerStream`,
launched `PowerToys.AdvancedPaste.exe <livePid> <pipe>`, waited for the app to
connect, and wrote `"ShowUI\r\n"` (UTF-16). The real **"Advanced Paste"** window
opened (`WinUIDesktopWin32WindowClass`, e.g. HWND 3148190) and was fully driven with
`winapp ui` (`list-windows` / `inspect` / `search` / `get-property` / `screenshot`).
Launcher: [`launch_window.ps1`](./launch_window.ps1); evidence:
[`examples/window_inspect.json`](./examples/window_inspect.json),
[`examples/advancedpaste_window.png`](./examples/advancedpaste_window.png).

Live UIA facts asserted against the window (check `cap-window-uia`, **PASS**):
three core actions present — *"Paste as plain text (Ctrl+1)"*, *"Paste as markdown
(Ctrl+2)"*, *"Paste as JSON (Ctrl+3)"* (invokable `ListItem`s) — and the AI prompt
box `InputTxtBox` is **gated** (`IsEnabled=False`) because no AI provider/GPO is
configured, corroborating the distilled AI-gating design.

> The core paste actions were **not invoked** through the window: invoking them
> triggers the module's `SendInput`(Ctrl+V) injection, which is the exact RDP-blocked
> path. That value is instead covered at the behavioral layer below (real product code,
> deterministic, no synthetic input).

### Behavioral core — the primary, repeatable gate
The end-user value (paste-as plain-text / markdown / json) lives in `.NET` product
code that operates on `DataPackageView` clipboard data. I authored an MSTest class
[`SignoffTransformTests.cs`](./examples/SignoffTransformTests.cs) (added to the
module's own `AdvancedPaste.UnitTests`) that drives the **real product code**
(`TransformHelpers.TransformAsync`, `JsonHelper`, `MarkdownHelper`, `PasteFormats`
metadata) with in-process `DataPackage` inputs and pins exact outputs. It runs via
the module's Microsoft.Testing.Platform host (`AdvancedPaste.UnitTests.exe`), emits a
TRX, and the harness maps each capability → test outcome. Harness:
[`run_advancedpaste_signoff.py`](./run_advancedpaste_signoff.py) (kinds: `vstest`, `uia`).

---

## (b) Capability spec (P0 / P1 / P2)

Grounded in `distilled_v2/microsoft-PowerToys/AdvancedPaste.md`. Full spec:
[`advancedpaste.spec.json`](./advancedpaste.spec.json).

| ID | Pri | Capability | Product code exercised |
|----|-----|------------|------------------------|
| `cap-plaintext-strips` | **P0** | Paste-as-plain-text returns the plain payload, strips HTML formatting | `TransformHelpers.ToPlainTextAsync` → `GetTextOrEmptyAsync` |
| `cap-markdown-transform` | **P0** | Paste-as-markdown converts HTML (heading + bold) to Markdown | `MarkdownHelper.ToMarkdownAsync` (HtmlAgilityPack + ReverseMarkdown) |
| `cap-json-csv` | **P0** | Paste-as-json converts CSV → JSON array-of-arrays | `JsonHelper.ToJsonFromXmlOrCsvAsync` (CSV branch) |
| `cap-json-xml` | P1 | Paste-as-json converts XML → JSON | `JsonHelper` (`SerializeXmlNode`) |
| `cap-json-passthrough` | P1 | Already-valid JSON returned unchanged | `JsonHelper.IsJson` short-circuit |
| `cap-json-never-throws` | P1 | No-text clipboard → empty string (never throws) — PR #48124 reliability guardrail | `JsonHelper` text guard |
| `cap-window-uia` | P1 | Real window exposes 3 core actions; AI box gated when AI unconfigured | live WinUI window via `winapp ui` |
| `cap-format-metadata` | P2 | `PasteFormats` metadata = AI-gating source of truth (core/non-AI vs RequiresAIService) | `PasteFormats` + `PasteFormatMetadataAttribute` |
| `cap-hexcolor` | P2 | Clipboard hex-color detection for history preview | `ClipboardItemHelper.IsRgbHexColor` |

Gate rule: **all P0 must PASS**. P0 is intentionally the three flagship transforms.

---

## (c) GREEN baseline — confirmed (run twice)

| Run | Gate | Passed | Artifacts |
|-----|------|--------|-----------|
| baseline_run1 | **PASS** | 9/9 | [json](./baseline_run1.json) · [md](./baseline_run1.md) |
| baseline_run2 | **PASS** | 9/9 | [json](./baseline_run2.json) · [md](./baseline_run2.md) |
| clean_final (post-revert, UIA live) | **PASS** | 9/9 | [json](./clean_final.json) · [md](./clean_final.md) |

Underlying MSTest: 28/28 tests pass on the clean build (11 sign-off transform/metadata
+ 17 existing hex-color rows). Example green report:
[`examples/example_report_green_baseline.md`](./examples/example_report_green_baseline.md).

---

## (d) Regression detection

Each regression = a **minimal edit to PRODUCT source**, rebuild
(`AdvancedPaste.csproj` → shared `x64\Release`), run, observe the flip, then **revert**.
Regression runs used `--no-uia` (the live window runs the previous binary and cannot
reflect a source change; the UIA element-presence check is orthogonal to transform
logic). Detection = **only the mapped check flips; every other check stays green.**

| # | Product file | Injected fault | Target check (Pri) | Result | Collateral |
|---|--------------|----------------|--------------------|--------|-----------|
| R1 | `TransformHelpers.cs` | PlainText upper-cases output instead of verbatim | `cap-plaintext-strips` (P0) | ✅ flipped → **GATE FAIL** | none (8/8 others green) |
| R2 | `JsonHelper.cs` | CSV row parse drops first cell (`Skip(1)`) | `cap-json-csv` (P0) | ✅ flipped → **GATE FAIL** | none |
| R3 | `MarkdownHelper.cs` | Markdown output strips `#` (headings break) | `cap-markdown-transform` (P0) | ✅ flipped → **GATE FAIL** | none |
| R4 | `JsonHelper.cs` | No-text guard returns `"{}"` instead of `""` | `cap-json-never-throws` (P1) | ✅ flipped (gate stays PASS — P1, by design) | none |
| R5 | `PasteFormats.cs` | `PlainText.IsCoreAction` flipped to `false` | `cap-format-metadata` (P2) | ✅ flipped (only PlainText datarow) | none |

**Detection rate: 5/5 (100%).** Artifacts: `regression_R1..R5.{json,md}`. Example:
[`examples/example_report_regression_R2_json_csv.md`](./examples/example_report_regression_R2_json_csv.md).

R4 correctly demonstrates gate semantics: a **P1** failure is surfaced (check FAIL)
but does **not** fail the P0 release gate.

## (d′) Zero false positives on clean build

- Both baselines and the post-revert `clean_final` run: **9/9 PASS**.
- In every regression run, exactly one mapped check flipped; the other 7 behavioral
  checks stayed green — no spurious failures.
- Product tree reverted clean: `git status --porcelain -- src/modules/AdvancedPaste`
  shows **only** the additive test file `AdvancedPaste.UnitTests/SignoffTransformTests.cs`
  (no product-code diff; all 5 injections reverted and verified via git).

---

## (e) Blockers / downshifts (explicit, no fabrication)

1. **Global hotkey (Win+Shift+V) is blocked over RDP** — synthetic global input is
   ACCESS_DENIED (this session does not own the input queue). *Downshift:* summoned the
   window via its named-pipe `ShowUI` message instead of the hotkey. **Window path
   fully recovered** (real window opened + UIA-driven).
2. **Core paste actions not invoked through the window** — invoking them runs the
   module's `SendInput`(Ctrl+V) injection, the same blocked path, and needs a real
   focused foreground target. *Downshift:* covered the transform value at the
   behavioral layer against real product code (deterministic, no synthetic input).
3. **AI paste / custom actions not executed end-to-end** — require an OpenAI/Azure/etc.
   key + network (`KernelServiceIntegrationTests` are `[Ignore]`d for exactly this).
   *Covered indirectly:* AI-gating is asserted two ways — live (`InputTxtBox`
   disabled) and by source-of-truth metadata (`RequiresAIService`), which R5 proves is
   regression-sensitive. End-to-end model calls remain out of scope.
4. **Product DLL is locked while the window runs** — the live window loads
   `PowerToys.AdvancedPaste.dll`, so regression rebuilds require closing the window
   first. Handled in the loop (kill process → rebuild → run `--no-uia`).

---

## (f) Confidence: **HIGH** (for the covered surface)

- The end-user core value (paste-as plain-text / markdown / json, plus the JSON
  XML/CSV/passthrough variants and the "never throws" reliability contract) is
  exercised against **real, freshly-built product code** with pinned outputs; all 5
  regressions were caught with zero collateral, and the clean build is green twice.
- The **real window** was launched and UIA-driven, proving the front-end surface and
  the AI-gating state — not a mock.
- Scope honestly excludes: hotkey-triggered activation, `SendInput` paste injection,
  OCR/transcode, and live AI model calls (environment/credential-gated). Confidence on
  those specific paths is **not claimed**.

### Artifacts
`advancedpaste.spec.json` · `results.json` (=clean_final) · `baseline_run{1,2}.*` ·
`regression_R{1..5}.*` · `clean_final.*` · `run_advancedpaste_signoff.py` ·
`launch_window.ps1` · `rebuild_and_run.ps1` · `examples/` (window screenshot + inspect
JSON, sign-off test source, green + regression example reports).
