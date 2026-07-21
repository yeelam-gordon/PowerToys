# Capability Spec Schema

A capability spec is a JSON file describing a prioritized suite of end-user
capability checks. `signoff.py run --spec <file>` executes it against a running
app via winappcli and produces a PASS/FAIL report gated on P0.

## Top-level object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `name` | string | no | Human label for the app (used in the report title). |
| `target` | object | no | Default app target `{ "app": "...", "window": "..." }`. CLI `--app` / `--window` override it. |
| `checks` | array | **yes** | The capability checks (see below). |

At least one of `target.app`, `target.window`, `--app`, or `--window` must resolve
so winappcli knows which app/window to drive.

## Check object

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `id` | string | **yes** | Unique, stable identifier (e.g. `add-1-2-equals-3`). |
| `priority` | `P0`\|`P1`\|`P2` | **yes** | P0 = must-pass (gates release), P1 = important, P2 = nice-to-have. |
| `description` | string | no | Human-readable intent. |
| `steps` | array | **yes** | Ordered list of steps; first failing step fails the check. |

### Priority meaning

- **P0** — core, release-blocking capabilities. **All P0 must PASS** or the overall
  gate is FAIL (exit code 1). Keep P0 small and truly critical (smoke test).
- **P1** — important features; failures are reported but don't block the gate.
- **P2** — secondary / edge features.

## Step object

Each step maps to one `winapp ui <verb>` invocation.

| Field | Type | Required | Description |
|-------|------|----------|-------------|
| `verb` | string | **yes** | winappcli verb: `invoke`, `click`, `set-value`, `get-value`, `get-property`, `wait-for`, `focus`, `hover`, `scroll`, `scroll-into-view`, `search`, `inspect`, `screenshot`. |
| `selector` | string | usually | Semantic slug or automationId (e.g. `num1Button`, `btn-num1button-fec5`). Omit only for verbs that don't need one. |
| `value` | string | no | Positional value passed after the selector (e.g. for `set-value`). |
| `args` | string[] | no | Extra raw args appended before `--json` (e.g. `["--double"]` for double-click, `["--timeout", "10"]` for `wait-for`). |
| `expect` | object | no | Assertion on this step's result. Steps with no `expect` are actions — they fail the check only if winappcli itself errors. |

## `expect` assertions

The runner extracts a comparable string from the winapp `--json` result
(prefers `text`, then `value`/`name`, else the whole JSON), then applies every key
present (all must hold):

| Key | Type | Meaning |
|-----|------|---------|
| `contains` | string | Substring must appear. |
| `not_contains` | string | Substring must NOT appear. |
| `equals` | string | Trimmed extracted text must equal this. |
| `regex` | string | Extracted text must match this pattern. |
| `exit_code` | int | winappcli process exit code must equal this. |
| `found` | bool | For `wait-for`: require `found == value`. |
| `ci` | bool | Case-insensitive matching (default `true`). Set `false` for exact case. |

### Choosing the right assertion verb

- Read displayed text/values with **`get-value`** (tries TextPattern → ValuePattern).
- Read a specific UIA property (e.g. `IsEnabled`, `ToggleState`) with
  **`get-property`** + `args: ["--property", "IsEnabled"]`.
- Confirm an element appeared/changed with **`wait-for`** + `expect: {"found": true}`.

## Full example

```json
{
  "name": "Windows Calculator",
  "target": { "window": "4065634", "app": "CalculatorApp" },
  "checks": [
    {
      "id": "add-1-2-equals-3",
      "priority": "P0",
      "description": "Addition: 1 + 2 = 3",
      "steps": [
        { "verb": "invoke", "selector": "clearButton" },
        { "verb": "invoke", "selector": "num1Button" },
        { "verb": "invoke", "selector": "plusButton" },
        { "verb": "invoke", "selector": "num2Button" },
        { "verb": "invoke", "selector": "equalButton" },
        { "verb": "get-value", "selector": "CalculatorResults",
          "expect": { "contains": "Display is 3" } }
      ]
    }
  ]
}
```

See [`../templates/capability-spec.template.json`](../templates/capability-spec.template.json)
for a complete 5-check P0/P1/P2 example.
