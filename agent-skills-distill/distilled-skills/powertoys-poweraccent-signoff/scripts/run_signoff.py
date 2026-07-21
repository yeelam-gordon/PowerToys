#!/usr/bin/env python3
"""PowerAccent (Quick Accent) behavioral + lifecycle sign-off harness.

REAL execution against freshly-built PowerToys binaries. Three real executors:

  * vstest    -- runs the module's own MSTest DLLs (PowerAccent.Common/Core.UnitTests)
                 once each via vstest.console, parses per-test outcomes from a TRX,
                 and evaluates each capability as the AND of its mapped test methods.
  * glyph     -- runs GlyphDriver.exe, which reflection-loads the freshly-built
                 PowerAccent.Common.dll and asserts EXACT end-user glyph sets
                 (pins specific accents the data-invariant unit tests do not catch).
  * lifecycle -- launches the real PowerToys.PowerAccent.exe and asserts launch/enable,
                 single-instance mutex, and clean POWERACCENT_EXIT_EVENT shutdown.

IMPORTANT: the end-user overlay-summon UIA path (hold letter + activation key) is
NOT covered here. Synthetic input (SendInput/keybd_event) is denied with
ERROR_ACCESS_DENIED under an RDP session that does not own the input queue, so the
overlay cannot be summoned or UIA-verified from such a session. This harness signs
off the reachable behavioral + lifecycle surface that FEEDS and HOSTS the overlay.
Run it on the interactive console (input-owning) session to additionally exercise
the blocked overlay checks. See SKILL.md "Coverage & Limits".

Usage:
    python run_signoff.py [options]

    --spec PATH        Capability spec JSON  (default: ../assets/poweraccent.spec.json)
    --release DIR      PowerToys x64\\Release root (default: $POWERTOYS_RELEASE, else
                       $POWERTOYS_ROOT\\x64\\Release; no machine-path default)
    --vstest PATH      vstest.console.exe path (default: auto-detect under VS 18)
    --glyph-exe PATH   Prebuilt GlyphDriver.exe (default: auto-detect under ./glyphdriver/bin)
    --out-json PATH    Report JSON out       (default: ./results.json)
    --out-md PATH      Report Markdown out   (default: ./report_generated.md)
    --skip KINDS       Comma list of executor kinds to skip: vstest,glyph,lifecycle

Exit code: 0 = GATE PASS, 1 = GATE FAIL (a P0 check failed), 2 = setup error.
"""
import argparse
import ctypes
import json
import os
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

HERE = Path(__file__).resolve().parent
EXIT_EVENT = r"Local\PowerToysPowerAccentExitEvent-53e93389-d19a-4fbb-9b36-1981c8965e17"


def log(msg):
    print(f"[signoff] {msg}", flush=True)


# --------------------------------------------------------------------------
# report builders (self-contained; gated on P0)
# --------------------------------------------------------------------------
def build_report(spec, target, results):
    order = {"P0": 0, "P1": 1, "P2": 2}
    results = sorted(results, key=lambda r: (order.get(r["priority"], 9), r["id"]))
    total = len(results)
    passed = sum(1 for r in results if r["status"] == "PASS")
    p0_fail = [r for r in results if r["priority"] == "P0" and r["status"] != "PASS"]
    gate = "FAIL" if p0_fail else "PASS"
    by_prio = {}
    for r in results:
        b = by_prio.setdefault(r["priority"], {"total": 0, "passed": 0})
        b["total"] += 1
        b["passed"] += 1 if r["status"] == "PASS" else 0
    return {
        "spec": spec.get("name"), "target": target, "gate": gate,
        "summary": {"total": total, "passed": passed, "failed": total - passed,
                    "by_priority": by_prio},
        "results": results,
    }


