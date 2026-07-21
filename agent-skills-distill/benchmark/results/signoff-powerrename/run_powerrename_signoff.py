#!/usr/bin/env python3
"""
run_powerrename_signoff.py - Execute the PowerRename capability spec against the
REAL PowerToys.PowerRename.exe WinUI 3 rename window.

Why a harness instead of `signoff.py run` directly?
  * PowerRename's two text boxes (Search for / Replace with) have per-session
    hashed winappcli slugs (txt-textbox-XXXX) that change on every launch, so they
    must be resolved at runtime.
  * PowerRename checkboxes/toggles only support the UIA Toggle pattern (no
    deterministic set-state), so cross-check state would leak. To guarantee every
    check starts from PowerRename's default flag state, this harness launches a
    FRESH app instance per check.

It still reuses the skill's engine: signoff.execute_check / build_report /
report_to_markdown. Preview is verified WITHOUT clicking Apply (non-destructive).
"""
from __future__ import annotations
import argparse, copy, importlib.util, json, re, subprocess, sys, time
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")

SKILL_DIR = Path(r"C:\s\Demo\SkillForDistill\.github\skills\app-signoff-uia")
EXE = Path(r"C:\s\powertoys\x64\Release\WinUI3Apps\PowerToys.PowerRename.exe")


def load_signoff():
    spec = importlib.util.spec_from_file_location("signoff", SKILL_DIR / "scripts" / "signoff.py")
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def winapp_json(args: list[str]) -> dict | None:
    p = subprocess.run(["winapp", "ui", *args, "--json"], capture_output=True, text=True, timeout=60)
    out = (p.stdout or "").strip()
    try:
        return json.loads(out)
    except json.JSONDecodeError:
        return None


def launch(files: list[str]) -> subprocess.Popen:
    return subprocess.Popen([str(EXE), *files])


def find_hwnd(timeout: float = 15.0) -> str | None:
    deadline = time.time() + timeout
    while time.time() < deadline:
        p = subprocess.run(["winapp", "ui", "list-windows"], capture_output=True, text=True, timeout=30)
        blob = " ".join((p.stdout or "").split())
        m = re.search(r'HWND (\d+): "PowerRename"', blob)
        if m:
            return m.group(1)
        time.sleep(0.5)
    return None


def resolve_edit_slug(hwnd: str, name: str) -> str | None:
    j = winapp_json(["search", name, "-w", hwnd])
    if not j:
        return None
    for match in j.get("matches", []):
        if match.get("type") == "Edit":
            return match.get("selector")
    return None


def kill(pid: int) -> None:
    subprocess.run(["taskkill", "/PID", str(pid), "/T", "/F"],
                   capture_output=True, text=True)


def substitute(check: dict, search_slug: str, replace_slug: str) -> dict:
    c = copy.deepcopy(check)
    for step in c.get("steps", []):
        sel = step.get("selector")
        if sel == "__SEARCH_SLUG__":
            step["selector"] = search_slug
        elif sel == "__REPLACE_SLUG__":
            step["selector"] = replace_slug
    return c


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--spec", required=True)
    ap.add_argument("--files", nargs="+", required=True, help="sample files to open in PowerRename")
    ap.add_argument("--report-json", required=True)
    ap.add_argument("--report-md", required=True)
    ap.add_argument("--step-pause", type=float, default=0.9)
    ap.add_argument("--timeout", type=int, default=60)
    ap.add_argument("--retries", type=int, default=3)
    ap.add_argument("--settle", type=float, default=3.0, help="seconds after launch before driving UI")
    args = ap.parse_args()

    sf = load_signoff()
    spec = json.loads(Path(args.spec).read_text(encoding="utf-8"))
    results = []

    for check in spec["checks"]:
        cid = check.get("id")
        print(f"\n=== ISOLATED CHECK {cid} (fresh app) ===", file=sys.stderr)
        proc = launch(args.files)
        try:
            time.sleep(args.settle)
            hwnd = find_hwnd()
            if not hwnd:
                results.append({"id": cid, "priority": check.get("priority", "P2"),
                                "description": check.get("description", ""), "status": "FAIL",
                                "fail_reason": "could not locate PowerRename window", "steps": []})
                continue
            s_slug = resolve_edit_slug(hwnd, "Search for")
            r_slug = resolve_edit_slug(hwnd, "Replace with")
            if not s_slug or not r_slug:
                results.append({"id": cid, "priority": check.get("priority", "P2"),
                                "description": check.get("description", ""), "status": "FAIL",
                                "fail_reason": f"could not resolve edit slugs (search={s_slug}, replace={r_slug})",
                                "steps": []})
                continue
            concrete = substitute(check, s_slug, r_slug)
            target = {"window": hwnd}
            res = sf.execute_check(concrete, target, "winapp", args.step_pause, args.timeout, args.retries)
            results.append(res)
        finally:
            kill(proc.pid)
            time.sleep(1.0)

    report = sf.build_report(spec, {"window": "per-check", "app": "PowerToys.PowerRename"}, results)
    Path(args.report_json).write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    Path(args.report_md).write_text(sf.report_to_markdown(report), encoding="utf-8")

    print(f"\nGATE: {report['gate']}  ({report['summary']['passed']}/{report['summary']['total']} passed)", file=sys.stderr)
    for r in results:
        print(f"  [{r['priority']}] {r['id']}: {r['status']}"
              + (f" -- {r['fail_reason']}" if r["status"] == "FAIL" else ""), file=sys.stderr)
    return 0 if report["gate"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
