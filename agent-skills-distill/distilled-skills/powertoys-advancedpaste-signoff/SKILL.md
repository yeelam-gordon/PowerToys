---
name: powertoys-advancedpaste-signoff
description: 'Declarative, winappcli-driven sign-off / regression suite for PowerToys Advanced Paste. Drives the REAL Advanced Paste window through winapp ui + the Windows clipboard, invokes each paste-format action, lets Advanced Paste paste into a target editor (Notepad) via its own SendInput(Ctrl+V), then reads the produced text back with winapp ui get-value and screenshots the result. Use to sign off / smoke test / regression test Advanced Paste after a build, or to gate a release on its paste transforms (plain text, markdown, JSON from CSV/XML, JSON passthrough/fallback), AI-box GPO gating, clipboard preview and the core format list. Keywords: PowerToys, Advanced Paste, paste as plain text/markdown/JSON, winappcli, winapp ui, UI automation, sign-off, smoke test, regression, P0 P1 P2, clipboard, SendInput, ShowUI pipe.'
license: Complete terms in LICENSE.txt
---

# PowerToys Advanced Paste — Sign-off via UI Automation (winappcli)

**WHAT:** A prioritized, executable P0/P1/P2 sign-off for **PowerToys Advanced
Paste**. It exercises the app exactly as an end user does: set the clipboard,
open the Advanced Paste window, invoke a paste-format action, and verify the
transformed text that Advanced Paste *pastes into a real target editor*. Every
assertion reads back the actual pasted output via `winapp ui get-value` and is
backed by a `winapp ui screenshot`. No unit tests, no reflection, no in-process
harness — real execution only.

**WHEN:** Use after rebuilding the Advanced Paste project, before shipping, or to
catch regressions in the transforms (plain text / markdown / JSON), the AI-box
GPO gating, the clipboard preview, or the core format list.

This skill is an **application of** the generic
[`app-signoff-uia`](../../.github/skills/app-signoff-uia/SKILL.md) skill — read
that for winappcli fundamentals (selectors, `expect` assertions, gotchas). This
file adds only the Advanced-Paste-specific logic.

The source of truth is [`signoff-checklist.md`](./signoff-checklist.md) — 10
declarative items (CHK-01..CHK-10) each with **Check / Drive / Verify**.

## When to Use This Skill

- Signing off / smoke-testing Advanced Paste after rebuilding the module.
- Gating a release on its paste transforms (plain text, markdown, JSON from
  CSV/XML, JSON passthrough & never-throws fallback), AI-box GPO gating,
  clipboard preview, and the core format list.
- Regression-checking a PR that touches `src/modules/AdvancedPaste` before merge.
- Reproducing/confirming a reported Advanced Paste transform bug end-to-end.

## Prerequisites

- **winappcli** on PATH (`winapp ui status`), v0.4.0+.
- **PowerToys built** at `C:\s\PowerToys\x64\Release`; Advanced Paste source at
  `C:\s\PowerToys\src\modules\AdvancedPaste`. Rebuild the project with VsDevCmd
  x64 + MSBuild (see below).
- **Synthetic input must work** in the current session. Advanced Paste pastes via
  `SendInput(Ctrl+V)`; if the session is disconnected the paste silently no-ops.
  **Always run `scripts/verify_input.ps1` first** — it exits 0 only when SendInput
  actually injects events *and* `GetForegroundWindow()!=0`. If it reports BLOCKED,
  STOP and report honestly; do not fabricate results.
- A **Notepad** window as the paste target.

## How to Sign Off

Work the checklist item by item, capturing a screenshot per item and gating on P0.

1. **Verify input** (hard gate):
   `powershell -File scripts/verify_input.ps1` → must print `INPUT: OK`.
2. **Rebuild** the Advanced Paste project (kill any running AP first — the DLL is
   locked while it runs):
   ```powershell
   & "$vsDevCmd\...\MSBuild.exe" `
     "C:\s\PowerToys\src\modules\AdvancedPaste\AdvancedPaste\AdvancedPaste.csproj" `
     /p:Configuration=Release /p:Platform=x64 "/p:SolutionDir=C:\s\PowerToys\" /m /v:m
   ```
   Success = output names `PowerToys.AdvancedPaste.dll` with no `: error`.
