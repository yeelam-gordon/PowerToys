# Bug to fix: [PowerRename] Fix Unicode characters and non-breaking spaces not being correctly normalized before matching

(Module: PowerRename. The repository is checked out at commit `d87dde132df50206d8ff90b1fb9168f79364c03f` — the bug is present and UNFIXED. Do NOT look for the fix in git history; it does not exist yet at this checkout.)

## Symptom / report

## Summary of the Pull Request
Fixes PowerRename failing to normalise different Unicode forms before matching. This results in filenames containing visually identical characters to the search term from failing to match because their underlying binary representations differ.

This affects renaming files created on macOS which names files in NFD (decomposed form) rather than Windows' NFC (precomposed form).

Additionally, this fixes matching to filenames containing non-breaking space characters, which can be created by automated systems and web downloaders. Previously, the NBSP character would fail to match a normal space.

<!-- Please review the items on the PR checklist before submitting-->
## PR Checklist

- [x] Closes: #43971
- [x] Closes: #43815
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
The underlying issue is a binary mismatch between:

1. Precomposed characters (NFC) typed by Windows users, e.g. `U+0439` - `й`.
2. Decomposed characters (NFD) found in filenames from other platforms (or copied from text), e.g. `U+0438` `U+0306` - `и` + `̆ `.
3. Standard spaces (`U+0020`) versus non-breaking spaces (`U+00A0`).

### Updates to PowerRenameRegex.cpp

I added a `SanitizeAndNormalize` function which replaces all non-breaking spaces with standard spaces and normalises the string to **Normalization Form C** using Win32's `NormalizeString`.

`PutSearchTerm` and `PutReplaceTerm` now normalise input immediately before performing any other processing.

`Replace` now normalises the `source` filename before processing.

I updated the RegEx path to ensure it runs against the normalised `sourceToUse` string instead of the raw `source` string; otherwise regex matches would fail.

<!-- Describe how you validated the behavior. Add automated tests wherever possible, but list manual validation steps taken as well -->
## Validation Steps Performed
Manually tested the use case detailed in #43971 with the following filenames:

- `Testй NFC.txt`
- `Testй NFD.txt`

Result:
<img width="1097" height="542" alt="image" src="https://github.com/user-attachments/assets/55dd4f01-8ec9-462c-a20f-dd246c368cf5" />

There are two new unit tests which exercise both the non-breaking space and Unicode form normalisation issues. These run on both the Boost- and non-Boost test paths, adding four tests to the total. All new tests fail as expected on the prior code and all PowerRename tests pass successfully with the changes in this PR:

<img width="606" height="276" alt="image" src="https://github.com/user-attachments/assets/08dc01f6-201c-4d56-8f34-e5043e3d1e86" />



## Your task

1. Identify the culprit file(s) and function(s) that must change.
2. Describe the fix (what to change and why).
3. If you can, cite the historical PR/commit that fixed this.

Working tree: `C:\s\Demo\SkillForDistill\benchmark\results\b1\43972\worktree`
