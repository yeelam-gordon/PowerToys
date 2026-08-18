#!/usr/bin/env python3
"""
signoff.py - Drive a REAL running Windows app through winappcli (`winapp ui ...`)
to execute a prioritized P0/P1/P2 sign-off / regression suite and emit a report.

Two modes:
  run       Execute a capability spec against a running app -> PASS/FAIL report.
  discover  Inspect a running app and emit a starter capability-spec skeleton.

The skill is GENERIC: nothing about any specific app is hardcoded. Every element
is addressed by a winappcli selector (semantic slug or automationId) that you
discover at runtime from the real app.

See references/capability-spec.md for the spec schema and
references/winappcli-recipes.md for the verb cheat-sheet.
"""
from __future__ import annotations

import argparse
import json
import re
import shutil
import subprocess
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

# ---------------------------------------------------------------------------
# Constants
# ---------------------------------------------------------------------------

WINAPP = "winapp"  # on PATH; overridable via --winapp
UI = "ui"

# Verbs that READ a result we can assert on.
READ_VERBS = {"get-value", "get-property", "wait-for", "search", "inspect", "get-focused"}
# Verbs that ACT (no assertion expected, though failures still surface).
ACTION_VERBS = {"invoke", "click", "set-value", "focus", "hover", "scroll",
                "scroll-into-view", "screenshot"}
VALID_PRIORITIES = ("P0", "P1", "P2")

# Elements that are almost never meaningful capabilities: window chrome + bots.
# Used by discover mode to keep the skeleton signal-dense (the "no-op filter").
NOISE_NAME_RE = re.compile(
    r"^(minimize|maximize|restore|close|open navigation|system|"
    r"keep on top|more options|settings)\b",
    re.IGNORECASE,
)
NOISE_AUTOMATION_IDS = {"Minimize", "Maximize", "Restore", "Close"}


def log(msg: str) -> None:
    """All diagnostics go to stderr so stdout stays clean for piping."""
    print(f"[signoff] {msg}", file=sys.stderr, flush=True)


# ---------------------------------------------------------------------------
# winappcli invocation
# ---------------------------------------------------------------------------

class WinappError(RuntimeError):
    pass


def _target_args(target: dict) -> list[str]:
    """Build -a/-w args from a target dict {app?, window?}."""
    args: list[str] = []
    if target.get("window"):
        args += ["-w", str(target["window"])]
    if target.get("app"):
        args += ["-a", str(target["app"])]
    if not args:
        raise WinappError("No target: provide --app, --window, or a spec 'target'.")
    return args


def run_winapp(verb: str, target: dict, selector: str | None = None,
               value: str | None = None, extra: list[str] | None = None,
               winapp: str = WINAPP, timeout: int = 60, retries: int = 2,
               retry_pause: float = 0.4) -> dict:
    """
    Shell out to `winapp ui <verb> [selector] [value] <target> --json`.

    Returns a dict:
      { ok: bool, verb, selector, exit_code, stdout, stderr, json: <parsed|None> }
    Never raises on non-zero exit; the caller decides how to treat failures.

    Retries transient UIA failures (e.g. 'element_not_found' while the app is
    mid-animation / regaining foreground) up to `retries` times.
    """
    cmd = [winapp, UI, verb]
    if selector is not None:
        cmd.append(selector)
    if value is not None:
        cmd.append(value)
    cmd += _target_args(target)
    if extra:
        cmd += extra
    cmd.append("--json")

    log("exec: " + " ".join(_quote(c) for c in cmd))
    result = None
    for attempt in range(retries + 1):
        try:
            proc = subprocess.run(cmd, capture_output=True, text=True, timeout=timeout)
        except FileNotFoundError as exc:
            raise WinappError(
                f"'{winapp}' not found on PATH. Install winappcli or pass --winapp."
            ) from exc
        except subprocess.TimeoutExpired:
            result = {"ok": False, "verb": verb, "selector": selector,
                      "exit_code": -1, "stdout": "",
                      "stderr": f"timeout after {timeout}s", "json": None}
        else:
            parsed = None
            out = (proc.stdout or "").strip()
            if out:
                try:
                    parsed = json.loads(out)
                except json.JSONDecodeError:
                    parsed = None
            result = {
                "ok": proc.returncode == 0,
                "verb": verb, "selector": selector,
                "exit_code": proc.returncode,
                "stdout": proc.stdout, "stderr": proc.stderr, "json": parsed,
            }
        if result["ok"] or not _is_transient(result) or attempt == retries:
            return result
        log(f"  transient failure, retry {attempt + 1}/{retries} after {retry_pause}s")
        time.sleep(retry_pause)
    return result


