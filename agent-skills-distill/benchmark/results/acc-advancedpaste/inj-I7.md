# AdvancedPaste Sign-off - inj-I7

GATE: **PASS** | Passed 9/10 | P0 failures: 0 | verification: produced-clipboard | 2026-07-05T21:53:56

| ID | Pri | PASS | Capability | Expected | Actual |
|----|-----|------|------------|----------|--------|
| CHK-01 | P0 | OK | Paste as plain text produces clipboard text with HTML stripped | equals "BoldHello" | BoldHello |
| CHK-02 | P0 | OK | Paste as markdown converts HTML <h1> heading to '# Title' | contains '# Title' | # Title  **bold** |
| CHK-03 | P0 | OK | Paste as JSON converts CSV to JSON array (values preserved) | array containing "name" and "Alice" | [   [     "name",     "age"   ],   [     "Alice",     "30"   ] ] |
| CHK-04 | P1 | OK | Paste as JSON converts XML to JSON object with element keys | contains '\ | note\:' object and 'Tove' |
| CHK-05 | P1 | OK | Paste as JSON returns already-valid JSON unchanged (passthrough) | equals {"k":123} | {"k":123} |
| CHK-06 | P1 | OK | Paste as JSON falls back to JSON array-of-lines for non-tabular text (never-throws guard) | array containing "hello world","second line" | [   "hello world",   "second line" ] |
| CHK-07 | P1 | X | AI prompt box (InputTxtBox) is disabled when no AI provider is configured (AI gating) | InputTxtBox IsEnabled=False | IsEnabled=True |
| CHK-08 | P2 | OK | Window clipboard preview shows the current clipboard content | preview shows 'PREVIEW_CHECK_555' | found=True |
| CHK-09 | P2 | OK | Core paste-format list shows plain text, markdown and JSON actions | all three core ListItems present | plain=True md=True json=True |
| CHK-10 | P2 | OK | Paste as markdown converts <b>bold</b> to '**bold**' emphasis | contains '**bold**' | # Title  **bold** |
