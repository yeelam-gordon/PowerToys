#!/usr/bin/env python3
"""run_b2.py - PR-review recall benchmark (B2) mechanics.

Measures whether a reviewer armed with the distilled module knowledge surfaces the
REAL historical review comments of a held-out PR, in few rounds.

INSIGHT PRINCIPLE: this script only does MECHANICS (fetch ground truth, build the
reviewer packet, compute recall from a judge's match list). All reviewing and all
match-judging is done by LLM agents — never by this script.

Modes:
  candidates  List PRs from a module's raw fetch with review-comment counts, flagging
              which already appear in the distilled .md (leakage) so you pick clean test PRs.
  prepare     For a test PR: write ground_truth.json (substantive review comments) and
              reviewer_packet.md (the PR's diff) into benchmark/results/b2/<pr>/.
  score       Given a judge's matches.json, compute recall@round and precision.

Usage:
  python run_b2.py candidates --repo microsoft/PowerToys --module PowerAccent --raw ./distilled --md ./distilled/microsoft-PowerToys/PowerAccent.md
  python run_b2.py prepare --repo microsoft/PowerToys --pr 46593 --out benchmark/results/b2
  python run_b2.py score --dir benchmark/results/b2/46593
"""
from __future__ import annotations

import argparse
import json
import re
import sys
from pathlib import Path

# Reuse the skill's HTTP layer (gather-only) rather than duplicating it.
SKILL_SCRIPTS = Path(__file__).resolve().parents[1] / ".github" / "skills" / \
    "repo-history-distill" / "scripts"
sys.path.insert(0, str(SKILL_SCRIPTS))
import distill  # noqa: E402

BOT_MARKERS = ("/azp run", "lgtm", "it builds", "thanks", "thank you", "done",
               "good catch", "nit:", "ping", "friendly ping")
BOT_USERS = distill.DEFAULT_IGNORE_USERS


def _substantive(body: str) -> bool:
    b = (body or "").strip().lower()
    if len(b) < 25:
        return False
    if b.startswith("## unrecognized spelling") or "is not a recognized word" in b:
        return False  # github-advanced-security spelling noise
    if any(b == m or b.startswith(m) for m in BOT_MARKERS):
        return False
    return True


def cmd_candidates(args) -> int:
    owner_repo = args.repo
    safe = distill._safe(args.module)
    raw = Path(args.raw) / owner_repo.replace("/", "-") / "raw" / safe
    prs = json.loads((raw / "prs.json").read_text(encoding="utf-8"))
    rcs = json.loads((raw / "review_comments.json").read_text(encoding="utf-8"))
    md_text = Path(args.md).read_text(encoding="utf-8") if args.md and Path(args.md).exists() else ""
    referenced = set(int(n) for n in re.findall(r"/pull/(\d+)", md_text))

    by_pr = {}
    for c in rcs:
        if _substantive(c.get("body", "")) and c.get("author") not in BOT_USERS:
            by_pr.setdefault(c["pr"], 0)
            by_pr[c["pr"]] += 1

    rows = []
    for pr in prs:
        n = pr["number"]
        rows.append((n, by_pr.get(n, 0), n in referenced, pr["title"][:60]))
    rows.sort(key=lambda r: (r[2], -r[1]))  # clean (not referenced) first, most comments
    print(f"{'PR':>7} {'#substantive':>12} {'leaked?':>8}  title")
    for n, cnt, leaked, title in rows:
        print(f"{n:>7} {cnt:>12} {'YES' if leaked else 'no':>8}  {title}")
    clean = [r for r in rows if not r[2] and r[1] >= args.min_comments]
    print(f"\nRecommended clean test PRs (>= {args.min_comments} substantive, not in .md): "
          f"{[r[0] for r in clean[:5]]}")
    return 0


