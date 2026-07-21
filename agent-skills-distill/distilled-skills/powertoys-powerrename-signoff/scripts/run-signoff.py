#!/usr/bin/env python3
"""
run-signoff.py -- P0/P1/P2 UI sign-off of the REAL PowerToys PowerRename WinUI 3
rename window, driven through winappcli (`winapp ui`) UI Automation, WITH a
`winapp ui screenshot` captured per check.

Non-destructive: every assertion reads the live PREVIEW. Apply is NEVER clicked, so
the sample files are never renamed on disk.

Why a dedicated harness (instead of `signoff.py run` directly)?
  * PowerRename's two text boxes (Search for / Replace with) have PER-SESSION hashed
    winappcli slugs (txt-textbox-XXXX) that change on every launch, so they must be
    resolved at runtime (spec placeholders __SEARCH_SLUG__ / __REPLACE_SLUG__).
  * PowerRename checkboxes/toggles only expose the UIA Toggle pattern (no
    deterministic set-state), so flag state would leak between checks. To guarantee
    every check starts from PowerRename's default (all-off) flag state, this harness
    launches a FRESH PowerRename instance per check.
  * After each check's steps run, it captures a screenshot of the resulting state
    (winapp ui screenshot -w <HWND> --output <dir>/<check-id>.png) so the run can be
    diffed against the baseline PNGs under assets/screenshots/.

It reuses the app-signoff-uia skill's engine (signoff.execute_check /
build_report / report_to_markdown) so reports match the generic sign-off format.

Usage:
  python run-signoff.py --exe <PowerToys.PowerRename.exe> --spec <spec.json> \
      --files f1.txt f2.txt ... --report-json out.json --report-md out.md \
      --screenshot-dir shots\

  # Auto-discover the built exe and generate throwaway sample files:
  python run-signoff.py --powertoys-root C:\\s\\powertoys \
      --report-json out.json --report-md out.md --screenshot-dir shots\

Exit codes: 0 = gate PASS (all P0 passed), 1 = gate FAIL, 2 = error.
"""
from __future__ import annotations
import argparse
import copy
import importlib.util
import json
import re
import subprocess
import sys
import time
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")

HERE = Path(__file__).resolve().parent
SKILL_ROOT = HERE.parent
DEFAULT_SPEC = SKILL_ROOT / "assets" / "powerrename.spec.json"
# Default sample files that satisfy every check in the bundled spec.
DEFAULT_SAMPLE_FILES = ["testCase1.txt", "testCase2.txt", "SpecialCase.txt", "report_2020.log"]


def die(msg: str, code: int = 2) -> "NoReturn":  # type: ignore[valid-type]
    print(f"[run-signoff] ERROR: {msg}", file=sys.stderr)
    sys.exit(code)


def locate_signoff() -> Path:
    """Find the generic app-signoff-uia skill's signoff.py engine (relative walk)."""
    rel = Path(".github") / "skills" / "app-signoff-uia" / "scripts" / "signoff.py"
    roots = [SKILL_ROOT, *SKILL_ROOT.parents, Path.cwd(), *Path.cwd().parents]
    seen = set()
    for root in roots:
        if root in seen:
            continue
        seen.add(root)
        candidate = root / rel
        if candidate.is_file():
            return candidate
    die("could not locate app-signoff-uia's scripts/signoff.py; pass --signoff <path>")


def load_signoff(path: Path):
    spec = importlib.util.spec_from_file_location("signoff", path)
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def find_exe(powertoys_root: str | None, exe: str | None) -> Path:
    if exe:
        p = Path(exe)
        if not p.is_file():
            die(f"--exe not found: {p}")
        return p
    if powertoys_root:
        p = Path(powertoys_root) / "x64" / "Release" / "WinUI3Apps" / "PowerToys.PowerRename.exe"
        if not p.is_file():
            die(f"PowerRename exe not found under --powertoys-root: {p}\n"
                "Build PowerRenameUI (Release x64) first, or pass --exe explicitly.")
        return p
    die("provide --exe <PowerToys.PowerRename.exe> or --powertoys-root <repo>")