3. **Start the window controller** (owns the ShowUI pipe):
   `powershell -File scripts/ap_controller.ps1` — it launches AP, writes `ap.pid`
   and `controller.ready`, and re-shows the window whenever a `show.trigger` file
   appears.
4. **Run the sign-off:**
   `powershell -File scripts/run_signoff.ps1 -Basename baseline`
   It drives CHK-01..CHK-10, writes `baseline.json` + `baseline.md`, saves
   screenshots to `screenshots\baseline\`, and exits 0 only when every P0 passes.
5. **Iterate.** If a should-pass check fails, re-inspect selectors / raise waits
   and re-run until green. Commit the checklist + baseline screenshots as the
   regression suite; re-run on every build.

## App-Specific winappcli Logic

The non-obvious business logic that makes signing off THIS app possible.

### 1. Summoning the window — the ShowUI named pipe (no hotkey required)

Advanced Paste has **no persistent window**. `App.xaml.cs::OnLaunched` parses
`arg1 = PID to watch`, `arg2 = pipe name`, connects to that pipe as a **client**,
and shows its window only when it reads a UTF-16 `"ShowUI\r\n"` line. Normally the
PowerToys Runner is the pipe server and sends ShowUI on the Win+Shift+V hotkey.

`ap_controller.ps1` **impersonates the Runner**: it creates a
`NamedPipeServerStream`, launches AP with `<controllerPID> <pipeName>`, and writes
`ShowUI\r\n` (UTF-16) on demand. `arg1` must be a **live PID** — AP exits when the
watched process dies, so the controller passes its own PID. The window is class
`WinUIDesktopWin32WindowClass`, title **"Advanced Paste"**; find its HWND with
`winapp ui list-windows -a PowerToys.AdvancedPaste`.

Triggering the real Win+Shift+V hotkey also works when synthetic input is healthy,
but the pipe path is deterministic and hotkey-independent.

### 2. Paste-to-target verification (the core pattern)

winappcli is pure UIA — it has **no type/clipboard verb**, so we verify a transform
by observing what Advanced Paste *actually pastes*:

```
set clipboard (WinForms DataObject: CF_HTML + unicode text)
  -> clear Notepad + force it foreground
  -> ShowUI (write show.trigger; controller re-shows AP)
  -> winapp ui screenshot "Advanced Paste"
  -> winapp ui invoke "<format display name>" -w <apHwnd>
  -> AP transforms, HideWindow, SendInput(Ctrl+V) into the prior foreground window
  -> poll winapp ui get-value "Text editor" -w <notepadHwnd> until non-empty
  -> assert output == expected  -> screenshot the Notepad result
