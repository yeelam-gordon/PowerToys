# Text Extractor (PowerOCR) — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split note:** `SKILL.md` owns the operational symptom → root-cause → guardrail playbooks. This
> reference keeps only source anchors, evidence chronology, reviewer decisions, unresolved clusters,
> and evidence limits.

## Evidence caveats

- Confirm every anchor against the current branch before changing code.
- Most issue records in the mined corpus were title-level. Rows marked **issue signal** support
  priority, not a proven mechanism.
- Source observations describe the reviewed snapshot; they are stronger than issue-title inference
  but can become stale.
- Product/settings/log names use **TextExtractor**; source directory and namespace use **PowerOCR**.

## Source-anchor ledger

| Area | Exact source anchors | Evidence retained | Decision or current fact | Basis |
|---|---|---|---|---|
| OCR availability | `src/modules/PowerOCR/PowerOCR/Helpers/ImageMethods.cs::GetOCRLanguage`, `::ExtractText`; `Helpers/OcrExtensions.cs::GetOcrResultFromImageAsync` | [#46030](https://github.com/microsoft/PowerToys/issues/46030), [#41969](https://github.com/microsoft/PowerToys/issues/41969), [#41517](https://github.com/microsoft/PowerToys/issues/41517) | Recognition inventory comes from `OcrEngine.AvailableRecognizerLanguages`; engine creation remains fallible. | Source + issue signal |
| Language selection and text assembly | `ImageMethods.cs::GetOCRLanguage`, `::ExtractText`; `Helpers/LanguageHelper.cs::IsLanguageSpaceJoining`; `OcrExtensions.cs::GetTextFromOcrLine` | [#42904](https://github.com/microsoft/PowerToys/issues/42904), [#47137](https://github.com/microsoft/PowerToys/issues/47137) | Resolution currently starts from `InputLanguageManager.Current.CurrentInputLanguage`, permits `UserSettings.PreferredLanguage`, then falls back by abbreviated name/first available; CJK spacing and RTL ordering stay in the language-aware helpers. | Source + issue signal |
| Per-monitor overlay and capture coordinates | `Helpers/WindowUtilities.cs::LaunchOCROverlayOnEveryScreen`; `Helpers/WPFExtensionMethods.cs::GetDpi`; `OCROverlay.xaml.cs::Window_Loaded`, `::RegionClickCanvas_MouseUp` | [#46852](https://github.com/microsoft/PowerToys/issues/46852), [#46088](https://github.com/microsoft/PowerToys/issues/46088), [#43024](https://github.com/microsoft/PowerToys/issues/43024), [#41930](https://github.com/microsoft/PowerToys/issues/41930) | Each `Screen.AllScreens` entry gets its own DPI; WPF bounds are physical-pixel bounds divided by DPI scale; capture coordinates return to pixels through `CompositionTarget.TransformToDevice`. The two-step `MoveWindow` remains an intentional DPI-context coercion. | Source + issue signal |
| Runner activation | `PowerOCRModuleInterface/dllmain.cpp::parse_hotkey`, `::get_hotkeys`, `::on_hotkey`; `PowerOCR/Keyboard/EventMonitor.cs::StartOCRSession` | [#44914](https://github.com/microsoft/PowerToys/issues/44914), [#44505](https://github.com/microsoft/PowerToys/issues/44505) | Runner activation uses the centralized hotkey and shared invoke event; `parse_hotkey` retains its Win+Shift+T fallback when no key is configured. | Source + issue signal |
| Standalone activation | `PowerOCR/App.xaml.cs::Application_Startup`; `PowerOCR/Keyboard/KeyboardMonitor.cs::SetActivationKeys`, `::Hook_KeyboardPressed`; `Keyboard/GlobalKeyboardHook.cs`; `Settings/UserSettings.cs::DefaultActivationShortcut` | [#48785](https://github.com/microsoft/PowerToys/issues/48785), [#43791](https://github.com/microsoft/PowerToys/issues/43791), [#43250](https://github.com/microsoft/PowerToys/issues/43250) | Detached startup uses the low-level hook and the Win+Shift+O settings default. The Runner and standalone defaults are intentionally recorded as different source facts; persisted `settings.json` determines effective behavior. | Source + issue signal |
| STA, composition, clipboard | `OCROverlay.xaml.cs::RegionClickCanvas_MouseUp`; `PowerOCR-UITests/PowerOCRTests.cs` clipboard STA helper | [#42784](https://github.com/microsoft/PowerToys/issues/42784), [#44069](https://github.com/microsoft/PowerToys/issues/44069) | Clipboard access stays on an STA thread and remains exception-contained; `0x80263001` maps to `DWM_E_COMPOSITIONDISABLED`. | Source + issue signal |
| Small-image padding and ownership | `Helpers/ImageMethods.cs::PadImage`, `::GetRegionAsBitmap`, `::GetWindowBoundsImage` | [PR #44906](https://github.com/microsoft/PowerToys/pull/44906) | The accepted padding contract is `bool` + nullable `out` + `[NotNullWhen(true)]`: no allocation when padding is unnecessary, and callers replace/dispose the original only on `true`. The PR does not establish Graphics-before-Bitmap disposal; current `GetRegionAsBitmap` still violates that lifetime. | Merged padding implementation + current-source caveat |
| OCR scaling limit | `ImageMethods.cs::ExtractText`; `OcrExtensions.cs::GetRegionsTextAsTableAsync` | — | The 1.5× scale path remains gated by `OcrEngine.MaxImageDimension`. | Source |
| Project references | PowerOCR project-file references using `$(RepoRoot)` | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | Reviewer-established repository convention: use `$(RepoRoot)` rather than bare traversal paths. | Merged cross-repo decision |

## Decision chronology

| Date | Artifact | Recorded decision |
|---|---|---|
| 2026-02-07 | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) merged | Standardized project references on `$(RepoRoot)`. |
| 2026-04-03 | [PR #44906](https://github.com/microsoft/PowerToys/pull/44906) merged | Adopted the non-allocating `PadImage` try-pattern. It did not establish correct Graphics-before-Bitmap disposal; current source still violates that lifetime. |

## Evidence clusters (lifecycle noted)

| Cluster | Open evidence | What remains unproved by this catalog |
|---|---|---|
| Installed OCR languages and offline machines | [#46030](https://github.com/microsoft/PowerToys/issues/46030), [#41969](https://github.com/microsoft/PowerToys/issues/41969), [#41517](https://github.com/microsoft/PowerToys/issues/41517) | Whether current Windows setup guidance and null handling cover every SKU/offline path. |
| Input language versus user expectation | [#42904](https://github.com/microsoft/PowerToys/issues/42904), [#47137](https://github.com/microsoft/PowerToys/issues/47137) | The desired precedence among keyboard, display, and persisted preferred language. |
| Mixed-DPI and multi-monitor capture | [#46852](https://github.com/microsoft/PowerToys/issues/46852), [#46088](https://github.com/microsoft/PowerToys/issues/46088), [#43024](https://github.com/microsoft/PowerToys/issues/43024), [#41930](https://github.com/microsoft/PowerToys/issues/41930) (all closed) | Historical coverage across topology changes, fractional scaling, and monitor-origin combinations; closed state does not establish one shared cause. |
| Dual activation paths | [#44914](https://github.com/microsoft/PowerToys/issues/44914), [#44505](https://github.com/microsoft/PowerToys/issues/44505), [#48785](https://github.com/microsoft/PowerToys/issues/48785), [#43791](https://github.com/microsoft/PowerToys/issues/43791), [#43250](https://github.com/microsoft/PowerToys/issues/43250) | Whether cleared/custom shortcuts are behaviorally identical in Runner and detached modes. |
| Composition and clipboard failures | [#42784](https://github.com/microsoft/PowerToys/issues/42784), [#44069](https://github.com/microsoft/PowerToys/issues/44069) | Frequency and remaining focused-window or composition-state dependencies. |

## Scope exclusions

Dependency/toolchain churn, command-palette work, spell-check chatter, test-name nits, and other
build-only discussions were not treated as module decisions.
