#!/usr/bin/env python3
"""PowerAccent (Quick Accent) behavioral sign-off harness.

REAL execution against freshly-built binaries. Three real check kinds:
  * vstest   -- runs the module's own MSTest DLLs (PowerAccent.Common/Core.UnitTests)
                once each via vstest.console, parses per-test outcomes from a TRX,
                and evaluates each capability as the AND of its mapped test methods.
  * glyph    -- runs GlyphDriver.exe, which reflection-loads the freshly-built
                PowerAccent.Common.dll and asserts EXACT end-user glyph sets
                (pins specific accents the data-invariant unit tests do not).
  * lifecycle-- launches the real PowerToys.PowerAccent.exe and asserts
                launch/enable, single-instance mutex, and clean POWERACCENT_EXIT_EVENT
                shutdown.

The overlay-summon UIA path is BLOCKED in this session by synthetic-input denial
(documented in report.md); this harness signs off the reachable behavioral +
lifecycle surface that feeds/hosts the overlay. Reuses build_report /
report_to_markdown from the app-signoff-uia skill for identical report shape.
"""
import ctypes
import json
import os
import subprocess
import sys
import time
import xml.etree.ElementTree as ET
from pathlib import Path

# Reuse the skill's report builders for an identical report contract.
SKILL_SCRIPTS = Path(r"C:\s\Demo\SkillForDistill\.github\skills\app-signoff-uia\scripts")
sys.path.insert(0, str(SKILL_SCRIPTS))
from signoff import build_report, report_to_markdown  # noqa: E402

HERE = Path(__file__).resolve().parent
RELEASE = Path(r"C:\s\powertoys\x64\Release")
VSTEST = Path(r"C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\IDE\Extensions\TestPlatform\vstest.console.exe")
COMMON_DLL = RELEASE / "tests" / "PowerAccent.Common.UnitTests" / "PowerToys.PowerAccent.Common.UnitTests.dll"
CORE_DLL = RELEASE / "tests" / "PowerAccent.Core.UnitTests" / "PowerToys.PowerAccent.Core.UnitTests.dll"
COMMON_DATA_DLL = RELEASE / "tests" / "PowerAccent.Common.UnitTests" / "PowerAccent.Common.dll"
PA_EXE = RELEASE / "WinUI3Apps" / "PowerToys.PowerAccent.exe"
SETTINGS = Path(os.environ["LOCALAPPDATA"]) / "Microsoft" / "PowerToys" / "QuickAccent" / "settings.json"
EXIT_EVENT = r"Local\PowerToysPowerAccentExitEvent-53e93389-d19a-4fbb-9b36-1981c8965e17"
GLYPH_EXE = next((HERE / "glyphdriver" / "bin" / "Release").rglob("GlyphDriver.exe"))

TRX_DIR = HERE / "workfiles"
TRX_DIR.mkdir(exist_ok=True)


def log(msg):
    print(f"[signoff] {msg}", flush=True)


# --------------------------------------------------------------------------
# vstest: run each DLL once, parse TRX -> {testName: outcome}
# --------------------------------------------------------------------------
def run_vstest(dll: Path, tag: str) -> dict:
    trx = TRX_DIR / f"{tag}.trx"
    if trx.exists():
        trx.unlink()
    cmd = [str(VSTEST), str(dll), "/Platform:x64",
           f"/Logger:trx;LogFileName={trx.name}",
           "/ResultsDirectory:" + str(TRX_DIR)]
    log(f"vstest {tag}: {dll.name}")
    subprocess.run(cmd, capture_output=True, text=True)
    # vstest may honor ResultsDirectory or write under TestResults; find the trx.
    produced = trx if trx.exists() else None
    if produced is None:
        cands = list(TRX_DIR.rglob(trx.name)) + list(TRX_DIR.rglob("*.trx"))
        produced = cands[0] if cands else None
    if produced is None:
        return {}
    return parse_trx(produced)


def parse_trx(trx: Path) -> dict:
    ns = {"t": "http://microsoft.com/schemas/VisualStudio/TeamTest/2010"}
    tree = ET.parse(trx)
    out = {}
    for r in tree.getroot().findall(".//t:UnitTestResult", ns):
        name = r.get("testName") or ""
        outcome = r.get("outcome") or ""
        out[name] = outcome
    return out


def eval_vstest_check(check, outcomes: dict) -> dict:
    steps, ok_all = [], True
    for i, tname in enumerate(check["tests"], 1):
        matched = {n: o for n, o in outcomes.items()
                   if n == tname or n.startswith(tname + " ") or tname in n}
        if not matched:
            ok_all = False
            steps.append({"index": i, "verb": "mstest", "selector": tname, "value": None,
                          "ok": False, "exit_code": 1, "detail": "no matching test result found"})
            continue
        passed = all(o == "Passed" for o in matched.values())
        ok_all = ok_all and passed
        detail = ", ".join(f"{n}={o}" for n, o in matched.items())
        steps.append({"index": i, "verb": "mstest", "selector": tname, "value": None,
                      "ok": passed, "exit_code": 0 if passed else 1,
                      "detail": detail[:400]})
    return finalize(check, ok_all, steps)


