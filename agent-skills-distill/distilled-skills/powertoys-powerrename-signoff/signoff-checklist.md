# PowerRename UI Sign-off Checklist (winappcli)

Declarative P0/P1/P2 sign-off for the real **PowerToys PowerRename** WinUI 3 rename
window (`PowerToys.PowerRename.exe`). Every item is driven and verified through
`winapp ui` against the live app. **Non-destructive** — every Verify reads the live
*preview*; **Apply is never clicked**, so the sample files are never renamed on disk.

This checklist is the **source of truth**. The optional machine-runnable mirror is
[`assets/powerrename.spec.json`](./assets/powerrename.spec.json) — it matches this
document item-for-item. App-specific launch/driving logic (non-destructive launch,
per-session hashed text-box slugs, count-label assertions, `ci:false`, fresh app per
check) is documented in [`SKILL.md`](./SKILL.md) › *App-Specific winappcli Logic*.

## Setup (once per run)

- **Sample files** (auto-created by the runner in a temp folder):
  `testCase1.txt`, `testCase2.txt`, `SpecialCase.txt`, `report_2020.log`.
- **Launch (non-destructive):**
  `PowerToys.PowerRename.exe "<…>\testCase1.txt" "<…>\testCase2.txt" "<…>\SpecialCase.txt" "<…>\report_2020.log"`
  (file-path args, **no** `\\.\pipe\` token ⇒ the app's UI-test path). Window:
  `HWND … "PowerRename" [WinUIDesktopWin32WindowClass]`; drive with `-w <HWND>`.
- **Fresh instance per item** — toggles expose only the UIA Toggle pattern (no
  set-state), so each item launches a new app so flags start all-off.
- **Slug resolution** — the two text boxes are `txt-textbox-XXXX` with a hash that
  changes every launch. Resolve at runtime:
  `winapp ui search "Search for" -w <HWND> --json` → the match with `type=="Edit"` →
  its `.selector`. Below, `<SEARCH>` / `<REPLACE>` denote those resolved selectors
  (spec placeholders `__SEARCH_SLUG__` / `__REPLACE_SLUG__`).
- **Baseline screenshots** live in `assets/screenshots/` (written at sign-off run time);
  capture with `winapp ui screenshot <selector> -w <HWND>` (add `--capture-screen`
  over RDP). See *Screenshot note* at the bottom.

Legend — each item: **Check** (capability) / **Drive** (`winapp ui` steps+selectors) /
**Verify** (`winapp ui` read + baseline screenshot). Gate: **all P0 must pass**.

---

## P0 — Release gate (core rename engine)

### P0-1 · `p0-literal-replace-multi` — Literal search/replace renames all matches
- **Check:** A literal (non-regex) find/replace renames every matched file and the
  preview + renamed-count label update.
- **Drive:**
  1. `winapp ui set-value <SEARCH> --value "testCase" -w <HWND>`
  2. `winapp ui set-value <REPLACE> --value "Renamed" -w <HWND>`
- **Verify:**
  - `winapp ui search "Renamed1.txt" -w <HWND>` ⇒ contains `Renamed1.txt`
  - `winapp ui search "Renamed2.txt" -w <HWND>` ⇒ contains `Renamed2.txt`
  - `winapp ui search "(2)" -w <HWND>` ⇒ renamed-count label contains `(2)`
  - Baseline: `assets/screenshots/p0-literal-replace-multi.png`

### P0-2 · `p0-regex-replace` — Regex vs literal dispatch
- **Check:** With *Use regular expressions* OFF a regex pattern matches nothing; turning
  it ON makes the same pattern match.
- **Drive:**
  1. `set-value <SEARCH> --value "^test.*\.txt$"`
  2. `set-value <REPLACE> --value "matched.txt"`
  3. (assert literal miss) then `invoke checkBox_regex`
- **Verify:**
  - Before toggle: `search "(0)"` ⇒ renamed-count `(0)` (literal mode matches nothing)
  - After `invoke checkBox_regex`: `search "matched.txt"` ⇒ contains `matched.txt`
  - `search "(2)"` ⇒ renamed-count `(2)`
  - Baseline: `assets/screenshots/p0-regex-replace.png`

---

## P1 — Important behaviors

### P1-1 · `p1-case-sensitive-toggle` — Case sensitivity flag
- **Check:** By default matching is case-insensitive; enabling *Case sensitive* stops a
  differently-cased match.
- **Drive:**
  1. `set-value <SEARCH> --value "testcase1"`
  2. `set-value <REPLACE> --value "match1"`
  3. (assert default match) then `invoke checkBox_case`
- **Verify:**
  - Default: `search "match1.txt"` ⇒ contains `match1.txt`; `search "(1)"` ⇒ `(1)`
  - After `invoke checkBox_case`: `search "(0)"` ⇒ renamed-count `(0)` (absence asserted
    positively via the count label)
  - Baseline: `assets/screenshots/p1-case-sensitive-toggle.png`

### P1-2 · `p1-match-all-occurrences` — Match-all vs first-only
- **Check:** By default only the first occurrence is replaced; *Match all occurrences*
  replaces every occurrence.
- **Drive:**
  1. `set-value <SEARCH> --value "t"`
  2. `set-value <REPLACE> --value "f"`
  3. (assert first-only) then `invoke checkBox_matchAll`
- **Verify:**
  - Default: `search "festCase1.txt"` ⇒ contains `festCase1.txt` (first `t` only)
  - After `invoke checkBox_matchAll`: `search "fesfCase1.fxf"` ⇒ contains `fesfCase1.fxf`
  - Baseline: `assets/screenshots/p1-match-all-occurrences.png`

### P1-3 · `p1-enumerate-counter-padding` — Enumeration counter token
- **Check:** The `${padding=N}` counter token produces a zero-padded, per-item
  incrementing index.
- **Drive:**
  1. `invoke checkBox_regex`
  2. `set-value <SEARCH> --value "^testCase.*\.txt$"`
  3. `set-value <REPLACE> --value "img_${padding=2}"`
- **Verify:**
  - `search "img_00"` ⇒ contains `img_00` (first item, padded)
  - `search "img_01"` ⇒ contains `img_01` (counter incremented)
  - Baseline: `assets/screenshots/p1-enumerate-counter-padding.png`

---

## P2 — Secondary transforms

### P2-1 · `p2-capture-groups` — Regex capture-group rewrite
- **Check:** Capture groups can be referenced/transposed in the replacement.
- **Drive:**
  1. `invoke checkBox_regex`
  2. `set-value <SEARCH> --value "^(testCase)(\d)\.txt$"`
  3. `set-value <REPLACE> --value "$2_$1"`
- **Verify:**
  - `search "1_testCase"` ⇒ contains `1_testCase`
  - `search "2_testCase"` ⇒ contains `2_testCase`
  - Baseline: `assets/screenshots/p2-capture-groups.png`

### P2-2 · `p2-uppercase-transform` — Uppercase toggle
- **Check:** The *uppercase* text-formatting toggle uppercases the whole file name (no
  search term needed).
- **Drive:** `invoke toggleButton_upperCase`
- **Verify:** `search "TESTCASE1.TXT"` ⇒ contains `TESTCASE1.TXT`, **`ci:false`**
  (exact-case; the mixed-case original does not satisfy it).
  Baseline: `assets/screenshots/p2-uppercase-transform.png`

### P2-3 · `p2-lowercase-transform` — Lowercase toggle
- **Check:** The *lowercase* toggle lowercases the whole file name.
- **Drive:** `invoke toggleButton_lowerCase`
- **Verify:** `search "specialcase.txt"` ⇒ contains `specialcase.txt`, **`ci:false`**.
  Baseline: `assets/screenshots/p2-lowercase-transform.png`

### P2-4 · `p2-titlecase-transform` — Title-case toggle
- **Check:** The *title case* toggle capitalizes the first letter of each word.
- **Drive:** `invoke toggleButton_titleCase`
- **Verify:** `search "Report_2020.log"` ⇒ contains `Report_2020.log`, **`ci:false`**.
  Baseline: `assets/screenshots/p2-titlecase-transform.png`

### P2-5 · `p2-capitalize-transform` — Capitalize-each-word toggle
- **Check:** The *capitalize* toggle capitalizes the first letter and lowercases the
  rest of each word.
- **Drive:** `invoke toggleButton_capitalize`
- **Verify:** `search "Specialcase.txt"` ⇒ contains `Specialcase.txt`, **`ci:false`**
  (distinguishes from the mixed-case `SpecialCase.txt`).
  Baseline: `assets/screenshots/p2-capitalize-transform.png`

---

## Gate rule

All **P0** checks (`p0-literal-replace-multi`, `p0-regex-replace`) must pass or the
sign-off **FAILs** (runner exit code `1`). P1/P2 failures are reported but do not block
the gate.

## Screenshot note (environment honesty)

Baselines are captured with real `winapp ui screenshot` calls and, when captured from
a **connected** session, render the full WinUI client area (Original/Renamed preview
columns, flag checkboxes, transform popup). PowerRename is
WinUI 3 (DirectComposition / swap-chain rendered); when the interactive session is
**RDP-disconnected**, that surface is not composited, so a screenshot taken in that
state captures window chrome only. Either way the sign-off's behavioral verdicts hold —
they come from the `winapp ui search`/`get-value` **reads**, which are unaffected.
Re-capture baselines from a connected session for pixel-level visual diffing. See
*Coverage & Limits* in [`SKILL.md`](./SKILL.md).