TRANSIENT_MARKERS = ("element_not_found", "no element found", "timeout",
                     "not connected", "no window")


def _is_transient(result: dict) -> bool:
    blob = ((result.get("stderr") or "") + " " +
            (result.get("stdout") or "")).lower()
    j = result.get("json")
    if isinstance(j, dict) and isinstance(j.get("error"), dict):
        blob += " " + str(j["error"].get("code", "")).lower()
    return any(m in blob for m in TRANSIENT_MARKERS)


def _quote(s: str) -> str:
    return f'"{s}"' if " " in s else s


# ---------------------------------------------------------------------------
# Assertion engine
# ---------------------------------------------------------------------------

def extract_text(result: dict) -> str:
    """
    Pull the most assertion-worthy string out of a winapp --json result.
    winapp get-value -> {text: "..."}; get-property -> {property: value} or
    {properties: {...}}; wait-for -> {found: true, ...}. Fall back to the whole
    JSON (or raw stdout) stringified so `contains`/`regex` still have something.
    """
    j = result.get("json")
    if isinstance(j, dict):
        for key in ("text", "value", "Value", "name", "Name"):
            if key in j and j[key] is not None:
                return str(j[key])
        # get-property single/multi
        if "property" in j and "value" in j:
            return str(j["value"])
        if isinstance(j.get("properties"), dict):
            return json.dumps(j["properties"], ensure_ascii=False)
        return json.dumps(j, ensure_ascii=False)
    if j is not None:
        return json.dumps(j, ensure_ascii=False)
    return (result.get("stdout") or "").strip()


def check_expect(expect: dict, result: dict) -> tuple[bool, str]:
    """
    Evaluate an `expect` assertion against a winapp result.
    Supported keys (any combination; all must hold):
      contains        substring must appear (case-insensitive unless ci=false)
      not_contains    substring must NOT appear
      equals          full extracted text must equal this (trimmed)
      regex           extracted text must match this pattern
      exit_code       winapp process exit code must equal this int
      found           bool: for wait-for, require found == value
    Returns (passed, human_detail).
    """
    text = extract_text(result)
    ci = expect.get("ci", True)
    hay = text.lower() if ci else text
    details: list[str] = []
    ok = True

    if "exit_code" in expect:
        want = int(expect["exit_code"])
        got = result.get("exit_code")
        cond = got == want
        ok &= cond
        details.append(f"exit_code={got} {'==' if cond else '!='} {want}")

    if "contains" in expect:
        needle = str(expect["contains"])
        n = needle.lower() if ci else needle
        cond = n in hay
        ok &= cond
        details.append(f"contains {needle!r}: {'yes' if cond else 'NO'}")

    if "not_contains" in expect:
        needle = str(expect["not_contains"])
        n = needle.lower() if ci else needle
        cond = n not in hay
        ok &= cond
        details.append(f"not_contains {needle!r}: {'yes' if cond else 'NO'}")

    if "equals" in expect:
        want = str(expect["equals"])
        a, b = (text.strip(), want.strip())
        if ci:
            a, b = a.lower(), b.lower()
        cond = a == b
        ok &= cond
        details.append(f"equals {want!r}: {'yes' if cond else 'NO'}")

    if "regex" in expect:
        pat = str(expect["regex"])
        flags = re.IGNORECASE if ci else 0
        cond = re.search(pat, text, flags) is not None
        ok &= cond
        details.append(f"regex {pat!r}: {'match' if cond else 'NO MATCH'}")

    if "found" in expect:
        want = bool(expect["found"])
        j = result.get("json") or {}
        got = bool(j.get("found", result.get("ok")))
        cond = got == want
        ok &= cond
        details.append(f"found={got} want {want}")

    detail = f"text={text!r} | " + "; ".join(details) if details else f"text={text!r}"
    return ok, detail


# ---------------------------------------------------------------------------
# Check execution
# ---------------------------------------------------------------------------