```

**Notepad MUST be the foreground window immediately before ShowUI** — AP returns
focus to whatever was foreground and pastes there. The harness force-foregrounds
Notepad via `AttachThreadInput` (`ForceForeground`) and retries the whole drive up
to 3×, polling `get-value` up to 12×700 ms (markdown/JSON need the longer waits).

### 3. SendInput on x64 — the 40-byte INPUT struct gotcha

Advanced Paste's own paste uses `SendInput`; our input helpers also do (foreground,
clear). On **x64 the `INPUT` struct must be 40 bytes** — define the union with a
`MOUSEINPUT` (the largest member) *and* `KEYBDINPUT` both at `FieldOffset(0)`. A
wrong size makes `SendInput` return 0 with `GetLastError()=87` and nothing is
injected. Unicode typing uses `KEYEVENTF_UNICODE (0x0004)`; Ctrl+V = VK 0x11 + 0x56.
See `scripts/input_helpers.ps1` (`WinInput` class).

### 4. Invoke by display name, not slug

Re-showing the window re-hashes the auto slugs (`btn-...-<hash>`), so invoke actions
by their **display name text** — `"Paste as plain text"`, `"Paste as markdown"`,
`"Paste as JSON"`. UIA `InvokePattern` works on these `ListItem`s headlessly.

### 5. Clipboard setup avoids arg-quoting corruption

Clipboard payloads with quotes/newlines get mangled across the PowerShell arg
boundary. `set_clipboard.ps1` accepts `-FromFile` / `-HtmlFromFile` so the harness
writes the value to a temp file and passes the path. Modes: `html` (sets CF_HTML +
unicode text) and `text`.

### 6. Notepad quirks

Windows 11 Notepad restores prior session tabs (stale content) — always clear
(Ctrl+A, Delete) after foregrounding, and read the delta. There is normally one
`"Text editor"` document; `get-value "Text editor" -w <hwnd>` on an empty doc
returns the literal name `"Text editor"`, which the harness treats as "not yet
pasted".

### 7. Locked-workstation variant — verify AP's produced clipboard

When `SendInput` is unavailable (locked/secure desktop → `SendInput` returns 0
with `GetLastError()=5 ERROR_ACCESS_DENIED`, `GetForegroundWindow()==0`), neither
our helpers nor AP's own `Ctrl+V` can reach a target editor. But **AP writes the
transformed result to the clipboard *before* it issues the paste keystroke**
(`ClipboardHelper.TryCopyPasteAsync` sets the clipboard, then pastes). So the
transform can still be verified end-to-end by reading that clipboard — the exact
bytes AP would paste. `run_signoff_clip.ps1` is this variant: identical drive path
(ShowUI → set clipboard → `winapp ui invoke` → screenshot) but it reads the
produced clipboard (STA `Clipboard.GetText`) instead of pasting into Notepad. Use
`run_signoff.ps1` (paste-into-target + `get-value`) when the session is unlocked
and input is healthy; use `run_signoff_clip.ps1` when it is locked. Detect which
with `verify_input.ps1` / `wait_input.ps1` (the latter reports `lastErr`).

## Coverage & Limits

**Covered (all winappcli-driven, end-to-end):** the three core paste transforms
(plain text strips HTML, markdown from HTML heading + bold, JSON from CSV/XML,
JSON passthrough, JSON array-of-lines fallback), AI-box GPO/settings gating,
window clipboard preview, and the core format list — 10 checks, CHK-01..CHK-10.

**Limits:**
- **Only the 3 `IsCoreAction` transforms appear in the window** (plain text,
  markdown, JSON). Additional formats (hex-color, image-to-text, etc.) are not in
  the default list, so they are covered indirectly through the JSON code paths
  rather than as separate list items.
- **The AI/LLM path is verified only up to its gate.** With no AI provider/GPO the
  prompt box is disabled (CHK-07); we do **not** exercise a live model call, so the
  actual AI transform output is out of scope in this environment.
- **Synthetic input is preferred but not mandatory.** The paste-to-target checks
  depend on `SendInput` reaching the interactive desktop. In a locked/secure
  desktop or disconnected RDP session `SendInput` is denied (0 events;
  `GetForegroundWindow()==0`; `GetLastError()=5` when locked, no-op when headless).
  In that case the sign-off does **not** skip the transform checks and does **not**
  fake them: it switches to `run_signoff_clip.ps1`, which verifies each transform by
  reading **AdvancedPaste's own produced clipboard** (the bytes AP would paste) —
  still real, end-to-end app execution, only the final keystroke delivery is
  substituted. `verify_input.ps1`/`wait_input.ps1` decide which variant to run. The
  non-paste checks (CHK-07/08/09) are pure UIA reads and run either way.
- **Screenshots can be black over a disconnected RDP session** — capture on a
  connected/known-good session for meaningful baselines.

## References

- Generic parent skill: [`.github/skills/app-signoff-uia/SKILL.md`](../../.github/skills/app-signoff-uia/SKILL.md)
  (winappcli fundamentals, selectors, `expect`, gotchas, `signoff.py`).
- Capabilities/knowledge: [`powertoys-advancedpaste-knowledge/SKILL.md`](../powertoys-advancedpaste-knowledge/SKILL.md).
- Checklist (source of truth): [`signoff-checklist.md`](./signoff-checklist.md).
- Scripts: [`scripts/`](./scripts) — `verify_input.ps1`, `wait_input.ps1`,
  `ap_controller.ps1`, `set_clipboard.ps1`, `input_helpers.ps1`,
  `run_signoff.ps1` (paste-to-target) and `run_signoff_clip.ps1`
  (locked-session produced-clipboard), plus `injections.ps1` /
  `run_injections.ps1` / `run_injections_clip.ps1` (the fault-injection
  acceptance harness).
- Optional machine-runnable mirror: [`assets/advancedpaste.spec.json`](./assets/advancedpaste.spec.json).
- Baseline screenshots: `assets/screenshots/` (written at sign-off run time).
- Advanced Paste source: `C:\s\PowerToys\src\modules\AdvancedPaste`.
