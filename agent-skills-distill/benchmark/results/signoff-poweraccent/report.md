# Sign-off Proof — PowerAccent (Quick Accent) — Skill-2 (winappcli UI sign-off)

**Target:** microsoft/PowerToys module **PowerAccent (Quick Accent)** — built at `C:\s\powertoys\x64\Release\`.
**Skill:** `.github/skills/app-signoff-uia` (reused `scripts/signoff.py` `build_report` / `report_to_markdown`).
**Date:** 2026-07-05 (UTC). **Mode:** REAL execution, honest numbers, fail-fast.

**Bottom line:** The intended winappcli **UIA overlay-summon** path is **BLOCKED in this session by OS-level synthetic-input denial** (not a code issue — proven below). I **downshifted** to a real, fully-executed behavioral + lifecycle sign-off of the exact surface that *feeds and hosts* the overlay: the module's own MSTest binaries, a reflection-driven glyph-assertion driver over the freshly-built `PowerAccent.Common.dll`, and real launch/enable/single-instance/clean-exit lifecycle of `PowerToys.PowerAccent.exe`. **20/20 GREEN (stable x2 + a final x2), 5/5 injected regressions detected, 0 false positives, PowerToys tree clean.**

---

## (a) How I enabled & triggered Quick Accent under automation — and the blocker

### What Quick Accent needs to show its overlay
From the distilled map + source: `PowerToys.PowerAccent.exe` installs a **global `WH_KEYBOARD_LL` hook** (`KeyboardListener.cpp`) and a **transient non-activating overlay** (`MainWindow`, shown `SW_SHOWNA`, title "Quick Accent", `ListView` AutomationId `QuickAccentCharacterList`). The overlay only materializes when the user **holds an accent-capable letter and presses the activation key** (Space/arrows). The hook does **not** filter injected input (no `LLKHF_INJECTED` check), so *in principle* `SendInput` would drive it.

### Enable/launch: WORKS under automation ✅
- Launching `PowerToys.PowerAccent.exe` directly starts the module: it acquires the `"QuickAccent"` single-instance mutex, initializes `SettingsService` (materializes `%LOCALAPPDATA%\Microsoft\PowerToys\QuickAccent\settings.json`), installs the hook, and waits on the named exit event `Local\PowerToysPowerAccentExitEvent-53e93389-…`. This is exercised for real by the three **lifecycle** checks below.

### Trigger (summon overlay): BLOCKED ❌ — synthetic input denied
I built a complete x64 `SendInput` trigger harness (`keydriver.py` + `edithost.py`) implementing the exact *hold-letter → press-activation → read candidates* sequence. **All synthetic input is denied by the OS in this session.** Evidence (`probe_inject.py`):

| Probe | Result |
|-------|--------|
| `keybd_event` | `GetLastError = 5` (ERROR_ACCESS_DENIED) |
| `SendInput` | returns `0` injected, `GetLastError = 5` |
| `BlockInput(FALSE)` | `GetLastError = 5` |
| `GetForegroundWindow()` | `0` (no foreground) |
| `GetInputState()` | `0` |

Ruled out: **not** elevation (every process is Medium IL 0x2000), **not** desktop mismatch (input desktop = `Default`, all threads on `Default`), **no** High-IL windows exist. Root cause: this is an **RDP automation session (session 2)** that does **not own the physical input queue** (interactive console = session 1); Windows blocks synthetic keystroke injection from a session that isn't the input owner. `winappcli` v0.4.0 has **no** key/type/send verb, and its `set-value` writes text **directly into the control, bypassing the low-level hook**, so it cannot summon the overlay either. Until injection is permitted (run on the console session / input-owning session), the overlay cannot be summoned and therefore cannot be UIA-verified. The harness is complete and ready to run the moment injection is available.

**This is an honest downshift, not a workaround or a fabrication.** No overlay result is claimed.

---

## (b) Capability spec (P0 / P1 / P2)

Grounded in `distilled_v2/microsoft-PowerToys/PowerAccent.md` (feature→file map + regression history). Full machine-readable spec: **`poweraccent.spec.json`** (20 checks). Each check runs against **real freshly-built binaries** via one of three real executors: `vstest` (the module's own MSTest DLLs), `glyph` (`GlyphDriver.exe` reflection-loading the built `PowerAccent.Common.dll` and pinning **exact** glyph sets), `lifecycle` (real `PowerToys.PowerAccent.exe`).

### P0 — gate (all must pass)
| id | capability | executor |
|----|-----------|----------|
| `candidates-populated` | Accent-capable key → non-empty candidate list; unmapped key / no languages → empty (the data the overlay renders) | vstest |
| `glyph-fr-a-exact` | French `a` → exactly `[à â á ä ã æ]` in order | glyph |
| `glyph-fr-e-exact` | French `e` → exactly `[é è ê ë €]` in order | glyph |
| `glyph-fr-c-exact` | French `c` → exactly `[ç]` | glyph |
| `lifecycle-launch-enable` | Enabling launches the exe, which stays resident and materializes its settings | lifecycle |

### P1 — important
| id | capability | executor |
|----|-----------|----------|
| `candidates-dedup` | Same glyph across languages shown once | vstest |
| `candidates-ordering` | Candidates ordered by language DisplayOrder; single language yields only its glyphs | vstest |
| `charset-all-expansion` | "ALL" expands to every language once; stable cached union | vstest |
| `enum-lockstep-native-managed` | Managed `LetterKey` matches native WinRT enum (names + values) | vstest |
| `language-metadata-integrity` | Every language entry well-formed & present in order maps | vstest |
| `language-alphabetical` | Spoken languages alphabetical by display name | vstest |
| `unknown-language-throws` | Unknown language fails fast (no silent-wrong candidates) | vstest |
| `glyph-cur-e-euro` | Currency-only language: `e` → exactly `[€]` | glyph |
| `glyph-all-a-contains-common` | All-languages `a` includes common Latin accents | glyph |
| `lifecycle-single-instance` | 2nd instance detects mutex and self-exits; one resident owner | lifecycle |
| `lifecycle-clean-exit` | `POWERACCENT_EXIT_EVENT` → clean shutdown (hook uninstalled) | lifecycle |

### P2 — nice-to-have
| id | capability | executor |
|----|-----------|----------|
| `positioning-dpi1` | 100% DPI: overlay anchors land at expected coords | vstest |
| `positioning-dpi-scaling` | 150% DPI: overlay footprint scales, stays on-screen | vstest |
| `positioning-multimonitor` | Honors active-monitor origin (offset & negative-origin) | vstest |
| `caret-placement` | Caret-follow: centers above caret, clamps at edges, flips below | vstest |

---

## (c) GREEN baseline confirmed? ✅ YES — stable

Clean build, no source changes: **20/20 PASS, GATE=PASS**, run **4 times total** (twice up-front for flakiness, twice again after the regression campaign) — **identical every time, zero flakiness**. Underlying real executions on clean build: `PowerAccent.Common.UnitTests` 20/20, `PowerAccent.Core.UnitTests` 21/21, glyph driver 5/5, lifecycle 3/3.
Artifacts: `examples/baseline-green.report.md`, `examples/baseline-green.results.json`.

---

## (d) Per-regression detection table

Each regression = a minimal real source edit → single-project rebuild (VsDevCmd x64 + msbuild) → re-run signoff → **REVERTED** → rebuilt clean.

| # | Injected regression (file) | Mapped check (priority) | Result | Checks that flipped | Gate |
|---|----------------------------|-------------------------|--------|---------------------|------|
| R1 | Remove `.Distinct()` in `CharacterMappings.Collect` (`CharacterMappings.cs`) | `candidates-dedup` (P1) | ✅ detected | `candidates-dedup`, `candidates-ordering`* | PASS (P1 fail) |
| R2 | Reverse candidate ordering (`OrderBy`→`OrderByDescending`) | `candidates-ordering` (P1) | ✅ detected | `candidates-ordering` | PASS (P1 fail) |
| R3 | Remove `"é"` from French `VK_E` set | `glyph-fr-e-exact` (P0) | ✅ detected | `glyph-fr-e-exact` | **FAIL** (P0) |
| R4 | Drop DPI factor in `Calculation.GetRawCoordinatesFromPosition` (`Calculation.cs`) | `positioning-dpi-scaling` (P2) | ✅ detected | `positioning-dpi-scaling`, `positioning-multimonitor`* | PASS (P2 fail) |
| R5 | Change `LetterKey.VK_A` value `0x41`→`0xEE` (`LetterKey.cs`) | `enum-lockstep-native-managed` (P1) | ✅ detected | `enum-lockstep-native-managed` | PASS (P1 fail) |

`*` collateral flips — genuine behavioral consequences of the same defect (R1's duplicates also break the order assertion; R4's dropped DPI factor also shifts the offset-monitor Center anchor), not false positives.

**Detection rate: 5 / 5 = 100%.** Every regression flipped its mapped check; each was reverted and the tree rebuilt clean.

**Honesty note on R5:** my first R5 value (`0x42`) *collided* with `VK_B` and tripped analyzer-as-error (`CA1069`/`CA2244`), so the rebuild **failed** — which the harness would also surface (no refreshed DLL). I switched to a non-colliding wrong value (`0xEE`) to get a clean **runtime** lockstep flip, which is the capability under test. Evidence per regression: `examples/regression-R{1..5}-*.results.json` (+ `regression-R3-glyph.report.md`).

---

## (e) Blockers / downshifts (explicit)

1. **BLOCKER — overlay cannot be summoned under automation.** Synthetic input (`SendInput`/`keybd_event`/`BlockInput`) is denied (ERROR_ACCESS_DENIED) because this RDP session doesn't own the input queue; `winappcli` has no key-send verb and `set-value` bypasses the hook. → The **P0 end-user assertion** "hold letter + activation shows overlay with correct candidates and inserts the selected glyph" **cannot be UIA-verified here.** Not fabricated.
2. **DOWNSHIFT (sanctioned).** Signed off the reachable surface that determines overlay correctness:
   - **Candidate data & glyph correctness** — the exact list the overlay would render (glyph driver pins specific accents; the module's own tests are data-invariant and would *miss* a removed glyph — R3 proves the driver catches it).
   - **Native/managed key lockstep** — guarantees the right physical key triggers the right candidates.
   - **DPI / multi-monitor / caret positioning** — where the overlay appears.
   - **Launch / enable / single-instance / clean-exit lifecycle** — real process, real mutex, real `POWERACCENT_EXIT_EVENT`.
3. **What is NOT covered** (be explicit): actual hook interception of a real keypress, the 300 ms `InputTime` render defer, key-up commit/insert (backspace-base + insert-glyph), Shift/arrow navigation *through the visible overlay*, press-and-hold mode, and excluded-app suppression — all require summoning the live overlay, which is blocked. The trigger harness (`keydriver.py`, `edithost.py`) is complete and ready to exercise these once run on an input-owning session.

---

## (f) Confidence

- **Downshifted behavioral + lifecycle sign-off: HIGH.** Real freshly-built binaries, the module's real MSTest suites, a real reflection driver over the real data DLL, real process lifecycle; GREEN is stable across 4 runs with 0 false positives; 5/5 injected regressions detected with correct gate behavior; every rebuild via the sanctioned single-module msbuild path; PowerToys tree verified clean (`git status` empty).
- **Full end-user overlay (UIA-through-winappcli) sign-off: NOT ESTABLISHED in this environment — LOW/blocked**, purely due to OS input-injection denial, with concrete evidence. No overlay results are claimed or fabricated.

---

## Reproduce

```powershell
# baseline (build test projects first if needed via rebuild.ps1 -Proj common/core)
$py = "C:\Users\yeelam\AppData\Local\Programs\Python\Python312\python.exe"
& $py .\run_poweraccent_signoff.py      # -> results.json + report_generated.md, exit 0 on GATE=PASS
```

### Files in this folder
| File | Purpose |
|------|---------|
| `poweraccent.spec.json` | P0/P1/P2 capability spec (20 checks) |
| `run_poweraccent_signoff.py` | Harness: vstest + glyph + lifecycle → report (reuses skill's `build_report`/`report_to_markdown`) |
| `glyphdriver/GlyphDriver.cs` (+ `.csproj`) | Reflection glyph-assertion driver over built `PowerAccent.Common.dll` |
| `rebuild.ps1` | Single-module rebuild (VsDevCmd x64 + msbuild) |
| `results.json` / `report_generated.md` | Latest run output (clean GREEN) |
| `examples/baseline-green.*` | GREEN proof |
| `examples/regression-R{1..5}-*.*` | Per-regression proof (each mapped check flipped) |
| `probe_inject.py` | **Injection-denial evidence** (section a) |
| `keydriver.py`, `edithost.py` | Complete SendInput overlay-trigger harness (blocked by env, ready to run) |