def execute_check(check: dict, target: dict, winapp: str,
                  step_pause: float, timeout: int, retries: int = 2) -> dict:
    """Run all steps of a single check; a step's `expect` gates PASS/FAIL."""
    cid = check.get("id", "<no-id>")
    priority = str(check.get("priority", "P2")).upper()
    log(f"CHECK {cid} [{priority}] - {check.get('description', '')}")

    step_results = []
    passed = True
    fail_reason = ""

    for i, step in enumerate(check.get("steps", [])):
        verb = step.get("verb")
        if not verb:
            passed = False
            fail_reason = f"step {i}: missing 'verb'"
            break
        selector = step.get("selector")
        value = step.get("value")
        extra = step.get("args")

        res = run_winapp(verb, target, selector=selector, value=value,
                         extra=extra, winapp=winapp, timeout=timeout,
                         retries=retries)

        expect = step.get("expect")
        step_ok = res["ok"]

        if expect is not None:
            exp_ok, exp_detail = check_expect(expect, res)
            step_ok = res["ok"] and exp_ok
            detail = exp_detail
        elif not res["ok"]:
            # An action step that failed to execute fails the whole check.
            j = res.get("json")
            if isinstance(j, dict) and isinstance(j.get("error"), dict):
                detail = j["error"].get("message") or j["error"].get("code") or "error"
            else:
                detail = res["stderr"].strip() or f"exit {res['exit_code']}"
        else:
            # Successful action step: keep a compact one-line detail.
            j = res.get("json")
            if isinstance(j, dict):
                detail = "; ".join(
                    f"{k}={j[k]}" for k in ("pattern", "elementId") if k in j
                ) or "ok"
            else:
                detail = " ".join((res["stdout"] or "ok").split())

        step_results.append({
            "index": i, "verb": verb, "selector": selector, "value": value,
            "ok": step_ok, "exit_code": res["exit_code"], "detail": detail,
        })
        log(f"  step {i} {verb} {selector or ''} -> {'OK' if step_ok else 'FAIL'}: {detail}")

        if not step_ok:
            passed = False
            fail_reason = f"step {i} ({verb} {selector or ''}): {detail}"
            break
        if step_pause:
            time.sleep(step_pause)

    return {
        "id": cid,
        "priority": priority if priority in VALID_PRIORITIES else "P2",
        "description": check.get("description", ""),
        "status": "PASS" if passed else "FAIL",
        "fail_reason": fail_reason,
        "steps": step_results,
    }


# ---------------------------------------------------------------------------
# Report building
# ---------------------------------------------------------------------------

def build_report(spec: dict, target: dict, results: list[dict]) -> dict:
    by_pri = {p: {"total": 0, "passed": 0, "failed": 0} for p in VALID_PRIORITIES}
    for r in results:
        b = by_pri[r["priority"]]
        b["total"] += 1
        b["passed" if r["status"] == "PASS" else "failed"] += 1

    p0_failed = by_pri["P0"]["failed"]
    gate = "PASS" if p0_failed == 0 else "FAIL"
    total = len(results)
    passed = sum(1 for r in results if r["status"] == "PASS")

    return {
        "generated": datetime.now(timezone.utc).isoformat(),
        "app": spec.get("name") or target.get("app") or target.get("window"),
        "target": target,
        "gate": gate,
        "gate_rule": "all P0 checks must PASS",
        "summary": {"total": total, "passed": passed, "failed": total - passed},
        "by_priority": by_pri,
        "checks": results,
    }


def report_to_markdown(report: dict) -> str:
    gate = report["gate"]
    badge = "✅ PASS" if gate == "PASS" else "❌ FAIL"
    s = report["summary"]
    lines = [
        f"# Sign-off Report — {report['app']}",
        "",
        f"- **Gate:** {badge} ({report['gate_rule']})",
        f"- **Generated:** {report['generated']}",
        f"- **Target:** `{json.dumps(report['target'])}`",
        f"- **Totals:** {s['passed']}/{s['total']} passed, {s['failed']} failed",
        "",
        "## Results by priority",
        "",
        "| Priority | Passed | Failed | Total |",
        "|----------|--------|--------|-------|",
    ]
    for p in VALID_PRIORITIES:
        b = report["by_priority"][p]
        lines.append(f"| {p} | {b['passed']} | {b['failed']} | {b['total']} |")
    lines.append("")

    for p in VALID_PRIORITIES:
        checks = [c for c in report["checks"] if c["priority"] == p]
        if not checks:
            continue
        lines.append(f"## {p} checks")
        lines.append("")
        for c in checks:
            mark = "✅" if c["status"] == "PASS" else "❌"
            lines.append(f"### {mark} `{c['id']}` — {c['description']}")
            if c["status"] == "FAIL":
                lines.append(f"> **Failure:** {c['fail_reason']}")
            lines.append("")
            lines.append("| # | verb | selector | ok | detail |")
            lines.append("|---|------|----------|----|--------|")
            for st in c["steps"]:
                ok = "✅" if st["ok"] else "❌"
                det = (st["detail"] or "").replace("|", "\\|").replace("\n", " ")
                if len(det) > 160:
                    det = det[:157] + "..."
                lines.append(
                    f"| {st['index']} | {st['verb']} | "
                    f"`{st['selector'] or ''}` | {ok} | {det} |"
                )
            lines.append("")
    return "\n".join(lines)


