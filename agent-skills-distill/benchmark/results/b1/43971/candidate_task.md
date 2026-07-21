# Bug to fix: [PowerRename] Decomposed Unicode (NFD) and non-breaking spaces are not detected, resulting in mismatches

(Module: PowerRename. The repository is checked out at commit `d87dde132df50206d8ff90b1fb9168f79364c03f` — the bug is present and UNFIXED. Do NOT look for the fix in git history; it does not exist yet at this checkout.)

## Symptom / report

### Microsoft PowerToys version

0.96.1

### Installation method

Dev build in Visual Studio

### Area(s) with issue?

PowerRename

### Steps to reproduce

### File creation

Create two files:

1. `Testй NFD.txt`
2. `Testй NFC.txt`

**Copy these exactly, do not type them in.**

### PowerRename renaming
1. Select the file(s) in Explorer and right-click.
2. Select **Rename with PowerRename**.
3. When PowerRename opens, type "Matched" into the **Replace with** textbox.
4. In the **Search for** textbox, paste `Testй`. 

### ✔️ Expected Behavior

Both files are matched.

### ❌ Actual Behavior

Only the `Testй NFC.txt` file is matched:

<img width="1225" height="585" alt="Image" src="https://github.com/user-attachments/assets/9528e9b5-4749-4063-a3d0-e4ddd737ff4d" />

### Additional Information

If you're having trouble reproducing this, you can use a website such as https://apps.timwhitlock.info/unicode/inspect to confirm that the `й` (i+breve) character differs between these representations - the NFD version of the `й` should show as two code points:

<img width="1421" height="630" alt="Image" src="https://github.com/user-attachments/assets/2c6cc8c7-654b-43b7-8152-8543c014f581" />

### Other Software

_No response_

## Your task

1. Identify the culprit file(s) and function(s) that must change.
2. Describe the fix (what to change and why).
3. If you can, cite the historical PR/commit that fixed this.

Working tree: `C:\s\Demo\SkillForDistill\benchmark\results\b1\43971\worktree`
