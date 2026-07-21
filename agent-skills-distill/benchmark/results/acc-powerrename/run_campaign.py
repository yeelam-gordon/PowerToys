#!/usr/bin/env python3
"""
run_campaign.py -- Acceptance campaign for the PowerRename winappcli sign-off.

For each of 10 distinct, UI-observable source injections:
  1. apply the injection (unique string replace in a PowerRename source file)
  2. rebuild PowerRenameUI (Release x64) via VsDevCmd + msbuild
  3. run the sign-off (run-signoff.py) -> per-injection report + screenshots
  4. record which checklist item(s) flipped PASS->FAIL and confirm the target caught it
  5. revert the injection (git checkout) and confirm the tree is clean

Real winappcli execution only. Apply is never clicked (non-destructive).
"""
from __future__ import annotations
import json, subprocess, sys, time
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8")
sys.stderr.reconfigure(encoding="utf-8")

PT = Path(r"C:\s\PowerToys")
LIB = PT / "src" / "modules" / "powerrename" / "lib"
REGEX = LIB / "PowerRenameRegEx.cpp"
HELPERS = LIB / "Helpers.cpp"
VSDEV = r"C:\Program Files\Microsoft Visual Studio\18\Enterprise\Common7\Tools\VsDevCmd.bat"
PROJ = str(PT / "src" / "modules" / "powerrename" / "PowerRenameUILib" / "PowerRenameUI.vcxproj")
EXE = PT / "x64" / "Release" / "WinUI3Apps" / "PowerToys.PowerRename.exe"

SKILL = Path(r"C:\s\Demo\SkillForDistill\distilled-skills\powertoys-powerrename-signoff")
SPEC = SKILL / "assets" / "powerrename.spec.json"
RUNNER = SKILL / "scripts" / "run-signoff.py"
ACC = Path(r"C:\s\Demo\SkillForDistill\benchmark\results\acc-powerrename")
WF = ACC / "workfiles"
PY = sys.executable

FILES = [str(WF / n) for n in ("testCase1.txt", "testCase2.txt", "SpecialCase.txt", "report_2020.log")]

# id, file, old_str, new_str, target check id, human description of the bug
INJECTIONS = [
    ("INJ1", REGEX,
     "res = sourceToUse.replace(pos, searchTerm.length(), replaceTerm);",
     "res = sourceToUse;",
     "p0-literal-replace-multi",
     "Literal simple-replace turned into a no-op (result = unchanged source)."),
    ("INJ2", REGEX,
     "const bool isCaseInsensitive = !(m_flags & CaseSensitive);",
     "const bool isCaseInsensitive = true;",
     "p1-case-sensitive-toggle",
     "Case-sensitive flag ignored: matching is forced case-insensitive."),
    ("INJ3", REGEX,
     "if (!(m_flags & MatchAllOccurrences))",
     "if (false)",
     "p1-match-all-occurrences",
     "Match-all guard removed: every occurrence is replaced even when the flag is OFF."),
    ("INJ4", REGEX,
     'replaceTerm = regex_replace(replaceTerm, otherGroupsRegex, L"$1$0$4");',
     'replaceTerm = regex_replace(replaceTerm, otherGroupsRegex, L"$1$0");',
     "p2-capture-groups",
     "Capture-group index dropped ($4 removed): $1..$9 back-references stop working."),
    ("INJ5", REGEX,
     "res = RegexReplaceDispatch[_useBoostLib](sourceToUse, m_searchTerm, replaceTerm, m_flags & MatchAllOccurrences, isCaseInsensitive);",
     "res = sourceToUse;",
     "p0-regex-replace",
     "Regex replace turned into a no-op (result = unchanged source)."),
    ("INJ6", REGEX,
     "if (shouldIncrementCounter)\n            enumIndex++;",
     "if (shouldIncrementCounter)\n            enumIndex += 0;",
     "p1-enumerate-counter-padding",
     "Enumeration counter never increments: every item gets the same counter value."),
    ("INJ7", HELPERS,
     "if (flags & Uppercase)",
     "if (false && (flags & Uppercase))",
     "p2-uppercase-transform",
     "Uppercase transform disabled: the whole-name uppercasing path is gated off."),
    ("INJ8", HELPERS,
     "else if (flags & Lowercase)",
     "else if (false && (flags & Lowercase))",
     "p2-lowercase-transform",
     "Lowercase transform disabled."),
    ("INJ9", HELPERS,
     "else if (flags & Titlecase)",
     "else if (false && (flags & Titlecase))",
     "p2-titlecase-transform",
     "Title-case transform disabled."),
    ("INJ10", HELPERS,
     "else if (flags & Capitalized)",
     "else if (false && (flags & Capitalized))",
     "p2-capitalize-transform",
     "Capitalize-each-word transform disabled."),
]