def make_sample_files(work_dir: Path) -> list[str]:
    work_dir.mkdir(parents=True, exist_ok=True)
    for name in DEFAULT_SAMPLE_FILES:
        f = work_dir / name
        if not f.exists():
            f.write_text(f"sample content for {name}\n", encoding="utf-8")
    return [str(work_dir / n) for n in DEFAULT_SAMPLE_FILES]


def winapp_json(args: list[str], winapp: str) -> dict | None:
    try:
        p = subprocess.run([winapp, "ui", *args, "--json"],
                           capture_output=True, text=True, timeout=60)
    except FileNotFoundError:
        die(f"'{winapp}' not on PATH; install winappcli (winapp ui status)")
    try:
        return json.loads((p.stdout or "").strip())
    except json.JSONDecodeError:
        return None


def find_hwnd(winapp: str, timeout: float = 15.0) -> str | None:
    deadline = time.time() + timeout
    while time.time() < deadline:
        p = subprocess.run([winapp, "ui", "list-windows"],
                           capture_output=True, text=True, timeout=30)
        blob = " ".join((p.stdout or "").split())
        m = re.search(r'HWND (\d+): "PowerRename"', blob)
        if m:
            return m.group(1)
        time.sleep(0.5)
    return None


def resolve_edit_slug(hwnd: str, name: str, winapp: str) -> str | None:
    j = winapp_json(["search", name, "-w", hwnd], winapp)
    if not j:
        return None
    for match in j.get("matches", []):
        if match.get("type") == "Edit":
            return match.get("selector")
    return None


def capture_screenshot(hwnd: str, out_path: Path, winapp: str,
                       capture_screen: bool) -> str | None:
    out_path.parent.mkdir(parents=True, exist_ok=True)
    args = ["screenshot", "-w", hwnd, "--output", str(out_path)]
    if capture_screen:
        args.append("--capture-screen")
    j = winapp_json(args, winapp)
    if out_path.is_file() and out_path.stat().st_size > 0:
        return str(out_path)
    if isinstance(j, dict) and j.get("filePath"):
        return j["filePath"]
    return None


def substitute(check: dict, search_slug: str, replace_slug: str) -> dict:
    c = copy.deepcopy(check)
    for step in c.get("steps", []):
        sel = step.get("selector")
        if sel == "__SEARCH_SLUG__":
            step["selector"] = search_slug
        elif sel == "__REPLACE_SLUG__":
            step["selector"] = replace_slug
    return c


def parse_args() -> argparse.Namespace:
    ap = argparse.ArgumentParser(
        description="Non-destructive P0/P1/P2 winappcli UI sign-off of PowerRename (with per-check screenshots).",
        formatter_class=argparse.RawDescriptionHelpFormatter,
        epilog=__doc__)
    src = ap.add_argument_group("PowerRename executable (one required)")
    src.add_argument("--exe", help="path to a built PowerToys.PowerRename.exe")
    src.add_argument("--powertoys-root",
                     help="PowerToys repo root; resolves x64/Release/WinUI3Apps/PowerToys.PowerRename.exe")
    ap.add_argument("--spec", default=str(DEFAULT_SPEC),
                    help=f"capability spec JSON (default: bundled {DEFAULT_SPEC.name})")
    ap.add_argument("--files", nargs="+",
                    help="sample files to open in PowerRename (default: auto-generated in --work-dir)")
    ap.add_argument("--work-dir", default=str(HERE.parent / "workfiles"),
                    help="folder for auto-generated sample files (default: <skill>/workfiles)")
    ap.add_argument("--report-json", required=True, help="write machine-readable report")
    ap.add_argument("--report-md", required=True, help="write Markdown report")
    ap.add_argument("--screenshot-dir", help="capture a per-check screenshot into this folder")
    ap.add_argument("--capture-screen", action="store_true",
                    help="pass --capture-screen to winapp screenshot (use if RDP screenshots are black)")
    ap.add_argument("--signoff", help="path to app-signoff-uia scripts/signoff.py (auto-detected)")
    ap.add_argument("--winapp", default="winapp", help="winappcli executable (default: winapp)")
    ap.add_argument("--step-pause", type=float, default=0.9, help="seconds between UI steps")
    ap.add_argument("--timeout", type=int, default=60, help="per-verb timeout (s)")
    ap.add_argument("--retries", type=int, default=3, help="transient-failure retries per step")
    ap.add_argument("--settle", type=float, default=3.0, help="seconds after launch before driving UI")
    return ap.parse_args()


