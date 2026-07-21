#!/usr/bin/env python3
"""AdvancedPaste sign-off harness (REAL execution, fail-fast, honest numbers).

Two real check kinds, both against freshly-built binaries:

  * vstest -- runs the module's own MSTest assembly (AdvancedPaste.UnitTests) once
              via its Microsoft.Testing.Platform host exe, emits a TRX, parses
              per-test outcomes, and evaluates each capability as the AND of its
              mapped test methods. The tests drive REAL product code
              (TransformHelpers / JsonHelper / MarkdownHelper / PasteFormats) with
              in-process DataPackage clipboard inputs -- i.e. the paste-as
              plain-text / markdown / json value the end user gets.

  * uia    -- drives the REAL "Advanced Paste" window through winappcli. The window
              is summoned WITHOUT the global hotkey (blocked over RDP) by
              impersonating the PowerToys Runner side of AdvancedPaste's named-pipe
              protocol and sending the "ShowUI" message. Asserts the three core
              paste-format actions are present and the AI prompt box is gated.

Reuses build_report / report_to_markdown from the app-signoff-uia skill for an
identical report contract.
"""
from __future__ import annotations

import json
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")

SKILL_SCRIPTS = Path(r"C:\s\Demo\SkillForDistill\.github\skills\app-signoff-uia\scripts")
sys.path.insert(0, str(SKILL_SCRIPTS))
from signoff import build_report, report_to_markdown  # noqa: E402

HERE = Path(__file__).resolve().parent
TEST_DIR = Path(r"C:\s\powertoys\x64\Release\tests\AdvancedPaste.UnitTests")
TEST_EXE = TEST_DIR / "AdvancedPaste.UnitTests.exe"
TRX_DIR = HERE / "workfiles"
TRX_DIR.mkdir(exist_ok=True)

VSTEST_FILTER = ("FullyQualifiedName~SignoffTransformTests|"
                 "FullyQualifiedName~ClipboardItemHelperTests")


def log(msg: str) -> None:
    print(f"[signoff] {msg}", flush=True)


# --------------------------------------------------------------------------
# vstest: run assembly once, parse TRX -> {testName: outcome}
# --------------------------------------------------------------------------
def run_vstest(tag: str) -> dict:
    trx_name = f"{tag}.trx"
    for stale in TRX_DIR.glob(trx_name):
        stale.unlink()
    import os
    env = dict(os.environ)
    env["TESTINGPLATFORM_TELEMETRY_OPTOUT"] = "1"
    cmd = [str(TEST_EXE), "--filter", VSTEST_FILTER,
           "--report-trx", "--report-trx-filename", trx_name,
           "--results-directory", str(TRX_DIR)]
    log("vstest: " + " ".join(cmd))
    proc = subprocess.run(cmd, capture_output=True, text=True, cwd=str(TEST_DIR), env=env)
    log("  " + " ".join((proc.stdout or "").split()[-8:]))
    produced = TRX_DIR / trx_name
    if not produced.exists():
        cands = list(TRX_DIR.rglob(trx_name)) or list(TRX_DIR.rglob("*.trx"))
        produced = cands[0] if cands else None
    if produced is None:
        return {}
    return parse_trx(produced)


def parse_trx(trx: Path) -> dict:
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    root = ET.parse(trx).getroot()
    out = {}
    for r in root.findall(".//t:UnitTestResult", ns):
        out[r.get("testName") or ""] = r.get("outcome") or ""
    return out


def eval_vstest_check(check: dict, outcomes: dict) -> dict:
    steps, ok_all = [], True
    for i, tname in enumerate(check["tests"], 1):
        matched = {n: o for n, o in outcomes.items()
                   if n == tname or n.startswith(tname + " ") or n.startswith(tname + "(") or tname in n}
        if not matched:
            ok_all = False
            steps.append(step(i, "mstest", tname, False, "no matching test result found"))
            continue
        passed = all(o == "Passed" for o in matched.values())
        ok_all = ok_all and passed
        detail = f"{len(matched)} result(s): " + ", ".join(f"{n}={o}" for n, o in list(matched.items())[:6])
        steps.append(step(i, "mstest", tname, passed, detail[:400]))
    return finalize(check, ok_all, steps)


# --------------------------------------------------------------------------
# uia: drive the real Advanced Paste window through winappcli
# --------------------------------------------------------------------------
def winapp_json(args: list[str]) -> dict | None:
    p = subprocess.run(["winapp", "ui", *args, "--json"],
                       capture_output=True, text=True, timeout=60)
    out = (p.stdout or "").strip()
    try:
        return json.loads(out)
    except json.JSONDecodeError:
        return None


