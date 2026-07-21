#!/usr/bin/env python3
"""run_b1.py - Issue-fix time-travel benchmark (B1) mechanics.

Given a historically-FIXED issue and the commit/PR that fixed it, roll a git worktree
back to BEFORE the fix and assemble the packets an LLM candidate needs. The candidate
(armed with the distilled module knowledge, pretending the bug is unfixed) must locate
the culprit and propose a fix; we score against the REAL fix diff.

INSIGHT PRINCIPLE: mechanics only (worktree, ground-truth diff extraction, scoring math
from a judge's JSON). Locating/fixing and judging are done by LLM agents.

Modes:
  prepare   Create a worktree at the fix commit's PARENT, write ground_truth.json (real
            changed files + diff) and candidate_task.md (issue with fix hidden).
  score     Given judge.json, compute the B1 score (located_area, fix_matches, found_fix_ref).
  cleanup   Remove a prepared worktree.

Usage:
  python run_b1.py prepare --repo microsoft/PowerToys --clone C:\\s\\PowerToys \
      --issue 44980 --fix-commit <sha> --module PowerAccent --out benchmark/results/b1
  python run_b1.py score --dir benchmark/results/b1/44980
  python run_b1.py cleanup --dir benchmark/results/b1/44980
"""
from __future__ import annotations

import argparse
import json
import subprocess
import sys
from pathlib import Path

SKILL_SCRIPTS = Path(__file__).resolve().parents[1] / ".github" / "skills" / \
    "repo-history-distill" / "scripts"
sys.path.insert(0, str(SKILL_SCRIPTS))
import distill  # noqa: E402


def git(clone: str, *args: str) -> str:
    p = subprocess.run(["git", "-C", clone, *args], capture_output=True,
                       text=True, encoding="utf-8", errors="replace")
    if p.returncode != 0:
        raise RuntimeError(f"git {' '.join(args)} failed: {p.stderr.strip()}")
    return p.stdout


def cmd_prepare(args) -> int:
    owner, repo = args.repo.split("/", 1)
    out = Path(args.out) / str(args.issue)
    out.mkdir(parents=True, exist_ok=True)

    fix = args.fix_commit
    # Verify the commit exists locally; else advise fetch.
    git(args.clone, "cat-file", "-e", f"{fix}^{{commit}}")
    parent = git(args.clone, "rev-parse", f"{fix}~1").strip()

    # Ground truth = the real fix diff.
    changed = git(args.clone, "diff", "--name-only", f"{fix}~1", fix).split()
    diff = git(args.clone, "diff", f"{fix}~1", fix)
    commit_msg = git(args.clone, "log", "-1", "--format=%s%n%n%b", fix)

    # Roll back: worktree at the parent (bug is "live").
    wt = (out / "worktree").resolve()
    if wt.exists():
        subprocess.run(["git", "-C", args.clone, "worktree", "remove", "--force",
                        str(wt)], capture_output=True, text=True)
    git(args.clone, "worktree", "add", "--detach", str(wt), parent)

    # Issue text (fix hidden): fetch from GitHub, strip fix references.
    issue = distill.gh_api(f"/repos/{owner}/{repo}/issues/{args.issue}")
    body = (issue.get("body") or "")[:4000] if isinstance(issue, dict) else ""
    title = issue.get("title") if isinstance(issue, dict) else f"Issue {args.issue}"

    (out / "ground_truth.json").write_text(json.dumps({
        "issue": args.issue, "module": args.module, "fix_commit": fix,
        "parent_commit": parent, "changed_files": changed,
        "commit_subject": commit_msg.splitlines()[0] if commit_msg else "",
    }, indent=2), encoding="utf-8")
    (out / "ground_truth.diff").write_text(diff, encoding="utf-8")

    task = [f"# Bug to fix: {title}", "",
            f"(Module: {args.module}. The repository is checked out at commit `{parent}` — "
            "the bug is present and UNFIXED. Do NOT look for the fix in git history; it does "
            "not exist yet at this checkout.)", "",
            "## Symptom / report", "", body or "(no description)", "",
            "## Your task", "",
            "1. Identify the culprit file(s) and function(s) that must change.",
            "2. Describe the fix (what to change and why).",
            "3. If you can, cite the historical PR/commit that fixed this.", "",
            f"Working tree: `{wt}`", ""]
    (out / "candidate_task.md").write_text("\n".join(task), encoding="utf-8")

    print(f"prepared B1 issue #{args.issue}: worktree at parent {parent[:10]} -> {wt}")
    print(f"  ground-truth changed files ({len(changed)}): {changed[:6]}")
    print("Next: give candidate_task.md (+ distilled .md, + the worktree to explore) to a "
          "CANDIDATE agent, and the same WITHOUT the .md to a BASELINE agent. Give both "
          "answers + ground_truth.diff to a JUDGE agent; save judge.json here; run `score`.")
    return 0


def cmd_score(args) -> int:
    d = Path(args.dir)
    judge = json.loads((d / "judge.json").read_text(encoding="utf-8"))
    # judge.json shape:
    # {"candidate":{"located_area":0-1,"fix_matches":0-1,"found_fix_ref":0-1},
    #  "baseline":{...}, "notes":"..."}
    def total(x):
        return round((x.get("located_area", 0) + x.get("fix_matches", 0)
                      + x.get("found_fix_ref", 0)) / 3, 3)
    cand = total(judge.get("candidate", {}))
    base = total(judge.get("baseline", {}))
    report = {"issue": d.name, "candidate_score": cand, "baseline_score": base,
              "lift": round(cand - base, 3), "detail": judge}
    (d / "score.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0


def cmd_cleanup(args) -> int:
    d = Path(args.dir)
    gt = json.loads((d / "ground_truth.json").read_text(encoding="utf-8"))
    wt = (d / "worktree").resolve()
    # We need the clone path; accept via --clone or infer not possible → require flag.
    if args.clone:
        subprocess.run(["git", "-C", args.clone, "worktree", "remove", "--force",
                        str(wt)], capture_output=True, text=True)
        print(f"removed worktree {wt}")
    else:
        print("pass --clone to remove the worktree via git")
    return 0


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    pr = sub.add_parser("prepare")
    pr.add_argument("--repo", required=True)
    pr.add_argument("--clone", required=True, help="local git clone path")
    pr.add_argument("--issue", type=int, required=True)
    pr.add_argument("--fix-commit", required=True, help="SHA that fixed the issue")
    pr.add_argument("--module", required=True)
    pr.add_argument("--out", default="benchmark/results/b1")
    pr.set_defaults(func=cmd_prepare)

    s = sub.add_parser("score")
    s.add_argument("--dir", required=True)
    s.set_defaults(func=cmd_score)

    c = sub.add_parser("cleanup")
    c.add_argument("--dir", required=True)
    c.add_argument("--clone")
    c.set_defaults(func=cmd_cleanup)

    args = p.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
