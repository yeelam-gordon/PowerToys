#!/usr/bin/env python3
"""Mechanical validator for generated target skills against agent-skills rules."""
import glob, re, os, sys

def check(path):
    t = open(path, encoding="utf-8").read()
    lines = t.count("\n") + 1
    issues = []
    m = re.match(r"---\r?\n(.*?)\r?\n---", t, re.S)
    fm = m.group(1) if m else ""
    if not m:
        issues.append("no frontmatter")
    name = re.search(r"name:\s*(.+)", fm)
    nm = name.group(1).strip() if name else ""
    if not re.fullmatch(r"[a-z0-9-]{1,64}", nm or ""):
        issues.append(f"bad name '{nm}'")
    dm = re.search(r"description:\s*'((?:[^']|'')*)'", fm)
    dl = len(dm.group(1)) if dm else 0
    if not (10 <= dl <= 1024):
        issues.append(f"desc len {dl} out of 10-1024")
    if lines > 500:
        issues.append(f"{lines} lines > 500")
    d = os.path.dirname(path)
    secs = re.findall(r"^##\s+(.+)", t, re.M)
    is_signoff = nm.endswith("-signoff")
    if is_signoff:
        # Declarative winappcli sign-off design: checklist is the source of truth,
        # plus app-specific winappcli logic, a sign-off procedure, coverage/limits.
        want_sections = ["When to Use", "Coverage", "Reference"]
        # sign-off procedure: either "How to Sign Off" or "Running the Sign-off"
        if not any(("sign off" in s.lower() or "running" in s.lower()) for s in secs):
            issues.append("missing a sign-off procedure section")
        # winappcli driving logic: "App-Specific winappcli Logic" or "Launch"/"Executors"
        if not any(("winappcli" in s.lower() or "launch" in s.lower()
                    or "executor" in s.lower()) for s in secs):
            issues.append("missing winappcli launch/drive logic section")
        want_files = ["LICENSE.txt"]
        # declarative checklist is the source of truth (or a machine-runnable spec)
        if not (os.path.exists(os.path.join(d, "signoff-checklist.md"))
                or glob.glob(os.path.join(d, "assets", "*.json"))):
            issues.append("missing signoff-checklist.md or assets/*.json spec")
    else:
        want_sections = ["When to Use", "Module Map", "Regression Playbook",
                         "Review Rule", "Gotcha", "Reference"]
        want_files = ["LICENSE.txt", "templates/pr-review-checklist.md",
                      "templates/bug-triage.md", "references/regression-catalog.md"]
    for want in want_sections:
        if not any(want.lower() in s.lower() for s in secs):
            issues.append(f"missing section ~'{want}'")
    for req in want_files:
        if not os.path.exists(os.path.join(d, req)):
            issues.append(f"missing file {req}")
    # relative template/reference links resolve
    for rel in re.findall(r"\]\(\.\/([^)]+)\)", t):
        if not os.path.exists(os.path.join(d, rel)):
            issues.append(f"broken relative link ./{rel}")
    return lines, nm, dl, len(secs), issues

rc = 0
for f in sorted(glob.glob("distilled-skills/*/SKILL.md")):
    lines, nm, dl, ns, issues = check(f)
    status = "PASS" if not issues else "FAIL"
    if issues:
        rc = 1
    print(f"[{status}] {f}: {lines} lines, name={nm}, desc_len={dl}, sections={ns}")
    for i in issues:
        print(f"        - {i}")
print("\nOVERALL:", "PASS" if rc == 0 else "FAIL")
sys.exit(rc)
