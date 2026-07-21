# Sign-off Report — PowerToys Environment Variables

- **Gate:** ❌ FAIL (all P0 checks must PASS)
- **Generated:** 2026-07-04T14:54:07.091390+00:00
- **Target:** `{"app": "PowerToys.EnvironmentVariables", "window": "6359956"}`
- **Totals:** 3/7 passed, 4 failed

## Results by priority

| Priority | Passed | Failed | Total |
|----------|--------|--------|-------|
| P0 | 2 | 1 | 3 |
| P1 | 1 | 2 | 3 |
| P2 | 0 | 1 | 1 |

## P0 checks

### ✅ `user-var-name-shown` — User variable B3_SIGNOFF appears in the Applied Variables list

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `B3_SIGNOFF` | ✅ | text='{"matchCount": 2, "hasMore": false, "matches": [{"type": "Text", "name": "B3_SIGNOFF", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |

### ❌ `user-var-value-shown` — User variable value HelloB3Value is displayed (value column populated)
> **Failure:** step 0 (search HelloB3Value): text='{"matchCount": 0, "hasMore": false, "matches": []}' | contains 'HelloB3Value': NO; regex '"matchCount":\\s*[1-9]': NO MATCH

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `HelloB3Value` | ❌ | text='{"matchCount": 0, "hasMore": false, "matches": []}' \| contains 'HelloB3Value': NO; regex '"matchCount":\\s*[1-9]': NO MATCH |

### ✅ `system-var-os-shown` — System variable OS value Windows_NT is displayed

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `Windows_NT` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "Windows_NT", "className": "TextBlock", "isEnabled": true, "isOffscreen": true... |

## P1 checks

### ❌ `value-expansion-works` — %NUMBER_OF_PROCESSORS% is expanded in the Applied view (B3_EXPAND -> 16_cores)
> **Failure:** step 0 (search 16_cores): text='{"matchCount": 0, "hasMore": false, "matches": []}' | contains '16_cores': NO; regex '"matchCount":\\s*[1-9]': NO MATCH

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `16_cores` | ❌ | text='{"matchCount": 0, "hasMore": false, "matches": []}' \| contains '16_cores': NO; regex '"matchCount":\\s*[1-9]': NO MATCH |

### ❌ `path-user-merge-works` — Merged PATH includes the USER PATH segment (ZZUSERPATHZZ appended to System PATH)
> **Failure:** step 0 (search ZZUSERPATHZZ): text='{"matchCount": 0, "hasMore": false, "matches": []}' | contains 'ZZUSERPATHZZ': NO; regex '"matchCount":\\s*[1-9]': NO MATCH

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `ZZUSERPATHZZ` | ❌ | text='{"matchCount": 0, "hasMore": false, "matches": []}' \| contains 'ZZUSERPATHZZ': NO; regex '"matchCount":\\s*[1-9]': NO MATCH |

### ✅ `system-var-arch-shown` — System variable PROCESSOR_ARCHITECTURE value AMD64 is displayed

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `AMD64` | ✅ | text='{"matchCount": 2, "hasMore": false, "matches": [{"type": "Text", "name": "AMD64", "className": "TextBlock", "isEnabled": true, "isOffscreen": true, "x"... |

## P2 checks

### ❌ `extra-user-var-shown` — Second user variable value AlphaUniqueVal is displayed
> **Failure:** step 0 (search AlphaUniqueVal): text='{"matchCount": 0, "hasMore": false, "matches": []}' | contains 'AlphaUniqueVal': NO; regex '"matchCount":\\s*[1-9]': NO MATCH

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `AlphaUniqueVal` | ❌ | text='{"matchCount": 0, "hasMore": false, "matches": []}' \| contains 'AlphaUniqueVal': NO; regex '"matchCount":\\s*[1-9]': NO MATCH |