# --------------------------------------------------------------------------
# glyph driver: run once -> {id: {ok, detail}}
# --------------------------------------------------------------------------
def run_glyph() -> dict:
    log(f"glyph driver: {GLYPH_EXE.name} <- {COMMON_DATA_DLL.name}")
    env = dict(os.environ)
    env["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "0"
    p = subprocess.run([str(GLYPH_EXE), str(COMMON_DATA_DLL)],
                       capture_output=True, text=True, encoding="utf-8", env=env)
    line = p.stdout.strip().splitlines()[-1] if p.stdout.strip() else "[]"
    arr = json.loads(line)
    return {r["id"]: r for r in arr}


def eval_glyph_check(check, glyphs: dict) -> dict:
    r = glyphs.get(check["id"])
    if r is None:
        return finalize(check, False, [step(1, "glyph", check["id"], False, "no glyph result")])
    return finalize(check, r["ok"], [step(1, "glyph", check["id"], r["ok"], r["detail"])])


# --------------------------------------------------------------------------
# lifecycle: real process launch / mutex / exit-event
# --------------------------------------------------------------------------
k32 = ctypes.WinDLL("kernel32", use_last_error=True)
EVENT_MODIFY_STATE = 0x0002


def signal_exit_event() -> bool:
    h = k32.OpenEventW(EVENT_MODIFY_STATE, False, EXIT_EVENT)
    if not h:
        return False
    ok = bool(k32.SetEvent(h))
    k32.CloseHandle(h)
    return ok


def launch_pa():
    return subprocess.Popen([str(PA_EXE)], cwd=str(PA_EXE.parent))


def eval_lifecycle_check(check, ctx) -> dict:
    op = check["op"]
    if op == "launch_enable":
        return lc_launch_enable(check, ctx)
    if op == "single_instance":
        return lc_single_instance(check, ctx)
    if op == "clean_exit":
        return lc_clean_exit(check, ctx)
    return finalize(check, False, [step(1, "lifecycle", op, False, "unknown op")])


def lc_launch_enable(check, ctx):
    steps = []
    if SETTINGS.exists():
        SETTINGS.unlink()
    proc = launch_pa()
    ctx["proc"] = proc
    time.sleep(6)
    alive = proc.poll() is None
    steps.append(step(1, "launch", str(PA_EXE.name), alive,
                      f"pid={proc.pid} alive={alive}"))
    got_settings = SETTINGS.exists()
    steps.append(step(2, "assert", "settings.json materialized", got_settings,
                      f"exists={got_settings} path={SETTINGS}"))
    return finalize(check, alive and got_settings, steps)


def lc_single_instance(check, ctx):
    steps = []
    first = ctx.get("proc")
    first_alive = first is not None and first.poll() is None
    steps.append(step(1, "precondition", "first instance resident", first_alive,
                      f"first_pid={getattr(first,'pid',None)} alive={first_alive}"))
    second = launch_pa()
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
                      f"second_pid={second.pid} exited={exited} code={second.returncode}"))
    still = first is not None and first.poll() is None
    steps.append(step(3, "assert", "first instance still resident", still,
                      f"first alive={still}"))
    return finalize(check, first_alive and exited and still, steps)


def lc_clean_exit(check, ctx):
    steps = []
    first = ctx.get("proc")
    alive = first is not None and first.poll() is None
    steps.append(step(1, "precondition", "instance resident", alive,
                      f"alive={alive}"))
    signalled = signal_exit_event()
    steps.append(step(2, "signal", "POWERACCENT_EXIT_EVENT", signalled,
                      f"OpenEvent+SetEvent ok={signalled}"))
    exited = False
    if first is not None:
        try:
            first.wait(timeout=15)
            exited = True
        except Exception:
            exited = False
    steps.append(step(3, "assert", "process exits on event", exited,
                      f"exit_code={getattr(first,'returncode',None)} exited={exited}"))
    return finalize(check, alive and signalled and exited, steps)


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
def main():
    spec = json.loads((HERE / "poweraccent.spec.json").read_text(encoding="utf-8"))
    checks = spec["checks"]

    need_common = any(c["kind"] == "vstest" and c["assembly"] == "common" for c in checks)
    need_core = any(c["kind"] == "vstest" and c["assembly"] == "core" for c in checks)
    need_glyph = any(c["kind"] == "glyph" for c in checks)

    common_out = run_vstest(COMMON_DLL, "common") if need_common else {}
    core_out = run_vstest(CORE_DLL, "core") if need_core else {}
    glyphs = run_glyph() if need_glyph else {}

    ctx = {}
    results = []
    try:
        for c in checks:
            if c["kind"] == "vstest":
                outcomes = common_out if c["assembly"] == "common" else core_out
                r = eval_vstest_check(c, outcomes)
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

    target = {"app": spec["name"], "exe": str(PA_EXE),
              "common_dll": str(COMMON_DLL), "core_dll": str(CORE_DLL)}
    report = build_report(spec, target, results)

    out_json = HERE / "results.json"
    out_md = HERE / "report_generated.md"
    out_json.write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    out_md.write_text(report_to_markdown(report), encoding="utf-8")
    log(f"GATE={report['gate']}  {report['summary']['passed']}/{report['summary']['total']} passed")
    log(f"wrote {out_json.name}, {out_md.name}")
    return 0 if report["gate"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
