# Skill Testing Report — SkillForDistill

Rigorous, execution-based verification of every distilled deliverable. No claim below is
asserted from the skill text alone; each was checked against **live GitHub**, the **raw mined
JSON**, and the **PowerToys source at `C:\s\PowerToys`**, or against **real winappcli fault-injection runs**.

## Method

Fresh, independent verifier sub-agents (Opus, no authoring context — self-review is disallowed):
- **5 grounding verifiers** — 30 knowledge skills, 6 per agent. For every skill: extract every
  `#NNNN`/PR/issue citation → confirm it exists on GitHub (200 + topical title match), confirm it
  appears in the module's mined `raw/*/{prs,issues}.json`, and confirm every Module-Map file path
  and named symbol resolves in source. Spot-check the highest-stakes security/quality claims
  line-for-line against source or the cited PR's file list.
- **1 sign-off structural verifier** — 3 sign-off skills. Confirm the declarative checklist is
  verifiable (concrete expected values, P0/P1/P2), every UI selector resolves to a real
  `x:Name`/`.resw` string in module source, the winappcli launch logic points at a real binary,
  and the fault-injection acceptance artifacts exist.
- **1 re-verifier** — re-checked the one skill that came back WEAK after its fix.

## Result — Knowledge skills (30)

**29/30 PASS on first pass, 1 WEAK → fixed → 30/30. Citations: 873 checked, 872 valid live (99.9%); after fix, 873/873.**

| # | Skill | Citations (valid/checked) | Score | Note |
|---|-------|---------------------------|-------|------|
| 1 | powerrename | 32/32 | PASS | SanitizeAndNormalize + 2-pass NormalizeString confirmed |
| 2 | poweraccent | 43/43 | PASS* | *#47085 mis-bucket removed (glyph issue ≠ ALL-sentinel bug) |
| 3 | advancedpaste | 60/60 | PASS | try_inject_modifier_key_up + PasswordVault confirmed |
| 4 | alwaysontop | 26/26 | PASS | owner tag 0x414F5450 confirmed |
| 5 | awake | 33/33 | PASS | ComputeAwakeState ES_* flags verbatim |
| 6 | cmdnotfound | 31/31 | PASS | WinGet.Client floor 1.8.1133 confirmed |
| 7 | cmdpal | 40/40 | PASS | DI-cycle fix matched to PR #49095 file list |
| 8 | colorpicker | 37/37 | PASS | HDR sRGB-capture limitation framed accurately |
| 9 | cropandlock | 27/27 | PASS | 3-mode architecture accurate |
| 10 | environmentvariables | 30/30 | PASS | #49069 correctly shared w/ FileLocksmith |
| 11 | fancyzones | 27/27 | PASS | 4 teardown races each traced to real source construct |
| 12 | filelocksmith | 27/27 | PASS | IPC binary-mode + is_open fix confirmed |
| 13 | grabandmove | 34/34 | PASS | CppWinRT 2.0.250303.1 pin verified |
| 14 | hosts | 28/28 | PASS | MaxHostsCount=9; mining noise honestly labeled |
| 15 | imageresizer | 22/22 | PASS | forceFresh JPEG fresh-encode invariant |
| 16 | keyboardmanager | 29/29 | PASS | AltGr + WM_SYSKEY confirmed |
| 17 | lightswitch | 39/39 | PASS | sunrise/coords/theme-scheduler symbols confirmed |
| 18 | mouseutils | 14/14 | PASS | 4 sub-utilities scoped accurately |
| 19 | mousewithoutborders | 31/31 | PASS | Encryption class confirmed |
| 20 | newplus | 18/18 | PASS | win10 shell-ext cpp path verified |
| 21 | peek | 26/26 | PASS | PreviewerFactory confirmed |
| 22 | powerdisplay | 42/42 | PASS | largest module; DDC/MCCS symbols confirmed |
| 23 | powertoysrun | 21/21 | PASS | StringMatcher / Wox.* paths resolve |
| 24 | previewpane | 18/18 | PASS | older SVG cites API-verified real |
| 25 | registrypreview | 9/9 | PASS | in-source `#36629` comment confirmed |
| 26 | screenruler | 21/21 | PASS | 3 bug claims (clamp/0xFF/mm-100x) line-for-line |
| 27 | shortcutguide | 29/29 (after fix) | PASS | **fixed**: #48547→#49131 (see below) |
| 28 | textextractor | 25/25 | PASS | PowerOCR/TextExtractor product split correct |
| 29 | workspaces | 23/23 | PASS | 4-process architecture confirmed |
| 30 | zoomit | 31/31 | PASS | XOR hotkey derivation line-for-line |

