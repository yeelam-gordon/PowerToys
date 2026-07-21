#!/usr/bin/env python3
"""prepare_b1_sparse.py - lightweight B1 worktree prep for scaling to many modules.

Same idea as run_b1.py prepare, but creates a SPARSE, --no-checkout worktree scoped to
only the directories the fix touched (plus the module root), so each rolled-back tree is
tiny and dozens can coexist. Ground truth = the real fix diff.

Usage:
  python prepare_b1_sparse.py --clone C:\\s\\PowerToys --fix-sha <sha> --module <m> \
      --out benchmark/results/b1 --paths src/modules/x src/common/y
"""
from __future__ import annotations
import argparse, json, subprocess, sys
from pathlib import Path


def git(clone, *a, check=True):
    p = subprocess.run(["git", "-C", clone, *a], capture_output=True, text=True,
                       encoding="utf-8", errors="replace")
    if check and p.returncode != 0:
        raise RuntimeError(f"git {' '.join(a)}: {p.stderr.strip()}")
    return p.stdout


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("--clone", required=True)
    ap.add_argument("--fix-sha", required=True)
    ap.add_argument("--module", required=True)
    ap.add_argument("--case-id", required=True)
    ap.add_argument("--out", default="benchmark/results/b1")
    ap.add_argument("--symptom", default="")
    ap.add_argument("--paths", nargs="*", default=[])
    args = ap.parse_args()

    out = Path(args.out) / args.case_id
    out.mkdir(parents=True, exist_ok=True)
    fix = args.fix_sha
    git(args.clone, "cat-file", "-e", f"{fix}^{{commit}}")
    parent = git(args.clone, "rev-parse", f"{fix}~1").strip()
    changed = git(args.clone, "diff", "--name-only", f"{fix}~1", fix).split()
    diff = git(args.clone, "diff", f"{fix}~1", fix)

    # sparse dirs: top-2-level dirs of changed files + any extra paths + module root
    dirs = set(args.paths)
    for f in changed:
        parts = f.split("/")
        dirs.add("/".join(parts[:3]) if len(parts) >= 3 else parts[0])
    dirs = sorted(d for d in dirs if d)

    wt = (out / "worktree").resolve()
    if wt.exists():
        subprocess.run(["git", "-C", args.clone, "worktree", "remove", "--force", str(wt)],
                       capture_output=True, text=True)
    git(args.clone, "worktree", "add", "--no-checkout", "--detach", str(wt), parent)
    git(str(wt), "sparse-checkout", "init", "--cone")
    git(str(wt), "sparse-checkout", "set", *dirs)
    git(str(wt), "checkout")

    (out / "ground_truth.json").write_text(json.dumps({
        "case_id": args.case_id, "module": args.module, "fix_commit": fix,
        "parent_commit": parent, "changed_files": changed, "sparse_dirs": dirs,
    }, indent=2), encoding="utf-8")
    (out / "ground_truth.diff").write_text(diff, encoding="utf-8")
    task = [f"# Bug in {args.module}", "", f"(Worktree at {parent[:10]}; bug is LIVE and unfixed.)",
            "", "## Symptom", "", args.symptom or "(see benchmark)", "",
            "## Task", "1. Locate culprit file(s)+function(s). 2. Describe the fix. 3. Cite the fix PR if known.",
            f"", f"Explore ONLY: {wt}", f"Sparse dirs: {dirs}"]
    (out / "candidate_task.md").write_text("\n".join(task), encoding="utf-8")
    print(f"{args.case_id} {args.module}: parent {parent[:10]}, sparse={dirs}, changed={len(changed)}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
