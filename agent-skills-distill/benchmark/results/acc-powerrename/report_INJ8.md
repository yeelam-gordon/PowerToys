# Sign-off Report — PowerToys PowerRename

- **Gate:** ✅ PASS (all P0 checks must PASS)
- **Generated:** 2026-07-05T11:51:07.897594+00:00
- **Target:** `{"window": "per-check", "app": "PowerToys.PowerRename"}`
- **Totals:** 9/10 passed, 1 failed

## Results by priority

| Priority | Passed | Failed | Total |
|----------|--------|--------|-------|
| P0 | 2 | 0 | 2 |
| P1 | 3 | 0 | 3 |
| P2 | 4 | 1 | 5 |

## P0 checks

### ✅ `p0-literal-replace-multi` — Literal (non-regex) search/replace renames every matched file and the preview + renamed-count label update: 'testCase' -> 'Renamed' yields Renamed1.txt, Renamed2.txt and the renamed counter (2).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-4011` | ✅ | elementId=txt-textbox-4011 |
| 1 | set-value | `txt-textbox-401c` | ✅ | elementId=txt-textbox-401c |
| 2 | search | `Renamed1.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "Renamed1.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fa... |
| 3 | search | `Renamed2.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "Renamed2.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fa... |
| 4 | search | `(2)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(2)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |

### ✅ `p0-regex-replace` — Regex vs literal dispatch: '^test.*\.txt$' -> 'matched.txt'. Literal mode matches nothing (renamed count (0)); enabling 'Use regular expressions' matches both testCase*.txt (matched.txt appears, renamed count (2)).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-2b03` | ✅ | elementId=txt-textbox-2b03 |
| 1 | set-value | `txt-textbox-2b0e` | ✅ | elementId=txt-textbox-2b0e |
| 2 | search | `(0)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(0)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |
| 3 | invoke | `checkBox_regex` | ✅ | pattern=TogglePattern; elementId=chk-checkboxregex-2b06 |
| 4 | search | `matched.txt` | ✅ | text='{"matchCount": 2, "hasMore": false, "matches": [{"type": "Text", "name": "matched.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fal... |
| 5 | search | `(2)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(2)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |

## P1 checks