def report_to_markdown(report):
    s = report["summary"]
    lines = [f"# Sign-off Report — {report['spec']}", "",
             f"**Gate:** {report['gate']}  |  **Passed:** {s['passed']}/{s['total']}", ""]
    for prio in ("P0", "P1", "P2"):
        rows = [r for r in report["results"] if r["priority"] == prio]
        if not rows:
            continue
        b = s["by_priority"].get(prio, {"passed": 0, "total": 0})
        lines += [f"## {prio} ({b['passed']}/{b['total']})", "",
                  "| id | status | description |", "|----|--------|-------------|"]
        for r in rows:
            desc = r["description"].replace("|", "\\|")
            lines.append(f"| `{r['id']}` | {r['status']} | {desc} |")
            if r["status"] != "PASS" and r.get("fail_reason"):
                lines.append(f"|  |  | _{r['fail_reason'][:300]}_ |")
        lines.append("")
    return "\n".join(lines)


# --------------------------------------------------------------------------
# vstest
# --------------------------------------------------------------------------
def run_vstest(vstest, dll, tag, trx_dir):
    trx = trx_dir / f"{tag}.trx"
    if trx.exists():
        trx.unlink()
    cmd = [str(vstest), str(dll), "/Platform:x64",
           f"/Logger:trx;LogFileName={trx.name}",
           "/ResultsDirectory:" + str(trx_dir)]
    log(f"vstest {tag}: {dll.name}")
    subprocess.run(cmd, capture_output=True, text=True)
    produced = trx if trx.exists() else None
    if produced is None:
        cands = list(trx_dir.rglob(trx.name)) + list(trx_dir.rglob("*.trx"))
        produced = cands[0] if cands else None
    return parse_trx(produced) if produced else {}


def parse_trx(trx):
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    out = {}
    for r in ET.parse(trx).getroot().findall(".//t:UnitTestResult", ns):
        out[r.get("testName") or ""] = r.get("outcome") or ""
    return out


def eval_vstest_check(check, outcomes):
    steps, ok_all = [], True
    for i, tname in enumerate(check["tests"], 1):
        matched = {n: o for n, o in outcomes.items()
                   if n == tname or n.startswith(tname + " ") or tname in n}
        if not matched:
            ok_all = False
            steps.append(step(i, "mstest", tname, False, "no matching test result found"))
            continue
        ok = all(o == "Passed" for o in matched.values())
        ok_all = ok_all and ok
        steps.append(step(i, "mstest", tname, ok,
                          ", ".join(f"{n}={o}" for n, o in matched.items())[:400]))
    return finalize(check, ok_all, steps)