def run(cmd, **kw):
    return subprocess.run(cmd, capture_output=True, text=True, **kw)


def git_clean_check() -> str:
    p = run(["git", "-C", str(PT), "status", "--short"])
    return p.stdout.strip()


def revert(path: Path):
    rel = str(path.relative_to(PT))
    run(["git", "-C", str(PT), "checkout", "--", rel])


def apply_injection(path: Path, old: str, new: str) -> bool:
    text = path.read_text(encoding="utf-8")
    if text.count(old) != 1:
        print(f"  !! old_str not unique (count={text.count(old)}) in {path.name}", file=sys.stderr)
        return False
    path.write_text(text.replace(old, new), encoding="utf-8")
    return True


def build() -> tuple[bool, str]:
    bat = ACC / "_build.bat"
    bat.write_text(
        '@echo off\r\n'
        f'call "{VSDEV}" -arch=amd64 >nul\r\n'
        f'msbuild "{PROJ}" /p:Configuration=Release /p:Platform=x64 -m -clp:ErrorsOnly;Summary\r\n'
        'exit /b %ERRORLEVEL%\r\n',
        encoding="ascii")
    p = run(["cmd", "/c", str(bat)], timeout=900)
    ok = p.returncode == 0
    return ok, (p.stdout or "")[-1200:] + (p.stderr or "")[-400:]


def signoff(tag: str) -> dict:
    shots = ACC / "screenshots" / tag
    rj = ACC / f"report_{tag}.json"
    rm = ACC / f"report_{tag}.md"
    run([PY, str(RUNNER), "--exe", str(EXE), "--spec", str(SPEC), "--files", *FILES,
         "--report-json", str(rj), "--report-md", str(rm),
         "--screenshot-dir", str(shots)], timeout=1200)
    return json.loads(rj.read_text(encoding="utf-8"))


def failed_ids(report: dict) -> list[str]:
    recs = report.get("results") or report.get("checks") or []
    return [r["id"] for r in recs if r.get("status") == "FAIL"]


def main() -> int:
    campaign = {"injections": [], "green_baseline": None}

    # sanity: tree must start clean (of powerrename changes)
    pre = git_clean_check()
    print(f"pre-campaign git status:\n{pre}", file=sys.stderr)

    caught = 0
    for iid, path, old, new, target, desc in INJECTIONS:
        print(f"\n########## {iid} -> {target} ##########", file=sys.stderr)
        rec = {"id": iid, "file": path.name, "old": old, "new": new,
               "target_check": target, "bug": desc}
        if not apply_injection(path, old, new):
            rec["error"] = "injection old_str not unique / not found"
            revert(path)
            campaign["injections"].append(rec)
            continue
        t0 = time.time()
        ok, log = build()
        rec["build_ok"] = ok
        rec["build_secs"] = round(time.time() - t0, 1)
        rec["exe_mtime"] = time.ctime(EXE.stat().st_mtime)
        if not ok:
            rec["error"] = "build failed"
            rec["build_log"] = log
            revert(path)
            campaign["injections"].append(rec)
            print(f"  BUILD FAILED for {iid}", file=sys.stderr)
            continue
        report = signoff(iid)
        fids = failed_ids(report)
        rec["gate"] = report.get("gate")
        rec["failed_checks"] = fids
        rec["target_caught"] = target in fids
        rec["screenshots_dir"] = str(ACC / "screenshots" / iid)
        # pull the read-evidence (search JSON) for the target check's failing step
        recs = report.get("results") or report.get("checks") or []
        for r in recs:
            if r["id"] == target:
                rec["target_status"] = r.get("status")
                rec["target_fail_reason"] = r.get("fail_reason")
                rec["target_screenshot"] = r.get("screenshot")
                break
        revert(path)
        rec["reverted_clean"] = (git_clean_check() == pre)
        if rec["target_caught"]:
            caught += 1
            print(f"  CAUGHT by {target}. failed={fids}", file=sys.stderr)
        else:
            print(f"  !! NOT caught by {target}. failed={fids}", file=sys.stderr)
        campaign["injections"].append(rec)
        Path(ACC / "campaign_progress.json").write_text(
            json.dumps(campaign, indent=2, ensure_ascii=False), encoding="utf-8")

    campaign["detection_rate"] = f"{caught}/{len(INJECTIONS)}"
    campaign["final_git_status"] = git_clean_check()
    Path(ACC / "results.json").write_text(
        json.dumps(campaign, indent=2, ensure_ascii=False), encoding="utf-8")
    print(f"\n==== DETECTION: {caught}/{len(INJECTIONS)} ====", file=sys.stderr)
    print(f"final git status:\n{campaign['final_git_status']}", file=sys.stderr)
    return 0 if caught == len(INJECTIONS) else 1


if __name__ == "__main__":
    sys.exit(main())