### ✅ `p1-case-sensitive-toggle` — Case sensitivity: 'testcase1' -> 'match1'. By default (case-insensitive) it matches testCase1.txt (match1.txt appears, renamed count (1)); enabling 'Case sensitive' stops the match (renamed count (0)). Absence asserted positively via the count label.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-e457` | ✅ | elementId=txt-textbox-e457 |
| 1 | set-value | `txt-textbox-e462` | ✅ | elementId=txt-textbox-e462 |
| 2 | search | `match1.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "match1.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |
| 3 | search | `(1)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(1)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |
| 4 | invoke | `checkBox_case` | ✅ | pattern=TogglePattern; elementId=chk-checkboxcase-e45e |
| 5 | search | `(0)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(0)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |

### ✅ `p1-match-all-occurrences` — Match all occurrences: literal 't' -> 'f'. By DEFAULT only the first occurrence is replaced (festCase1.txt); enabling 'Match all occurrences' replaces every occurrence (fesfCase1.fxf). Verifies the default first-only path AND the flag.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-118d` | ✅ | elementId=txt-textbox-118d |
| 1 | set-value | `txt-textbox-1198` | ✅ | elementId=txt-textbox-1198 |
| 2 | search | `festCase1.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "festCase1.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": f... |
| 3 | invoke | `checkBox_matchAll` | ✅ | pattern=TogglePattern; elementId=chk-checkboxmatchal-1192 |
| 4 | search | `fesfCase1.fxf` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "fesfCase1.fxf", "className": "TextBlock", "isEnabled": true, "isOffscreen": f... |

### ✅ `p1-enumerate-counter-padding` — Enumeration counter token: regex '^testCase.*\.txt$' -> 'img_${padding=2}' produces the zero-padded, per-item incrementing counter img_00 and img_01. Catches both a broken counter increment and broken padding parsing.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `checkBox_regex` | ✅ | pattern=TogglePattern; elementId=chk-checkboxregex-e45a |
| 1 | set-value | `txt-textbox-e457` | ✅ | elementId=txt-textbox-e457 |
| 2 | set-value | `txt-textbox-e462` | ✅ | elementId=txt-textbox-e462 |
| 3 | search | `img_00` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "img_00", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "... |
| 4 | search | `img_01` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "img_01", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "... |

## P2 checks

### ✅ `p2-capture-groups` — Regex capture-group rewrite: '^(testCase)(\d)\.txt$' -> '$2_$1' transposes the groups to 1_testCase and 2_testCase.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `checkBox_regex` | ✅ | pattern=TogglePattern; elementId=chk-checkboxregex-2b06 |
| 1 | set-value | `txt-textbox-2b03` | ✅ | elementId=txt-textbox-2b03 |
| 2 | set-value | `txt-textbox-2b0e` | ✅ | elementId=txt-textbox-2b0e |
| 3 | search | `1_testCase` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "1_testCase", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |
| 4 | search | `2_testCase` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "2_testCase", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |

### ✅ `p2-uppercase-transform` — Uppercase text-formatting toggle uppercases the whole file name with no search term needed: testCase1.txt -> TESTCASE1.TXT (asserted case-exact via ci:false).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `toggleButton_upperCase` | ✅ | pattern=TogglePattern; elementId=btn-togglebuttonupp-e472 |
| 1 | search | `TESTCASE1.TXT` | ✅ | text='{"matchCount": 3, "hasMore": false, "matches": [{"type": "CheckBox", "name": "testCase1.txt", "automationId": "2", "className": "CheckBox", "isEnabled"... |

### ❌ `p2-lowercase-transform` — Lowercase text-formatting toggle lowercases the whole file name: SpecialCase.txt -> specialcase.txt (asserted case-exact via ci:false; the mixed-case original does not satisfy it).
> **Failure:** step 1 (search specialcase.txt): text='{"matchCount": 2, "hasMore": false, "matches": [{"type": "CheckBox", "name": "SpecialCase.txt", "automationId": "1", "className": "CheckBox", "isEnabled": true, "isOffscreen": false, "x": 1500, "y": 598, "width": 56, "height": 40, "selector": "1", "toggleState": "on", "isInvokable": true}, {"type": "Text", "name": "SpecialCase.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x": 1608, "y": 602, "width": 161, "height": 32, "selector": "lbl-specialcasetxt-ca8b", "isInvokable": false}]}' | contains 'specialcase.txt': NO

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `toggleButton_lowerCase` | ✅ | pattern=TogglePattern; elementId=btn-togglebuttonlow-ca68 |
| 1 | search | `specialcase.txt` | ❌ | text='{"matchCount": 2, "hasMore": false, "matches": [{"type": "CheckBox", "name": "SpecialCase.txt", "automationId": "1", "className": "CheckBox", "isEnable... |

### ✅ `p2-titlecase-transform` — Title-case text-formatting toggle capitalizes the first letter of each word: report_2020.log -> Report_2020.log (asserted case-exact via ci:false).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `toggleButton_titleCase` | ✅ | pattern=TogglePattern; elementId=btn-togglebuttontit-0832 |
| 1 | search | `Report_2020.log` | ✅ | text='{"matchCount": 3, "hasMore": false, "matches": [{"type": "CheckBox", "name": "report_2020.log", "automationId": "0", "className": "CheckBox", "isEnable... |

### ✅ `p2-capitalize-transform` — Capitalize-each-word text-formatting toggle capitalizes the first letter and lowercases the rest of each word: SpecialCase.txt -> Specialcase.txt (asserted case-exact via ci:false; the mixed-case original does not satisfy it).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `toggleButton_capitalize` | ✅ | pattern=TogglePattern; elementId=btn-togglebuttoncap-1d96 |
| 1 | search | `Specialcase.txt` | ✅ | text='{"matchCount": 3, "hasMore": false, "matches": [{"type": "CheckBox", "name": "SpecialCase.txt", "automationId": "1", "className": "CheckBox", "isEnable... |
