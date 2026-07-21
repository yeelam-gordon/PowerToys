# Bug to fix: [PowerRename] Regex used to work, now showing unexpected behavior

(Module: PowerRename. The repository is checked out at commit `7b0b284d40a1f6b938695b4eee3cd0f09e668b22` — the bug is present and UNFIXED. Do NOT look for the fix in git history; it does not exist yet at this checkout.)

## Symptom / report

### Microsoft PowerToys version

0.96.1

### Installation method

PowerToys auto-update

### Area(s) with issue?

PowerRename

### Steps to reproduce

I get a lot of files from a scanner, named scan1.pdf, scan001.pdf, scan_dherh43754.pdf, S55C-894739023.pdf, etc.

I've used, for a long time now, the following pattern Scan\d{0,4}|S55C\-\d+ in the 1st box, and in the second box, the "rename to" field I've used this: Scan$YYYY-$MM-$DDT$hh$mm$ss so that I get the word "Scan" then an ISO datetime as the file name. 

After a recent update it's renaming files like this: Scan2025-12-$DDT121915.pdf rather than the expected Scan2025-12-10T121915.pdf. What gives? Is this a bug or a recent change I need to fix my string to match? I have regex enabled, Boost library enabled, and "Apply to" set to Filename only.

### ✔️ Expected Behavior

file renamed to Scan2025-12-$10T121915.pdf (or Scan+Date+T+Time concatenated)

### ❌ Actual Behavior

the file was renamed to "Scan2025-12-$DDT121915.pdf." This is repeatable no matter the date or time, although I've only been testing it during the day.

### Additional Information

WIndows 11


### Other Software

_No response_

## Your task

1. Identify the culprit file(s) and function(s) that must change.
2. Describe the fix (what to change and why).
3. If you can, cite the historical PR/commit that fixed this.

Working tree: `C:\s\Demo\SkillForDistill\benchmark\results\b1\44202\worktree`