# ---------------------------------------------------------------------------
# discover mode
# ---------------------------------------------------------------------------

def flatten_inspect(data: dict) -> list[dict]:
    """winapp ui inspect --json -> flat list of element dicts across windows."""
    elements: list[dict] = []
    for win in data.get("windows", []):
        hwnd = win.get("hwnd")
        title = win.get("title")
        for el in win.get("elements", []):
            el = dict(el)
            el["_hwnd"] = hwnd
            el["_windowTitle"] = title
            elements.append(el)
    return elements


def is_noise(el: dict) -> bool:
    if el.get("automationId") in NOISE_AUTOMATION_IDS:
        return True
    name = el.get("name") or ""
    if NOISE_NAME_RE.match(name.strip()):
        return True
    if el.get("isEnabled") is False:
        return True
    if el.get("isOffscreen") is True:
        return True
    return False


def discover(target: dict, winapp: str, timeout: int,
             include_noise: bool) -> dict:
    log("discover: inspecting interactive elements...")
    res = run_winapp("inspect", target, extra=["-i"], winapp=winapp, timeout=timeout)
    if not res["ok"] or not isinstance(res["json"], dict):
        raise WinappError(
            "inspect failed: " + (res["stderr"].strip() or "no JSON returned"))

    elements = flatten_inspect(res["json"])
    invokable = [e for e in elements if e.get("isInvokable")]
    kept = invokable if include_noise else [e for e in invokable if not is_noise(e)]
    log(f"discover: {len(elements)} elements, {len(invokable)} invokable, "
        f"{len(kept)} after no-op filter")

    checks = []
    for i, el in enumerate(kept):
        sel = el.get("selector")
        name = el.get("name") or el.get("automationId") or sel
        checks.append({
            "id": f"invoke-{re.sub(r'[^a-z0-9]+', '-', (name or 'el').lower()).strip('-') or f'el{i}'}",
            "priority": "P2",
            "description": f"Invoke '{name}' ({el.get('type')})",
            "steps": [
                {"verb": "invoke", "selector": sel},
                # TODO: add a get-value/get-property step with an `expect`
                # assertion to make this a meaningful capability check.
            ],
        })

    return {
        "name": target.get("app") or f"window-{target.get('window')}",
        "target": target,
        "_note": "STARTER SKELETON from discover. Replace P2 stubs with real "
                 "capability checks: chain steps and add `expect` assertions. "
                 "See references/capability-spec.md.",
        "checks": checks,
    }


# ---------------------------------------------------------------------------
# spec loading / validation
# ---------------------------------------------------------------------------

def load_spec(path: Path) -> dict:
    spec = json.loads(path.read_text(encoding="utf-8"))
    if "checks" not in spec or not isinstance(spec["checks"], list):
        raise ValueError("spec must contain a 'checks' array")
    seen = set()
    for c in spec["checks"]:
        cid = c.get("id")
        if not cid:
            raise ValueError("every check needs an 'id'")
        if cid in seen:
            raise ValueError(f"duplicate check id: {cid}")
        seen.add(cid)
        pri = str(c.get("priority", "P2")).upper()
        if pri not in VALID_PRIORITIES:
            raise ValueError(f"check {cid}: priority must be P0/P1/P2, got {pri!r}")
        if not isinstance(c.get("steps"), list) or not c["steps"]:
            raise ValueError(f"check {cid}: 'steps' must be a non-empty array")
    return spec


