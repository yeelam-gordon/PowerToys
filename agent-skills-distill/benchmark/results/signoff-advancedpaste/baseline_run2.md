# Sign-off Report — AdvancedPaste

- **Gate:** ✅ PASS (all P0 checks must PASS)
- **Generated:** 2026-07-05T08:13:28.007289+00:00
- **Target:** `{"app": "AdvancedPaste", "test_exe": "C:\\s\\powertoys\\x64\\Release\\tests\\AdvancedPaste.UnitTests\\AdvancedPaste.UnitTests.exe", "product_dll": "C:\\s\\powertoys\\x64\\Release\\tests\\AdvancedPaste.UnitTests\\PowerToys.AdvancedPaste.dll"}`
- **Totals:** 9/9 passed, 0 failed

## Results by priority

| Priority | Passed | Failed | Total |
|----------|--------|--------|-------|
| P0 | 3 | 0 | 3 |
| P1 | 4 | 0 | 4 |
| P2 | 2 | 0 | 2 |

## P0 checks

### ✅ `cap-plaintext-strips` — Paste-as-plain-text returns the plain text payload and strips HTML rich formatting (TransformHelpers.ToPlainTextAsync -> GetTextOrEmptyAsync).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `PlainText_StripsHtmlFormatting` | ✅ | 1 result(s): PlainText_StripsHtmlFormatting=Passed |

### ✅ `cap-markdown-transform` — Paste-as-markdown converts clipboard HTML into Markdown (heading + bold) (MarkdownHelper.ToMarkdownAsync).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `Markdown_ConvertsHtmlHeadingAndBold` | ✅ | 1 result(s): Markdown_ConvertsHtmlHeadingAndBold=Passed |

### ✅ `cap-json-csv` — Paste-as-json converts CSV clipboard text into a JSON array-of-arrays (JsonHelper.ToJsonFromXmlOrCsvAsync).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `Json_ConvertsCsv` | ✅ | 1 result(s): Json_ConvertsCsv=Passed |

## P1 checks

### ✅ `cap-json-xml` — Paste-as-json converts XML clipboard text into JSON (SerializeXmlNode path).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `Json_ConvertsXml` | ✅ | 1 result(s): Json_ConvertsXml=Passed |

### ✅ `cap-json-passthrough` — Paste-as-json returns already-valid JSON unchanged (IsJson short-circuit).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `Json_PassesThroughExistingJson` | ✅ | 1 result(s): Json_PassesThroughExistingJson=Passed |

### ✅ `cap-json-never-throws` — Reliability contract: Json transform returns empty string (never throws) when the clipboard has no text (PR #48124 guardrail).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `Json_NoTextReturnsEmpty` | ✅ | 1 result(s): Json_NoTextReturnsEmpty=Passed |

### ✅ `cap-window-uia` — The REAL Advanced Paste window (summoned via named-pipe ShowUI, no global hotkey) exposes the three core paste-format actions and gates the AI prompt box when AI is not configured.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | search | `Paste as plain text` | ✅ | matchCount=3 (>= 1) |
| 2 | search | `Paste as markdown` | ✅ | matchCount=3 (>= 1) |
| 3 | search | `Paste as JSON` | ✅ | matchCount=3 (>= 1) |
| 4 | get-property | `InputTxtBox.IsEnabled` | ✅ | value='False' expected='False' |

## P2 checks

### ✅ `cap-format-metadata` — PasteFormats metadata is the source of truth for AI gating: PlainText/Markdown/Json are core & non-AI; KernelQuery/CustomTextTransformation require AI.

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `Metadata_CoreVsAiGating` | ✅ | 5 result(s): Metadata_CoreVsAiGating (PlainText,True,False)=Passed, Metadata_CoreVsAiGating (Markdown,True,False)=Passed, Metadata_CoreVsAiGating (Json,True,... |

### ✅ `cap-hexcolor` — Clipboard hex-color detection for history-item preview (ClipboardItemHelper.IsRgbHexColor).

| # | verb | selector | ok | detail |
|---|------|----------|----|--------|
| 1 | mstest | `TestIsRgbHexColor` | ✅ | 17 result(s): TestIsRgbHexColor ("#FFBFAB",True)=Passed, TestIsRgbHexColor ("#000000",True)=Passed, TestIsRgbHexColor ("#FFFFFF",True)=Passed, TestIsRgbHexCo... |