def main() -> int:
    args = parse_args()

    exe = find_exe(args.powertoys_root, args.exe)
    spec_path = Path(args.spec)
    if not spec_path.is_file():
        die(f"spec not found: {spec_path}")
    spec = json.loads(spec_path.read_text(encoding="utf-8"))

    files = args.files or make_sample_files(Path(args.work_dir))
    for f in files:
        if not Path(f).is_file():
            die(f"sample file missing: {f}")

    shot_dir = Path(args.screenshot_dir) if args.screenshot_dir else None
    sf = load_signoff(Path(args.signoff) if args.signoff else locate_signoff())

    results = []
    for check in spec["checks"]:
        cid = check.get("id")
        priority = check.get("priority", "P2")
        desc = check.get("description", "")
        print(f"\n=== ISOLATED CHECK {cid} (fresh app) ===", file=sys.stderr)
        proc = subprocess.Popen([str(exe), *files])
        try:
            time.sleep(args.settle)
            hwnd = find_hwnd(args.winapp)
            if not hwnd:
                results.append({"id": cid, "priority": priority, "description": desc,
                                "status": "FAIL", "fail_reason": "could not locate PowerRename window",
                                "steps": []})
                continue
            s_slug = resolve_edit_slug(hwnd, "Search for", args.winapp)
            r_slug = resolve_edit_slug(hwnd, "Replace with", args.winapp)
            if not s_slug or not r_slug:
                results.append({"id": cid, "priority": priority, "description": desc,
                                "status": "FAIL",
                                "fail_reason": f"could not resolve edit slugs (search={s_slug}, replace={r_slug})",
                                "steps": []})
                continue
            concrete = substitute(check, s_slug, r_slug)
            res = sf.execute_check(concrete, {"window": hwnd}, args.winapp,
                                   args.step_pause, args.timeout, args.retries)
            if shot_dir is not None:
                shot = capture_screenshot(hwnd, shot_dir / f"{cid}.png",
                                          args.winapp, args.capture_screen)
                res["screenshot"] = shot
                print(f"  screenshot -> {shot}", file=sys.stderr)
            results.append(res)
        finally:
            subprocess.run(["taskkill", "/PID", str(proc.pid), "/T", "/F"],
                           capture_output=True, text=True)
            time.sleep(1.0)

    report = sf.build_report(spec, {"window": "per-check", "app": "PowerToys.PowerRename"}, results)
    # carry screenshot paths into the report's per-check records
    shot_by_id = {r.get("id"): r.get("screenshot") for r in results}
    for rec in report.get("results", report.get("checks", [])):
        if isinstance(rec, dict) and rec.get("id") in shot_by_id:
            rec["screenshot"] = shot_by_id[rec["id"]]
    Path(args.report_json).write_text(json.dumps(report, indent=2, ensure_ascii=False), encoding="utf-8")
    Path(args.report_md).write_text(sf.report_to_markdown(report), encoding="utf-8")

    print(f"\nGATE: {report['gate']}  "
          f"({report['summary']['passed']}/{report['summary']['total']} passed)", file=sys.stderr)
    for r in results:
        line = f"  [{r['priority']}] {r['id']}: {r['status']}"
        if r["status"] == "FAIL":
            line += f" -- {r.get('fail_reason', '')}"
        print(line, file=sys.stderr)
    return 0 if report["gate"] == "PASS" else 1


if __name__ == "__main__":
    sys.exit(main())
