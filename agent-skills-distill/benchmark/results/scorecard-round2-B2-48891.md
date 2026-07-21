# Round 2 Scorecard — B2 PR-recall (PR #48891, PowerAccent WinUI migration)

| Reviewer | Comments | GT matched | Recall@1 |
|----------|----------|-----------|----------|
| Candidate (WITH distilled knowledge) | 14 | 2 | 0.20 |
| Baseline (WITHOUT) | 14 | 2 | 0.20 |

**Lift: 0.00 (tie).**

## Finding (honest)
Ground truth for #48891 is 8/10 informal design-discussion comments (UX revert, ThemeListener,
xaml namespace, x:Bind->UserControl) that NO technical distillation can predict. Both Opus
reviewers raised sound technical concerns (DPI, threading, enum-sync) that were simply not what
maintainers flagged on this UI-migration PR.

## Root cause
Held-out test PRs (44304 build, 48891 migration, 44767 small feat) are NOT
technical-bug-fix PRs with code-level review threads -> they don't exercise the skill's strength.

## Round-3 correction
Hold out TECHNICAL bug-fix PRs with substantive code-level review, re-distill without them,
measure recall on the technical review comments. Candidate technical test PRs:
PowerRename #44267 (regex longest-match), PowerAccent #46593 (shift-state), AdvancedPaste #46486 (Electron modifiers).
Also: categorize ground-truth into technical vs procedural and report recall on the technical subset.
