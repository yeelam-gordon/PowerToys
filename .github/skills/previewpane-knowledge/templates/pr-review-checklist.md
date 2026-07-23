# PreviewPane PR Review Checklist

Apply after reading the diff cold (see anti-anchoring in SKILL.md). Only check rows the diff touches.

## Security (previews render untrusted files)
- [ ] External navigation restricted to `http`/`https` only; all other URI schemes blocked before `LaunchUriAsync` (#45801).
- [ ] WebView2 sandbox intact: script, web messages, host objects, autofill, DevTools, default dialogs/context menus disabled.
- [ ] `WebResourceRequested` filter returns 403 for any URI ≠ the single local file URI; `AddWebResourceRequestedFilter("*")` present.
- [ ] `--block-new-web-contents` env option and `CoreWebView2HostResourceAccessKind.Deny` kept.
- [ ] SVG routed through `CheckBlockedElements` (blocks `<script>`); no hand-rolled sanitization.

## Reliability / resource exhaustion
- [ ] `NavigateToString` size guard uses `Encoding.UTF8.GetByteCount(...) > 1_500_000`, NOT `string.Length` (#47391).
- [ ] Over-limit content falls back to temp-file URI navigation; temp folder cleaned.
- [ ] Thumbnail size bounded: reject `cx == 0 || cx > MaxThumbnailSize (10000)`; render only page 0 / fixed dimensions (#42732, #46386).
- [ ] No heavy work added to the `Application.DoEvents()` spin-wait loop.
- [ ] Exceptions in render paths are caught and logged (not silently swallowed).

## Format correctness
- [ ] SVG passes through `SwapNamespaces` + `AddStyleSVG` before rendering (#17527).
- [ ] Raw SVG/HTML not passed to `string.Format` where literal `{`/`}` could be misread (#43059).
- [ ] Encoding detection preserved for Monaco/source preview (`UtfUnknown`).

## Registration / configuration
- [ ] New format adds all four: `m_fileExplorerModules` entry (settingName, GPO rule, change-set) in `powerpreview.cpp`, CLSID pair in `CLSID.h`, and Settings UI toggle.
- [ ] GPO re-checked at render time (`GetConfigured<Fmt>…EnabledValue() == Disabled` early-return), matching the registration GPO rule.
- [ ] Both register and unregister paths verified.
- [ ] Correct generation edited — managed `<Fmt>PreviewHandler/` vs native `<Fmt>PreviewHandlerCpp/`.

## Build hygiene
- [ ] Paths use `$(RepoRoot)`, not `$(ProjectDir)$(RepoRoot)` or `..\..\` (#44639).
- [ ] PowerShell build steps quote path args and disable module auto-loading instead of suppressing warnings (#46729).

## Tests
- [ ] Regression test added under `src/modules/previewpane/UnitTests-*` covering the fixed case (esp. multi-byte/CJK, oversized, or malformed inputs).