### The one real defect the testing caught — and the fix

**shortcutguide-knowledge (WEAK → PASS).** The "empty-title startup fault" playbook cited
issue **#48547** as its evidence. Live API check: **#48547 is "EnviromentVariable crush when run
as Admin" (Product-Environment Variables)** — an unrelated module. The fix PR it pointed to
(**#49069** "Guard TitleBar windows against an empty window title") is *correct* and legitimately
carries the `Product-Shortcut Guide` label (a multi-module guard).

Fix applied: swapped the wrong **#48547** for **#49131** ("keyboard overview opens empty,
immediately closes" — `Product-Shortcut Guide`), the closest real SG symptom, keeping the already-correct
launch-crash issues **#48170**/**#48638** and fix **#49069**. Corrected in both `SKILL.md` and
`references/regression-catalog.md`.

**poweraccent-knowledge (minor).** #47085 ("Y-Umlaut not available for Welsh/Hungarian/French")
was grouped under the "first-run All-available selects only SPECIAL" playbook. That playbook's
real evidence is #47113 → fix #47117; #47085 is a distinct glyph-completeness issue. Removed the
mis-bucketed #47085 from the SKILL, catalog, and bug-triage template; #47113→#47117 retained.

## Result — Sign-off skills (3)

**3/3 PASS, high confidence, zero invented selectors.** Every UI selector the checklists drive
resolves to a real control in module source:

| Skill | Checklist | Selectors (found/checked) | winappcli logic | Acceptance |
|-------|-----------|---------------------------|-----------------|------------|
| powerrename-signoff | 10 declarative (P0/P1/P2) | 9/9 in `MainWindow.xaml` + `.resw` | real exe; launch branch matches `App.xaml.cpp::OnLaunched` line-for-line | **10/10 injections caught, 0 false positives** |
| advancedpaste-signoff | 10 declarative | 4/4 module (+1 external Notepad) — `InputTxtBox`, ShowUI pipe confirmed | real exe; named-pipe ShowUI + Ctrl+V-into-Notepad matches source | **10/10 injections caught, 0 false positives** |
| poweraccent-signoff | 20 declarative (spec.json) | 6/6 — French glyph arrays match `CharacterMappings.cs` char-for-char | honest downshift to glyph/lifecycle executors; RDP synthetic-input limit disclosed | 20/20 stable + 5/5 injected caught |

## Acceptance proof — inject 10, catch 10

See [`ACCEPTANCE-10x10.md`](./ACCEPTANCE-10x10.md). Catch criterion = a checklist item flips to
FAIL vs. the clean baseline (the P0 gate is stricter and only trips on P0 items, so a caught P1/P2
regression can still show `gate=PASS` while being detected).

- **Clean baseline (false-positive control): AdvancedPaste 0 failures, PowerRename 0 failures.**
- **AdvancedPaste: 10/10 injections detected** (I1→CHK-01 … I10→CHK-10).
- **PowerRename: 10/10 injections detected** (each INJ flips its targeted check).
- PowerAccent: overlay summon needs an unlocked input-owning session (RDP-locked at test time);
  the data/enum/lifecycle surface that *feeds* the overlay is covered instead — disclosed, not hidden.

## Bottom line

- **Knowledge skills are grounded, not fabricated:** 873 citations, 99.9% valid on first pass;
  the single wrong link and one mis-bucket were caught by the testing and fixed → 30/30 grounded.
- **Sign-off skills drive real UI:** every selector proven in source; both UIA-drivable modules
  hit the 10/10 injection bar with a clean-baseline control.
- **The verifiers were adversarial and independent** (fresh Opus agents, no authoring context),
  which is why they surfaced a defect the mechanical validator (structure-only) could not.
