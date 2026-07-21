# Bug to fix: Hosts File Editor: Massive IP column (and unable to resize)

(Module: hosts. The repository is checked out at commit `4c9e18116c8bb3e96b9a08383983d1efdc9f6dc2` — the bug is present and UNFIXED. Do NOT look for the fix in git history; it does not exist yet at this checkout.)

## Symptom / report

### Microsoft PowerToys version

0.80.1

### Installation method

PowerToys auto-update

### Running as admin

Yes

### Area(s) with issue?

Hosts File Editor

### Steps to reproduce

In "_Hosts File Editor_", the _IP_ column is nearly 3x the width of _IP_ entries, but most of all, without an ability to resize/shrink the column. Resizing the window, from either side, only resizes the center "_Hosts_" column -- the column that will naturally have wider entries on average over the _IP_ column (in general, of course).

![2024-05-02_21-11-28](https://github.com/microsoft/PowerToys/assets/1448076/6187ec99-22e9-49ec-983d-368b4b07af0b)


### ✔️ Expected Behavior

Possibilities:
- Resizable column headers
- Column auto-sizing
- Ability to shrink _IP_ column's width, e.g. resizing the window via right window edge resizes the "_Hosts_" column; resizing via left window edge resizes the "_IP_" column.

### ❌ Actual Behavior

- No ability to resize columns
- _IP_ column width is 3x wider than necessary
- Resizing the window only impacts the "_Hosts_" column, which typically will have wider entries than the IP column on average

### Other Software

Windows 10 Pro x64 | 22H2 19045.4291

## Your task

1. Identify the culprit file(s) and function(s) that must change.
2. Describe the fix (what to change and why).
3. If you can, cite the historical PR/commit that fixed this.

Working tree: `C:\s\Demo\SkillForDistill\benchmark\results\b1\32704\worktree`
