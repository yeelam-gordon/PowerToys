# AdvancedPaste Sign-off Checklist (winappcli-driven)

Declarative behavioral sign-off for **PowerToys Advanced Paste**. Every item is
verified by driving the **real** app through `winapp ui` (UI Automation) + the
Windows clipboard, invoking a real paste-format action, letting Advanced Paste
paste into a **target editor** (Notepad) via its own `SendInput(Ctrl+V)`, then
reading the produced text back with `winapp ui get-value` and capturing a
`winapp ui screenshot`. No unit tests, no reflection, no in-process bypass.

**Legend:** P0 = release-blocking, P1 = important, P2 = nice-to-have.
Gate = PASS only when every P0 passes.

**Common setup (once per run):**
1. `ap_controller.ps1` running — owns the named-pipe **ShowUI** server and
   re-shows the Advanced Paste window whenever a `show.trigger` file appears.
2. A **Notepad** window open as the paste target. It MUST be the foreground
   window immediately before ShowUI so Advanced Paste's Ctrl+V lands in it.
3. Verify synthetic input works first (`verify_input.ps1`) — abort/report if not.

---

## CHK-01 — Paste as plain text strips HTML formatting  (P0)

- **Check:** "Paste as plain text" returns the clipboard's text payload with all
  HTML markup removed.
- **Drive:** Set clipboard to `{ text: "BoldHello", html: "<b>Bold</b>Hello" }`.
  Clear Notepad, foreground it, ShowUI, `winapp ui invoke "Paste as plain text"`.
- **Verify:** `winapp ui get-value "Text editor" -w <notepad>` **equals**
  `BoldHello` (no `<b>` tags). Screenshot AP window + Notepad result.

## CHK-02 — Paste as markdown converts an HTML heading  (P0)

- **Check:** "Paste as markdown" converts HTML `<h1>` to a Markdown `# ` heading.
- **Drive:** Clipboard `html: "<h1>Title</h1><p><b>bold</b></p>"`. Invoke
  `"Paste as markdown"`.
- **Verify:** Notepad text **matches** `#\s*Title`. Screenshot both.

## CHK-03 — Paste as JSON converts CSV to a JSON array  (P0)

- **Check:** "Paste as JSON" recognizes CSV and emits a JSON array preserving
  cell values.
- **Drive:** Clipboard text `name,age\r\nAlice,30`. Invoke `"Paste as JSON"`.
- **Verify:** Output starts with `[` and contains `"name"` and `"Alice"`.
  Screenshot both.

## CHK-04 — Paste as JSON converts XML to a JSON object  (P1)

- **Check:** "Paste as JSON" converts XML into a JSON object keyed by element.
- **Drive:** Clipboard text `<note><to>Tove</to><from>Jani</from></note>`.
  Invoke `"Paste as JSON"`.
- **Verify:** Output matches `"note"\s*:` and contains `Tove`. Screenshot both.

## CHK-05 — Paste as JSON passes valid JSON through unchanged  (P1)

- **Check:** Already-valid JSON is returned verbatim (no double-encoding).
- **Drive:** Clipboard text `{"k":123}`. Invoke `"Paste as JSON"`.
- **Verify:** Output **equals** `{"k":123}`. Screenshot both.

## CHK-06 — Paste as JSON falls back to an array of lines  (P1)

- **Check:** Non-tabular, non-JSON multiline text never throws; it falls back to
  a JSON array of lines (the "never-throws" guard).
- **Drive:** Clipboard text `hello world\r\nsecond line`. Invoke `"Paste as JSON"`.
- **Verify:** Output starts with `[` and contains `"hello world"` and
  `"second line"`. Screenshot both.

## CHK-07 — AI prompt box is gated when no AI provider is configured  (P1)

- **Check:** The custom-AI prompt box (`InputTxtBox`) is **disabled** unless AI is
  allowed by GPO and enabled in settings.
- **Drive:** ShowUI. `winapp ui get-property "InputTxtBox" -w <ap> --property IsEnabled`.
- **Verify:** `IsEnabled = False`. Screenshot AP window.

## CHK-08 — Window clipboard preview reflects current clipboard  (P2)

- **Check:** The AP window preview shows the live clipboard content.
- **Drive:** Set clipboard text `PREVIEW_CHECK_555`. ShowUI.
  `winapp ui search "PREVIEW_CHECK_555" -w <ap>`.
- **Verify:** The preview surface contains `PREVIEW_CHECK_555`. Screenshot.

## CHK-09 — Core paste-format list exposes the three core actions  (P2)

- **Check:** The default action list shows plain-text, markdown and JSON items.
- **Drive:** ShowUI. `winapp ui search "Paste as plain text|markdown|JSON" -w <ap>`.
- **Verify:** All three appear as `ListItem`s. Screenshot.

## CHK-10 — Paste as markdown emits bold emphasis  (P2)

- **Check:** "Paste as markdown" converts `<b>bold</b>` to `**bold**`.
- **Drive:** Clipboard `html: "<h1>Title</h1><p><b>bold</b></p>"`. Invoke
  `"Paste as markdown"`.
- **Verify:** Output (after stripping backslash escapes from ReverseMarkdown)
  contains `**bold**`. Screenshot both.

---

### Verification pattern (per paste item)

```
set clipboard  ->  clear + foreground Notepad  ->  ShowUI (trigger pipe)
   ->  screenshot AP window
   ->  winapp ui invoke "<format name>" -w <ap>
   ->  AdvancedPaste hides + SendInput(Ctrl+V) into Notepad
   ->  poll winapp ui get-value "Text editor" -w <notepad> until non-empty
   ->  assert output == expected  ->  screenshot Notepad result
```

Invoke actions by **display name** (e.g. `"Paste as JSON"`), not the auto slug —
slug hashes change every time the window is re-shown.