def find_advancedpaste_hwnd() -> str | None:
    p = subprocess.run(["winapp", "ui", "list-windows"],
                       capture_output=True, text=True, timeout=30)
    blob = " ".join((p.stdout or "").split())
    m = re.search(r'HWND (\d+): "Advanced Paste".*?PowerToys\.AdvancedPaste', blob)
    return m.group(1) if m else None


def eval_uia_check(check: dict) -> dict:
    hwnd = find_advancedpaste_hwnd()
    if not hwnd:
        return finalize(check, False,
                        [step(1, "list-windows", "Advanced Paste", False,
                              "window not running (summon via launch_window.ps1)")])
    steps, ok_all = [], True
    for i, a in enumerate(check["assertions"], 1):
        if "search" in a:
            j = winapp_json(["search", a["search"], "-w", hwnd]) or {}
            cnt = j.get("matchCount", 0)
            ok = cnt >= a.get("min_count", 1)
            steps.append(step(i, "search", a["search"], ok,
                              f"matchCount={cnt} (>= {a.get('min_count', 1)})"))
        elif "get_property" in a:
            j = winapp_json(["get-property", a["get_property"], "-w", hwnd]) or {}
            props = j.get("properties", {})
            val = props.get(a["property"])
            ok = str(val) == str(a["equals"])
            steps.append(step(i, "get-property", f'{a["get_property"]}.{a["property"]}',
                              ok, f"value={val!r} expected={a['equals']!r}"))
        else:
            ok = False
            steps.append(step(i, "?", "unknown", False, "unknown assertion"))
        ok_all = ok_all and ok
    return finalize(check, ok_all, steps)


# --------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------
def step(i, verb, selector, ok, detail):
    return {"index": i, "verb": verb, "selector": selector, "value": None,
            "ok": ok, "exit_code": 0 if ok else 1, "detail": detail}


def finalize(check, passed, steps):
    fail_reason = ""
    if not passed:
        bad = next((s for s in steps if not s["ok"]), None)
        if bad:
            fail_reason = f"step {bad['index']} ({bad['verb']} {bad['selector']}): {bad['detail']}"
    return {"id": check["id"], "priority": check["priority"],
            "description": check["description"],
            "status": "PASS" if passed else "FAIL",
            "fail_reason": fail_reason, "steps": steps}


# --------------------------------------------------------------------------
# main
# --------------------------------------------------------------------------
def main() -> int:
    if len(sys.argv) < 2:
        print("usage: run_advancedpaste_signoff.py <report-basename> [--no-uia]", file=sys.stderr)
        return 2
    basename = sys.argv[1]
    skip_uia = "--no-uia" in sys.argv[2:]

    spec = json.loads((HERE / "advancedpaste.spec.json").read_text(encoding="utf-8"))
    checks = spec["checks"]

    need_vstest = any(c["kind"] == "vstest" for c in checks)
    outcomes = run_vstest(basename) if need_vstest else {}
    log(f"parsed {len(outcomes)} test outcomes")

    results = []
    for c in checks:
        if c["kind"] == "vstest":
            r = eval_vstest_check(c, outcomes)
        elif c["kind"] == "uia":
            if skip_uia:
                r = finalize(c, True, [step(1, "uia", "skipped", True, "skipped (--no-uia)")])
                r["status"] = "SKIP"
            else:
                r = eval_uia_check(c)
        else:
            r = finalize(c, False, [step(1, "?", c["kind"], False, "unknown kind")])
        log(f"  [{r['priority']}] {r['id']}: {r['status']}"
            + (f" -- {r['fail_reason']}" if r["status"] == "FAIL" else ""))
        results.append(r)

    # Treat SKIP as pass-through for gating (don't fail the gate on skipped uia).
    gate_results = [r for r in results if r["status"] != "SKIP"]
    target = {"app": "AdvancedPaste", "test_exe": str(TEST_EXE),
              "product_dll": str(TEST_DIR / "PowerToys.AdvancedPaste.dll")}
    report = build_report(spec, target, gate_results)
    # re-attach skipped for transparency
    report["checks"] = results
    report["skipped"] = [r["id"] for r in results if r["status"] == "SKIP"]

    (HERE / f"{basename}.json").write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    (HERE / f"{basename}.md").write_text(report_to_markdown(report), encoding="utf-8")
    log(f"GATE={report['gate']}  {report['summary']['passed']}/{report['summary']['total']} passed"
        + (f"  (skipped: {report['skipped']})" if report.get('skipped') else ""))
    log(f"wrote {basename}.json / {basename}.md")
    return 0 if report["gate"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