def resolve_target(spec: dict, args) -> dict:
    """CLI --app/--window override spec 'target'."""
    target = dict(spec.get("target", {})) if isinstance(spec.get("target"), dict) else {}
    if args.app:
        target["app"] = args.app
    if args.window:
        target["window"] = args.window
    return target


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def build_parser() -> argparse.ArgumentParser:
    p = argparse.ArgumentParser(
        prog="signoff.py",
        description="Execute a P0/P1/P2 UI sign-off suite against a running "
                    "Windows app via winappcli, or discover a starter spec.",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=(
            "examples:\n"
            "  # discover a starter spec from a running app (by HWND or process)\n"
            "  python signoff.py discover --window 4065634 --out spec.json\n"
            "  python signoff.py discover --app Notepad --out spec.json\n\n"
            "  # run a spec and write JSON+Markdown report\n"
            "  python signoff.py run --spec spec.json --window 4065634 "
            "--report-json out.json --report-md out.md\n"
        ),
    )
    sub = p.add_subparsers(dest="mode", required=True)

    common = argparse.ArgumentParser(add_help=False)
    common.add_argument("-a", "--app",
                        help="Target app: process name, window title, or PID.")
    common.add_argument("-w", "--window", help="Target window HWND.")
    common.add_argument("--winapp", default=WINAPP,
                        help="Path to the winapp executable (default: on PATH).")
    common.add_argument("--timeout", type=int, default=60,
                        help="Per-winapp-call timeout in seconds (default 60).")
    common.add_argument("--retries", type=int, default=2,
                        help="Retries for transient UIA failures (default 2).")

    pr = sub.add_parser("run", parents=[common], help="Run a capability spec.")
    pr.add_argument("--spec", required=True, type=Path, help="Capability spec JSON.")
    pr.add_argument("--report-json", type=Path, help="Write JSON report here.")
    pr.add_argument("--report-md", type=Path, help="Write Markdown report here.")
    pr.add_argument("--step-pause", type=float, default=0.15,
                    help="Seconds to sleep between steps (default 0.15).")
    pr.add_argument("--gate-only", action="store_true",
                    help="Print only the gate result (PASS/FAIL) to stdout.")

    pd = sub.add_parser("discover", parents=[common],
                        help="Emit a starter spec from a running app.")
    pd.add_argument("--out", type=Path, help="Write skeleton spec here (else stdout).")
    pd.add_argument("--include-noise", action="store_true",
                    help="Keep window chrome / disabled / offscreen elements.")

    return p


def cmd_run(args) -> int:
    spec = load_spec(args.spec)
    target = resolve_target(spec, args)
    log(f"target: {json.dumps(target)}")

    results = [execute_check(c, target, args.winapp, args.step_pause,
                             args.timeout, args.retries)
               for c in spec["checks"]]
    report = build_report(spec, target, results)

    if args.report_json:
        args.report_json.write_text(
            json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
        log(f"wrote {args.report_json}")
    if args.report_md:
        args.report_md.write_text(report_to_markdown(report), encoding="utf-8")
        log(f"wrote {args.report_md}")

    if args.gate_only:
        print(report["gate"])
    else:
        print(report_to_markdown(report))

    s = report["summary"]
    log(f"GATE {report['gate']} | {s['passed']}/{s['total']} passed")
    return 0 if report["gate"] == "PASS" else 1


def cmd_discover(args) -> int:
    target = {}
    if args.app:
        target["app"] = args.app
    if args.window:
        target["window"] = args.window
    if not target:
        log("error: discover needs --app or --window")
        return 2
    skeleton = discover(target, args.winapp, args.timeout, args.include_noise)
    text = json.dumps(skeleton, indent=2, ensure_ascii=False)
    if args.out:
        args.out.write_text(text, encoding="utf-8")
        log(f"wrote {args.out} ({len(skeleton['checks'])} stub checks)")
    else:
        print(text)
    return 0


def main(argv: list[str] | None = None) -> int:
    # Windows consoles default to cp1252; emoji in reports would crash on print.
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding="utf-8")  # type: ignore[attr-defined]
        except (AttributeError, ValueError):
            pass
    args = build_parser().parse_args(argv)
    try:
        if args.mode == "run":
            return cmd_run(args)
        if args.mode == "discover":
            return cmd_discover(args)
    except (WinappError, ValueError, FileNotFoundError) as exc:
        log(f"error: {exc}")
        return 2
    except KeyboardInterrupt:
        log("interrupted")
        return 130
    return 2


if __name__ == "__main__":
    raise SystemExit(main())
