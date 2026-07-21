#!/usr/bin/env python3
"""score_all.py - programmatic B1 scoring for the 26-module scaled run.

Reads answer_baseline.md / answer_candidate.md + ground_truth.json per case, and scores
the two OBJECTIVE axes deterministically:
  - located_area: 1.0 if any ground-truth CHANGED FILE (basename) is named in CULPRIT_FILES;
                  0.5 if the right module dir is named but not the exact file; else 0.
  - found_fix_ref: 1.0 if CITED_FIX_PR contains the real fix PR number; else 0.
The subjective axis (fix_matches) is left to batched judges; this script emits judge_inputs.json.
"""
import json, os, re, glob, sys
sys.stdout.reconfigure(encoding="utf-8")
B1 = "benchmark/results/b1"
cases = json.load(open(f"{B1}/cases_all.json", encoding="utf-8"))

def read(p):
    return open(p, encoding="utf-8", errors="replace").read() if os.path.exists(p) else ""

def parse(ans):
    """Format-agnostic: keep the whole text for matching, plus best-effort FIX extraction."""
    d = {"_full": ans}
    m = re.search(r"FIX[:\s#*]*\s*(.+?)(?:\n\s*(?:CITED|CONFID|USED|##)|\Z)", ans, re.S | re.I)
    d["FIX"] = (m.group(1).strip()[:600] if m else "")
    return d

def located(full, gt_files, module):
    t = full.lower()
    for f in gt_files:
        bn = os.path.basename(f).lower()
        # ignore trivial shared/aux ground-truth files so they don't inflate 0.5->1.0
        if bn in ("code.txt", "allow.txt", "expect.txt") or bn.endswith(".txt"):
            continue
        if bn in t:
            return 1.0
    if any(seg in t for seg in [module.lower(), "src/modules"]):
        return 0.5
    return 0.0

def has_ref(full, pr):
    return 1.0 if re.search(rf"(#|pull/|PR\s*#?|issues/){pr}\b", full, re.I) else 0.0

rows = []
judge_inputs = []
for m, c in cases.items():
    cid = f"m-{m}"
    gt = c["files"]; pr = str(c["fix_pr"])
    base = parse(read(f"{B1}/{cid}/answer_baseline.md"))
    cand = parse(read(f"{B1}/{cid}/answer_candidate.md"))
    if not base["_full"] and not cand["_full"]:
        rows.append((m, cid, pr, None)); continue
    b_loc = located(base["_full"], gt, m)
    c_loc = located(cand["_full"], gt, m)
    b_ref = has_ref(base["_full"], pr)
    c_ref = has_ref(cand["_full"], pr)
    rows.append((m, cid, pr, {"b_loc": b_loc, "c_loc": c_loc, "b_ref": b_ref, "c_ref": c_ref}))
    judge_inputs.append({"module": m, "case_id": cid, "fix_pr": pr,
                         "gt_files": gt, "baseline_fix": base["FIX"], "candidate_fix": cand["FIX"]})

json.dump(judge_inputs, open(f"{B1}/judge_inputs.json", "w", encoding="utf-8"), indent=2)
print(f"{'module':22} {'PR':7} b_loc c_loc b_ref c_ref  (fix_matches pending judge)")
done = 0
for m, cid, pr, s in rows:
    if s is None:
        print(f"{m:22} {pr:7} -- MISSING ANSWERS --"); continue
    done += 1
    print(f"{m:22} {pr:7} {s['b_loc']:.1f}   {s['c_loc']:.1f}   {s['b_ref']:.0f}     {s['c_ref']:.0f}")
print(f"\nscored {done}/{len(rows)} cases; judge_inputs.json written for fix_matches")
json.dump({r[0]: r[3] for r in rows if r[3]}, open(f"{B1}/objective_scores.json", "w"), indent=2)
