# Bug to fix: [PowerRename] Fix date replacement tokens failing if followed by a capital letter

(Module: PowerRename. The repository is checked out at commit `7b0b284d40a1f6b938695b4eee3cd0f09e668b22` — the bug is present and UNFIXED. Do NOT look for the fix in git history; it does not exist yet at this checkout.)

## Symptom / report

## Summary of the Pull Request
Fixes date/time-related replacement tokens being rejected if they were followed by capital letters in the user's replacement string.

<!-- Please review the items on the PR checklist before submitting-->
## PR Checklist

- [x] Closes: #44202
- [ ] **Communication:** I've discussed this with core contributors already. If the work hasn't been agreed, this work might be rejected
- [x] **Tests:** Added/updated and all pass
- [ ] **Localization:** All end-user-facing strings can be localized
- [ ] **Dev docs:** Added/updated
- [ ] **New binaries:** Added on the required places
   - [ ] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json) for new binaries
   - [ ] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs) for new binaries and localization folder
   - [ ] [YML for CI pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml) for new test projects
   - [ ] [YML for signed pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/release.yml)
- [ ] **Documentation updated:** If checked, please file a pull request on [our docs repo](https://github.com/MicrosoftDocs/windows-uwp/tree/docs/hub/powertoys) and link it here: #xxx

<!-- Provide a more detailed description of the PR, other things fixed, or any additional comments/features here -->
## Detailed Description of the Pull Request / Additional comments
With the addition of image metadata replacement options, a strict negative lookahead was added to the date replacement regular expressions to prevent conflicts. This was required because, for example, `$D` would otherwise match before `$DATE_TAKEN_YYYY`. Metadata and date-related replacements are executed separately at the moment, so this awareness of each other is required.

However, the negative lookahead was far too aggressive:
- It used `(?![A-Z])`, meaning any capital letter after the date token would reject the match entirely. This caused the problem referred to in the linked issue, where `$DDT` was rejected instead of matching to the `$DD` replacement followed by a verbatim `T` character.
- It was applied to the majority of fields, whereas it is only actually needed where date tokens are prefixes of metadata tokens. Only `$D` and `$H` are affected.
- There was no need to apply negative lookups to catch 'self-matches' like preventing `$D`, `$DD`, and `$DDD` from matching when `$DDDD` was in the replacement string. Instead, the order of processing already matches the longest token first, so this could never happen.

To fix these issues, I:
- Removed the majority of the negative lookaheads.
- Made the remaining negative lookaheads only match actual conflicting suffixes, e.g. `$D(?!(ATE_TAKEN_|ESCRIPTION|OCUMENT_ID))` instead of `$D(?![A-Z])`. This makes mistaken rejections of user-supplied replacement strings much more unlikely.

Please note: there remain inherent issues with the current token replacement approach. Tackling these will require a more extensive refactoring PR which separates replacement string tokenization from matching and replacement, and which tackles both image metadata and file date metadata in a unified manner.

## Validation Steps Performed
- Corrected unit tests which classified, for example, `$YYY` as invalid, instead of identifying it as a valid `$YY` token + verbatim capital Y replacement.
- Wrote new unit tests to exercise the refined negative lookaheads.
- Wrote tests to confirm certain negative lookaheads were not required because the order of processing guaranteed the longest match happens before any prefix matches.
- Wrote a unit test to exercise the specific issue raised in #44202.
- Confirmed that all new and pre-existing PowerRename tests pass.

## Your task

1. Identify the culprit file(s) and function(s) that must change.
2. Describe the fix (what to change and why).
3. If you can, cite the historical PR/commit that fixed this.

Working tree: `C:\s\Demo\SkillForDistill\benchmark\results\b1\44267\worktree`
