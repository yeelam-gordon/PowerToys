# Sign-off Report — PowerToys Environment Variables

- **Gate:** ✅ PASS (all P0 checks must PASS)
- **Generated:** 2026-07-04T14:44:05.221373+00:00
- **Target:** `{"app": "PowerToys.EnvironmentVariables", "window": "8784636"}`
- **Totals:** 7/7 passed, 0 failed

## Results by priority

| Priority | Passed | Failed | Total |
|----------|--------|--------|-------|
| P0 | 3 | 0 | 3 |
| P1 | 3 | 0 | 3 |
| P2 | 1 | 0 | 1 |

## P0 checks

### ✅ `user-var-name-shown` — User variable B3_SIGNOFF appears in the Applied Variables list

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `B3_SIGNOFF` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "B3_SIGNOFF", "className": "TextBlock", "isEnabled": true, "isOffscreen": fals... |

### ✅ `user-var-value-shown` — User variable value HelloB3Value is displayed (value column populated)

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `HelloB3Value` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "HelloB3Value", "className": "TextBlock", "isEnabled": true, "isOffscreen": fa... |

### ✅ `system-var-os-shown` — System variable OS value Windows_NT is displayed

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `Windows_NT` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "Windows_NT", "className": "TextBlock", "isEnabled": true, "isOffscreen": true... |

## P1 checks

### ✅ `value-expansion-works` — %NUMBER_OF_PROCESSORS% is expanded in the Applied view (B3_EXPAND -> 16_cores)

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `16_cores` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "16_cores", "className": "TextBlock", "isEnabled": true, "isOffscreen": false,... |

### ✅ `path-user-merge-works` — Merged PATH includes the USER PATH segment (ZZUSERPATHZZ appended to System PATH)

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `ZZUSERPATHZZ` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "C:\\\\.Tools\\\\agency\\\\CurrentVersion;C:\\\\Program Files (x86)\\\\Microso... |

### ✅ `system-var-arch-shown` — System variable PROCESSOR_ARCHITECTURE value AMD64 is displayed

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `AMD64` | ✅ | text='{"matchCount": 2, "hasMore": false, "matches": [{"type": "Text", "name": "AMD64", "className": "TextBlock", "isEnabled": true, "isOffscreen": true, "x"... |

## P2 checks

### ✅ `extra-user-var-shown` — Second user variable value AlphaUniqueVal is displayed

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 0 | search | `AlphaUniqueVal` | ✅ | text='{"matchCount": 1, "hasMore": false, "matches": [{"type": "Text", "name": "AlphaUniqueVal", "className": "TextBlock", "isEnabled": true, "isOffscreen": ... |
