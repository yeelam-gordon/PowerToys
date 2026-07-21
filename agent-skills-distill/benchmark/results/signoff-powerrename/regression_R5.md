# Sign-off Report — PowerToys PowerRename

- **Gate:** ✅ PASS (all P0 checks must PASS)
- **Generated:** 2026-07-05T06:51:26.502366+00:00
- **Target:** `{"window": "per-check", "app": "PowerToys.PowerRename"}`
- **Totals:** 7/8 passed, 1 failed

## Results by priority

| Priority | Passed | Failed | Total |
|----------|--------|--------|-------|
| P0 | 2 | 0 | 2 |
| P1 | 3 | 0 | 3 |
| P2 | 2 | 1 | 3 |

## P0 checks

### ✅ `p0-literal-replace-multi-preview` — Literal (non-regex) search/replace renames all matched files and the preview updates: 'testCase' -> 'Renamed' yields Renamed1.txt and Renamed2.txt

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-feb3` | ✅ | elementId=txt-textbox-feb3 |
| 1 | set-value | `txt-textbox-febe` | ✅ | elementId=txt-textbox-febe |
| 2 | search | `Renamed1.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "Renamed1.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fa... |
| 3 | search | `Renamed2.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "Renamed2.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fa... |

### ✅ `p0-literal-replace-distinct-file` — Literal search/replace on a distinct file: 'Special' -> 'General' yields GeneralCase.txt in the preview

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-f731` | ✅ | elementId=txt-textbox-f731 |
| 1 | set-value | `txt-textbox-f73c` | ✅ | elementId=txt-textbox-f73c |
| 2 | search | `GeneralCase.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "GeneralCase.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen":... |

## P1 checks

### ✅ `p1-regex-replace` — Regex vs literal dispatch: '^test.*\.txt$' -> 'matched.txt'. In literal mode nothing matches (renamed count (0)); enabling 'Use regular expressions' matches both testCase*.txt (renamed count (2), matched.txt appears). Absence is asserted positively via the renamed-count label to avoid the winapp 'search' non-zero exit on 0 matches.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-ea61` | ✅ | elementId=txt-textbox-ea61 |
| 1 | set-value | `txt-textbox-ea6c` | ✅ | elementId=txt-textbox-ea6c |
| 2 | search | `(0)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(0)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |
| 3 | invoke | `checkBox_regex` | ✅ | pattern=TogglePattern; elementId=chk-checkboxregex-ea64 |
| 4 | search | `matched.txt` | ✅ | text='{"matchCount": 2, "hasMore": false, "matches": [{"type": "Text", "name": "matched.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fal... |
| 5 | search | `(2)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(2)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |

### ✅ `p1-case-sensitive-toggle` — Case sensitivity: 'testcase1' -> 'match1'. By default (case-insensitive) it matches testCase1.txt (match1.txt appears, renamed count (1)); enabling 'Case sensitive' makes it stop matching (renamed count (0)). Absence asserted positively via the renamed-count label.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-feb3` | ✅ | elementId=txt-textbox-feb3 |
| 1 | set-value | `txt-textbox-febe` | ✅ | elementId=txt-textbox-febe |
| 2 | search | `match1.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "match1.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |
| 3 | search | `(1)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(1)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |
| 4 | invoke | `checkBox_case` | ✅ | pattern=TogglePattern; elementId=chk-checkboxcase-feba |
| 5 | search | `(0)` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "(0)", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "x":... |

### ✅ `p1-enumerate-counter-padding` — Enumeration counter token: regex '^testCase.*\.txt$' -> 'img_${padding=2}' produces zero-padded counter names img_00 and img_01.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `checkBox_regex` | ✅ | pattern=TogglePattern; elementId=chk-checkboxregex-f968 |
| 1 | set-value | `txt-textbox-f965` | ✅ | elementId=txt-textbox-f965 |
| 2 | set-value | `txt-textbox-f970` | ✅ | elementId=txt-textbox-f970 |
| 3 | search | `img_00` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "img_00", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "... |
| 4 | search | `img_01` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "img_01", "className": "TextBlock", "isEnabled": true, "isOffscreen": false, "... |

## P2 checks

### ✅ `p2-capture-groups` — Regex capture-group rewrite: '^(testCase)(\d)\.txt$' -> '$2_$1' transposes to 1_testCase and 2_testCase.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | invoke | `checkBox_regex` | ✅ | pattern=TogglePattern; elementId=chk-checkboxregex-ea64 |
| 1 | set-value | `txt-textbox-ea61` | ✅ | elementId=txt-textbox-ea61 |
| 2 | set-value | `txt-textbox-ea6c` | ✅ | elementId=txt-textbox-ea6c |
| 3 | search | `1_testCase` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "1_testCase", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |
| 4 | search | `2_testCase` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "2_testCase", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |

### ✅ `p2-uppercase-transform` — Case transform: identity search/replace 'Case'->'Case' with the Uppercase toggle uppercases the whole name (testCase1.txt -> TESTCASE1.TXT).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-f965` | ✅ | elementId=txt-textbox-f965 |
| 1 | set-value | `txt-textbox-f970` | ✅ | elementId=txt-textbox-f970 |
| 2 | invoke | `toggleButton_upperCase` | ✅ | pattern=TogglePattern; elementId=btn-togglebuttonupp-f980 |
| 3 | search | `TESTCASE1.TXT` | ✅ | text='{"matchCount": 3, "hasMore": false, "matches": [{"type": "CheckBox", "name": "testCase1.txt", "automationId": "2", "className": "CheckBox", "isEnabled"... |

### ❌ `p2-match-all-occurrences` — Match all occurrences: literal 't'->'f'. Default replaces only the first occurrence (festCase1.txt); enabling 'Match all occurrences' replaces every occurrence (fesfCase1.fxf).
> **Failure:** step 4 (search fesfCase1.fxf): text='{"matchCount": 0, "hasMore": false, "matches": []}' | contains 'fesfCase1.fxf': NO

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | set-value | `txt-textbox-3fe7` | ✅ | elementId=txt-textbox-3fe7 |
| 1 | set-value | `txt-textbox-3ff2` | ✅ | elementId=txt-textbox-3ff2 |
| 2 | search | `festCase1.txt` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "festCase1.txt", "className": "TextBlock", "isEnabled": true, "isOffscreen": f... |
| 3 | invoke | `checkBox_matchAll` | ✅ | pattern=TogglePattern; elementId=chk-checkboxmatchal-3fec |
| 4 | search | `fesfCase1.fxf` | ❌ | text='{"matchCount": 0, "hasMore": false, "matches": []}' \| contains 'fesfCase1.fxf': NO |