def cmd_prepare(args) -> int:
    owner, repo = args.repo.split("/", 1)
    n = args.pr
    out = Path(args.out) / str(n)
    out.mkdir(parents=True, exist_ok=True)

    pr = distill.gh_api(f"/repos/{owner}/{repo}/pulls/{n}")
    files = distill.gh_api(f"/repos/{owner}/{repo}/pulls/{n}/files?per_page=100", paginate=True)
    comments = distill.gh_api(f"/repos/{owner}/{repo}/pulls/{n}/comments?per_page=100", paginate=True)

    # Ground truth = REVIEWER feedback only: substantive comments NOT authored by the
    # PR author (the author's replies aren't concerns a reviewer should "predict").
    pr_author = (pr.get("user") or {}).get("login") if isinstance(pr, dict) else None
    gt = []
    for c in (comments or []):
        u = (c.get("user") or {}).get("login")
        if u in BOT_USERS or u == pr_author or not _substantive(c.get("body", "")):
            continue
        gt.append({
            "id": c.get("id"), "author": u,
            "path": c.get("path"), "line": c.get("line") or c.get("original_line"),
            "body": (c.get("body") or "").strip()[:1500],
            "url": c.get("html_url"),
        })
    (out / "ground_truth.json").write_text(json.dumps(gt, indent=2), encoding="utf-8")

    # Reviewer packet: the diff the reviewer must critique (patch per file).
    lines = [f"# PR #{n} — {pr.get('title','')}", "",
             f"Base: {pr.get('base',{}).get('ref')}  Head: {pr.get('head',{}).get('ref')}",
             "", "## Description", "", (pr.get("body") or "(none)")[:3000], "",
             "## Changed files (unified diff)", ""]
    for f in (files or []):
        lines.append(f"### {f.get('filename')}  (+{f.get('additions')}/-{f.get('deletions')})")
        patch = f.get("patch")
        lines.append("```diff")
        lines.append(patch if patch else "(binary or too large — omitted)")
        lines.append("```")
    (out / "reviewer_packet.md").write_text("\n".join(lines), encoding="utf-8")

    meta = {"repo": args.repo, "pr": n, "ground_truth_count": len(gt),
            "title": pr.get("title")}
    (out / "meta.json").write_text(json.dumps(meta, indent=2), encoding="utf-8")
    print(f"prepared PR #{n}: {len(gt)} ground-truth comments, "
          f"{len(files or [])} files -> {out}")
    print("Next: give reviewer_packet.md (+ distilled .md) to a reviewer agent; give its "
          "output + ground_truth.json to a judge agent; save judge matches.json here; run `score`.")
    return 0


def cmd_score(args) -> int:
    d = Path(args.dir)
    gt = json.loads((d / "ground_truth.json").read_text(encoding="utf-8"))
    matches = json.loads((d / "matches.json").read_text(encoding="utf-8"))
    # matches.json shape (produced by the judge agent):
    # {"rounds":[{"round":1,"matched_gt_ids":[...],"candidate_total":N}], ...}
    total_gt = len(gt)
    seen = set()
    report = {"pr": d.name, "total_ground_truth": total_gt, "rounds": []}
    for rnd in matches.get("rounds", []):
        seen |= set(rnd.get("matched_gt_ids", []))
        recall = len(seen) / total_gt if total_gt else 0.0
        cand = rnd.get("candidate_total", 0)
        prec = (len(set(rnd.get("matched_gt_ids", []))) / cand) if cand else 0.0
        report["rounds"].append({
            "round": rnd.get("round"), "cumulative_recall": round(recall, 3),
            "round_precision": round(prec, 3),
            "matched_so_far": len(seen), "candidates_this_round": cand,
        })
    rounds_to_90 = next((r["round"] for r in report["rounds"]
                         if r["cumulative_recall"] >= 0.9), None)
    report["rounds_to_90pct_recall"] = rounds_to_90
    report["final_recall"] = report["rounds"][-1]["cumulative_recall"] if report["rounds"] else 0.0
    (d / "score.json").write_text(json.dumps(report, indent=2), encoding="utf-8")
    print(json.dumps(report, indent=2))
    return 0


def main() -> int:
    p = argparse.ArgumentParser(description=__doc__,
                                formatter_class=argparse.RawDescriptionHelpFormatter)
    sub = p.add_subparsers(dest="cmd", required=True)

    c = sub.add_parser("candidates")
    c.add_argument("--repo", required=True)
    c.add_argument("--module", required=True)
    c.add_argument("--raw", default="./distilled")
    c.add_argument("--md", default="")
    c.add_argument("--min-comments", type=int, default=6)
    c.set_defaults(func=cmd_candidates)

    pr = sub.add_parser("prepare")
    pr.add_argument("--repo", required=True)
    pr.add_argument("--pr", type=int, required=True)
    pr.add_argument("--out", default="benchmark/results/b2")
    pr.set_defaults(func=cmd_prepare)

    s = sub.add_parser("score")
    s.add_argument("--dir", required=True)
    s.set_defaults(func=cmd_score)

    args = p.parse_args()
    return args.func(args)


if __name__ == "__main__":
    sys.exit(main())
