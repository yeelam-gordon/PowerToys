# winappcli Recipes (`winapp ui`)

winappcli drives any running Windows app through UI Automation (UIA). Works for
Win32, WinForms, WPF, WinUI 3, and Electron. Verified against winappcli **v0.4.0**.

## Golden rules

1. **Add `--json` to every verb** you parse — the default output is human-formatted
   and not stable.
2. **Discover selectors at runtime** with `inspect` / `search`; never hardcode.
3. **Prefer `automationId` selectors** (e.g. `num1Button`) over hash-suffixed slugs
   (e.g. `btn-num1button-fec5`) — automationIds survive app restarts; slugs may not.

## Targeting an app

Every verb accepts a global target:

| Option | Meaning |
|--------|---------|
| `-a, --app <name\|title\|PID>` | Process name, window title, or PID. |
| `-w, --window <HWND>` | A specific top-level window handle. |

```powershell
winapp ui list-windows                 # HWND, title, process, size for all windows
winapp ui list-windows -a notepad      # filter by app
winapp ui status -a notepad            # confirm connection
```

**Packaged/UWP apps (Calculator, Store apps, some WinUI):** the app's own process
often reports **no window** — the real window is owned by `ApplicationFrameHost`.
Find the HWND with `list-windows` and target it with `-w <HWND>`:

```powershell
winapp ui list-windows | Select-String "Calculator"
#   HWND 4065634: "Calculator" ... [ApplicationFrameWindow] (ApplicationFrameHost)
winapp ui inspect -w 4065634 -i
```

## Verb cheat-sheet

| Verb | Purpose | Notes |
|------|---------|-------|
| `list-windows` | Enumerate windows | Start here for packaged apps. |
| `status` | Connection info | Shows resolved process/PID/window. |
| `inspect <sel?>` | Dump the element tree with selectors | `-i` = interactive only; `--depth N`. |
| `search <text>` | Find elements by name/automationId | Returns slugs to reuse. |
| `get-value <sel>` | Read text/value | TextPattern → ValuePattern → Name. |
| `get-property <sel>` | Read a UIA property | `--property IsEnabled` for one. |
| `invoke <sel>` | Activate | Tries Invoke → Toggle → Selection patterns. |
| `click <sel>` | Mouse-click | Use for elements without InvokePattern; `--double`, `--right`. |
| `set-value <sel> <val>` | Set text/value | TextBox, ComboBox, Slider. |
| `focus <sel>` / `hover <sel>` | Focus / hover | Hover triggers tooltips/flyouts. |
| `scroll` / `scroll-into-view <sel>` | Scroll containers / into view | |
| `wait-for <sel>` | Wait for appear/disappear/value | `--timeout`, poll until condition. |
| `screenshot <sel?>` | PNG of window/element | Add `--capture-screen` over RDP. |
| `get-focused` | Element with keyboard focus | |
| `ui --cli-schema` | Dump full command structure as JSON | Ground scripts against it. |

## The selector/slug workflow

1. **Inspect** the live app to see the tree and selectors:
   ```
   winapp ui inspect -w <HWND> -i
   ...
   num1Button Button "One" (252,913 153x102)
   equalButton Button "Equals" (723,1019 153x102)
   CalculatorResults Text "Display is 0" (244,271 640x150)
   ```
   The **first token of each row is the selector**. In `--json`, it's the
   `selector` field of each element under `windows[].elements[]`.
2. **Drive** using those selectors:
   ```
   winapp ui invoke num1Button -w <HWND>
   winapp ui get-value CalculatorResults -w <HWND> --json   # -> {"text":"Display is 1"}
   ```
3. **Assert** on the returned `text`/`value`.

## `inspect --json` shape

```json
{
  "depth": 8, "interactive": true,
  "windows": [
    { "hwnd": 4065634, "title": "Calculator", "elementCount": 38,
      "elements": [
        { "type": "Button", "name": "One", "automationId": "num1Button",
          "selector": "num1Button", "isInvokable": true,
          "isEnabled": true, "isOffscreen": false,
          "x": 252, "y": 913, "width": 153, "height": 102,
          "ancestorPath": ["Window", "Window", "..."] }
      ] } ]
}
```

## Result shapes to assert on

| Verb | `--json` result | Assert on |
|------|-----------------|-----------|
| `get-value` | `{ "elementId": "...", "text": "Display is 3" }` | `text` |
| `invoke` | `{ "elementId": "...", "pattern": "InvokePattern", "hwnd": ... }` | success = exit 0 |
| `get-property` | `{ "property": "IsEnabled", "value": true }` | `value` |
| error (any verb) | `{ "error": { "code": "element_not_found", "message": "...", "selector": "..." } }` | `error.code` |

## Gotchas

- **Packaged apps have no window under their own process** — target the
  `ApplicationFrameHost` HWND with `-w`. `-a Calculator` finds 0 elements.
- **`get-value` returns the full accessible name** — Calculator's display reads
  `"Display is 3"`, not `"3"`. Assert with `contains`, not `equals`.
- **Transient `element_not_found`** occurs when a verb fires while the app is
  mid-animation or regaining foreground. Retry with a short backoff (the runner
  does this) and/or add a small pause between steps.
- **Emoji/Unicode in output crashes a cp1252 console.** Force UTF-8
  (`[Console]::OutputEncoding` or `stream.reconfigure(encoding="utf-8")`) before
  printing report text; always write report files as UTF-8.
- **Over RDP / disconnected console**, `screenshot` may capture black — add
  `--capture-screen`. Some apps also launch off-screen; move/restore first.
- **`click` vs `invoke`** — `invoke` uses UIA patterns and works headlessly; `click`
  simulates the mouse and needs the element on-screen. Use `click` only for
  elements lacking InvokePattern (column headers, custom list items).