# --------------------------------------------------------------------------
# glyph driver
# --------------------------------------------------------------------------
def run_glyph(glyph_exe, common_data_dll):
    log(f"glyph driver: {glyph_exe.name} <- {common_data_dll.name}")
    env = dict(os.environ)
    env["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "0"
    p = subprocess.run([str(glyph_exe), str(common_data_dll)],
                       capture_output=True, text=True, encoding="utf-8", env=env)
    line = p.stdout.strip().splitlines()[-1] if p.stdout.strip() else "[]"
    return {r["id"]: r for r in json.loads(line)}


def eval_glyph_check(check, glyphs):
    r = glyphs.get(check["id"])
    if r is None:
        return finalize(check, False, [step(1, "glyph", check["id"], False, "no glyph result")])
    return finalize(check, r["ok"], [step(1, "glyph", check["id"], r["ok"], r["detail"])])


# --------------------------------------------------------------------------
# lifecycle
# --------------------------------------------------------------------------
k32 = ctypes.WinDLL("kernel32", use_last_error=True)


def signal_exit_event():
    h = k32.OpenEventW(0x0002, False, EXIT_EVENT)  # EVENT_MODIFY_STATE
    if not h:
        return False
    ok = bool(k32.SetEvent(h))
    k32.CloseHandle(h)
    return ok


def eval_lifecycle_check(check, ctx):
    pa_exe, settings = ctx["pa_exe"], ctx["settings"]
    launch = lambda: subprocess.Popen([str(pa_exe)], cwd=str(pa_exe.parent))
    op = check["op"]
    if op == "launch_enable":
        steps = []
        if settings.exists():
            settings.unlink()
        proc = launch()
        ctx["proc"] = proc
        time.sleep(6)
        alive = proc.poll() is None
        steps.append(step(1, "launch", pa_exe.name, alive, f"pid={proc.pid} alive={alive}"))
        got = settings.exists()
        steps.append(step(2, "assert", "settings.json materialized", got, f"exists={got}"))
        return finalize(check, alive and got, steps)
    if op == "single_instance":
        steps = []
        first = ctx.get("proc")
        first_alive = first is not None and first.poll() is None
        steps.append(step(1, "precondition", "first instance resident", first_alive, ""))
        second = launch()
        exited = False
        for _ in range(20):
            time.sleep(0.5)
            if second.poll() is not None:
                exited = True
                break
        if not exited:
            try:
                second.terminate()
            except Exception:
                pass
        steps.append(step(2, "assert", "second instance self-exits (mutex)", exited,
                          f"code={second.returncode}"))
        still = first is not None and first.poll() is None
        steps.append(step(3, "assert", "first instance still resident", still, ""))
        return finalize(check, first_alive and exited and still, steps)
    if op == "clean_exit":
        steps = []
        first = ctx.get("proc")
        alive = first is not None and first.poll() is None
        steps.append(step(1, "precondition", "instance resident", alive, ""))
        signalled = signal_exit_event()
        steps.append(step(2, "signal", "POWERACCENT_EXIT_EVENT", signalled, f"ok={signalled}"))
        exited = False
        if first is not None:
            try:
                first.wait(timeout=15)
                exited = True
            except Exception:
                exited = False
        steps.append(step(3, "assert", "process exits on event", exited,
                          f"exit_code={getattr(first,'returncode',None)}"))
        return finalize(check, alive and signalled and exited, steps)
    return finalize(check, False, [step(1, "lifecycle", op, False, "unknown op")])


# --------------------------------------------------------------------------
# helpers
# --------------------------------------------------------------------------
def step(i, verb, selector, ok, detail):
    return {"index": i, "verb": verb, "selector": selector, "value": None,
            "ok": ok, "exit_code": 0 if ok else 1, "detail": detail}


def finalize(check, passed, steps):
    reason = ""
    if not passed:
        bad = next((s for s in steps if not s["ok"]), None)
        if bad:
            reason = f"step {bad['index']} ({bad['verb']} {bad['selector']}): {bad['detail']}"
    return {"id": check["id"], "priority": check["priority"],
            "description": check["description"],
            "status": "PASS" if passed else "FAIL", "fail_reason": reason, "steps": steps}


def autodetect_vstest():
    root = Path(r"C:\Program Files\Microsoft Visual Studio")
    hits = list(root.rglob("vstest.console.exe")) if root.exists() else []
    return hits[0] if hits else None


def autodetect_glyph(explicit):
    if explicit:
        return Path(explicit)
    base = HERE / "glyphdriver" / "bin"
    hits = list(base.rglob("GlyphDriver.exe")) if base.exists() else []
    return hits[0] if hits else None


def main():
    ap = argparse.ArgumentParser(description="PowerAccent behavioral+lifecycle sign-off")
    ap.add_argument("--spec", default=str(HERE.parent / "assets" / "poweraccent.spec.json"))
    ap.add_argument("--release", default=None,
                    help="PowerToys x64\\Release root (default: $POWERTOYS_RELEASE, "
                         "else $POWERTOYS_ROOT\\x64\\Release; no machine-path default)")
    ap.add_argument("--vstest", default=None)
    ap.add_argument("--glyph-exe", default=None)
    ap.add_argument("--out-json", default=str(HERE / "results.json"))
    ap.add_argument("--out-md", default=str(HERE / "report_generated.md"))
    ap.add_argument("--skip", default="")
    args = ap.parse_args()

    skip = {s.strip() for s in args.skip.split(",") if s.strip()}
    spec = json.loads(Path(args.spec).read_text(encoding="utf-8"))
    checks = [c for c in spec["checks"] if c["kind"] not in skip]

    release_arg = args.release or os.environ.get("POWERTOYS_RELEASE")
    if not release_arg and os.environ.get("POWERTOYS_ROOT"):
        release_arg = str(Path(os.environ["POWERTOYS_ROOT"]) / "x64" / "Release")
    if not release_arg:
        log("ERROR: PowerToys Release root not set. Pass --release <x64\\Release>, "
            "or set POWERTOYS_RELEASE / POWERTOYS_ROOT. No machine-path default is shipped.")
        return 2
    release = Path(release_arg)
    if not release.exists():
        log(f"ERROR: PowerToys Release root does not exist: {release}")
        return 2
    common_dll = release / "tests" / "PowerAccent.Common.UnitTests" / "PowerToys.PowerAccent.Common.UnitTests.dll"
    core_dll = release / "tests" / "PowerAccent.Core.UnitTests" / "PowerToys.PowerAccent.Core.UnitTests.dll"
    common_data_dll = release / "tests" / "PowerAccent.Common.UnitTests" / "PowerAccent.Common.dll"
    pa_exe = release / "WinUI3Apps" / "PowerToys.PowerAccent.exe"
    settings = Path(os.environ["LOCALAPPDATA"]) / "Microsoft" / "PowerToys" / "QuickAccent" / "settings.json"

    trx_dir = HERE / "workfiles"
    trx_dir.mkdir(exist_ok=True)

    need_common = any(c["kind"] == "vstest" and c.get("assembly") == "common" for c in checks)
    need_core = any(c["kind"] == "vstest" and c.get("assembly") == "core" for c in checks)
    need_glyph = any(c["kind"] == "glyph" for c in checks)

    vstest = Path(args.vstest) if args.vstest else autodetect_vstest()
    if (need_common or need_core) and (not vstest or not vstest.exists()):
        log("ERROR: vstest.console.exe not found — pass --vstest or --skip vstest")
        return 2
    glyph_exe = autodetect_glyph(args.glyph_exe)
    if need_glyph and (not glyph_exe or not glyph_exe.exists()):
        log("ERROR: GlyphDriver.exe not built — run scripts/run-signoff.ps1 (builds it) or --skip glyph")
        return 2

    common_out = run_vstest(vstest, common_dll, "common", trx_dir) if need_common else {}
    core_out = run_vstest(vstest, core_dll, "core", trx_dir) if need_core else {}
    glyphs = run_glyph(glyph_exe, common_data_dll) if need_glyph else {}

    ctx = {"pa_exe": pa_exe, "settings": settings}
    results = []
    try:
        for c in checks:
            if c["kind"] == "vstest":
                r = eval_vstest_check(c, common_out if c["assembly"] == "common" else core_out)
            elif c["kind"] == "glyph":
                r = eval_glyph_check(c, glyphs)
            elif c["kind"] == "lifecycle":
                r = eval_lifecycle_check(c, ctx)
            else:
                r = finalize(c, False, [step(1, "?", c["kind"], False, "unknown kind")])
            log(f"  {r['priority']} {r['id']}: {r['status']}")
            results.append(r)
    finally:
        proc = ctx.get("proc")
        if proc is not None and proc.poll() is None:
            signal_exit_event()
            time.sleep(2)
            if proc.poll() is None:
                try:
                    proc.terminate()
                except Exception:
                    pass

    target = {"app": spec["name"], "exe": str(pa_exe),
              "common_dll": str(common_dll), "core_dll": str(core_dll)}
    report = build_report(spec, target, results)
    Path(args.out_json).write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    Path(args.out_md).write_text(report_to_markdown(report), encoding="utf-8")
    log(f"GATE={report['gate']}  {report['summary']['passed']}/{report['summary']['total']} passed")
    log(f"wrote {args.out_json}, {args.out_md}")
    return 0 if report["gate"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
