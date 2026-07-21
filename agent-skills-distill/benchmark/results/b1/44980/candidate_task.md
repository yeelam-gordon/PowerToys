# Bug to fix: Quick accent not showing when typing in second monitor but still adding accents

(Module: PowerAccent. The repository is checked out at commit `6e9b3b1536d20d4c00dc1ae36d53054ad8300f9a` — the bug is present and UNFIXED. Do NOT look for the fix in git history; it does not exist yet at this checkout.)

## Symptom / report

### Microsoft PowerToys version

0.97.0

### Installation method

WinGet

### Area(s) with issue?

Quick Accent

### Steps to reproduce

1. Go to second monitor
2. Press letter (e.g., "a") + activation key (space in my case)
3. "á" is printed but the quick accent bar is not shown anywhere
4. Go to main monitor
5. Press letter "a" + activation key
6. Quick accent bar is shown and "á" is printed.

### ✔️ Expected Behavior

The quick accent bar is shown at all preferably in the same monitor where I am typing.

### ❌ Actual Behavior

The quick accent bar is not shown at all in any monitor.

### Additional Information

OS Version: Microsoft Windows NT 10.0.26200.0 
.NET Version: .NET 9.0.12
PowerToys is running as user (non-elevated)
Install scope: per user
Operating System Language: Catalan (Spain)
System locale: ca-ES

### Other Software

By quick accent bar I mean this:

<img width="908" height="145" alt="Image" src="https://github.com/user-attachments/assets/994efbb6-5ac4-49c1-b88f-e02242e888ba" />

My monitors have different resolutions and different UI scaling settings. My main monitor is 3840x2160 and has 150% scaling. My second monitor is 1920x1080 and has 100% scaling.

This issue did not occur in the previous version (I think it was 0.96.1?).

## Your task

1. Identify the culprit file(s) and function(s) that must change.
2. Describe the fix (what to change and why).
3. If you can, cite the historical PR/commit that fixed this.

Working tree: `C:\s\Demo\SkillForDistill\benchmark\results\b1\44980\worktree`
