---
name: previewpane-knowledge
description: 'PowerToys PreviewPane module knowledge: File Explorer preview handlers (SVG, Markdown, Monaco/source-code, PDF, G-code, BG-code, QOI) and thumbnail providers (SVG, PDF, STL, G-code, BG-code, QOI). Feature->file/function map, regression playbooks (WebView2 NavigateToString byte-vs-char limit, URI-scheme sandbox isolation of untrusted files, SVG blocked-element/namespace handling, PDF/SVG resource exhaustion, per-format COM registration, STA/threading), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/previewpane. Keywords: preview handler, thumbnail provider, IPreviewHandler, IThumbnailProvider, WebView2, SVG, Markdown, Monaco, PDF, gcode, QOI, STL, COM registration, CLSID, sandbox, STA, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys PreviewPane Knowledge

Grounded engineering knowledge for the PowerToys **PreviewPane** module — the set of Windows
Explorer **preview handlers** (Reading/Preview pane) and **thumbnail providers** that render
untrusted files: SVG, Markdown, source code (Monaco), PDF, 3D-print G-code/BG-code, QOI images,
and STL meshes. Use it to localize code fast, avoid known regression traps, and enforce the
conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/previewpane/` and needing prior art.
- Fixing/triaging a preview/thumbnail bug: a format not rendering, Explorer/preview crash,
  WebView2 exception, blank thumbnail, high CPU/power draw, handler not registered after
  install/GPO change, or preview shown while renaming.
- Reviewing a PreviewPane PR against maintainer conventions and security/reliability traps.
- Adding a **new format** handler or thumbnail provider (new CLSID + registration + sandbox isolation).
- Touching WebView2-hosted rendering (SVG/Markdown/Monaco), COM registration, or STA/threading.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see
anti-anchoring below). Root: `src/modules/previewpane/`. Two generations coexist: **managed C#**
handlers (e.g. `SvgPreviewHandler/`) and newer **native C++** rewrites (the `*Cpp/` siblings).

### Registration / module host
| Sub-feature | Implementation (file · function) |
|---|---|
| PowerToys module: enable/disable each handler, GPO gate, registry change-sets | `powerpreview/powerpreview.cpp` `PowerPreviewModule` ctor (per-handler `m_fileExplorerModules` entries: settingName, `checkModuleGPOEnabledRuleFunction`, `registryChanges`) |
| All handler/provider CLSIDs (+ SHIMActivate CLSIDs) | `powerpreview/CLSID.h` |
| Registry change-set builders (`get<Fmt>PreviewHandlerChangeSet` / `get<Fmt>ThumbnailHandlerChangeSet`) | `powerpreview/` (per-format), applied via `settings_objects` registry helpers |
| DLL entry / COM class factory (native handlers) | `<Fmt>PreviewHandlerCpp/dllmain.cpp`, `ClassFactory.cpp`, `GlobalExportFunctions.def` |
| Shell COM interfaces (managed interop) | `common/cominterop/` `IPreviewHandler.cs`, `IThumbnailProvider.cs`, `IInitializeWithStream.cs`, `IInitializeWithFile.cs`, `IObjectWithSite.cs`, `IPreviewHandlerFrame.cs` |
| Managed handler process entry point | `<Fmt>PreviewHandler/Program.cs` (out-of-proc COM server) |
| Preview handler common base (WinForms host control) | `common/` `FormHandlerControl` (base of `SvgPreviewControl`, `MonacoPreviewHandlerControl`) |

### Preview handlers (Reading/Preview pane)
| Format | Implementation (file · function) |
|---|---|
| SVG preview (C#) | `SvgPreviewHandler/SvgPreviewControl.cs` `DoPreview`; HTML gen `SvgHTMLPreviewGenerator.cs` `GeneratePreview`; sanitize/blocked-elements `common/Utilities/SvgPreviewHandlerHelper.cs` `CheckBlockedElements`, `SwapNamespaces`, `AddStyleSVG` |
| SVG preview (native) | `SvgPreviewHandlerCpp/SvgPreviewHandler.cpp` |
| Markdown preview | `MarkdownPreviewHandler/MarkdownPreviewHandlerControl.cs` `DoPreview` (Markdig → HTML → WebView2); native `MarkdownPreviewHandlerCpp/` |
| Monaco / source-code preview | `MonacoPreviewHandler/MonacoPreviewHandlerControl.cs`; encoding detect `UtfUnknown`; file map `FileHandler.cs`; native `MonacoPreviewHandlerCpp/` |
| PDF preview | `PdfPreviewHandler/`; native `PdfPreviewHandlerCpp/` |
| G-code / BG-code preview | `GcodePreviewHandler/`, `BgcodePreviewHandler/` (+ `*Cpp/`) — parse embedded thumbnail/layers |
| QOI image preview | `QoiPreviewHandler/` (+ `QoiPreviewHandlerCpp/`) |

### Thumbnail providers (Explorer icons)
| Format | Implementation (file · function) |
|---|---|
| SVG thumbnail | `SvgThumbnailProvider/SvgThumbnailProvider.cs` `GetThumbnail`/`GetThumbnailImpl` (WebView2 `CapturePreviewAsync`) |
| PDF thumbnail | `PdfThumbnailProvider/PdfThumbnailProvider.cs` `GetThumbnail`→`DoGetThumbnail`→`PageToImage` (`Windows.Data.Pdf`) |
| STL thumbnail | `StlThumbnailProvider/` (mesh render) |
| G-code / BG-code thumbnail | `GcodeThumbnailProvider/`, `BgcodeThumbnailProvider/` (extract embedded PNG) |
| QOI thumbnail | `QoiThumbnailProvider/` |

**Registration invariant:** every format is one row in `PowerPreviewModule`'s constructor with a
`settingName`, a GPO rule function, and a registry change-set — **and** a matching CLSID pair in
`CLSID.h` (the real handler CLSID + a `SHIMActivate…` CLSID). Adding a format means touching all
three plus the Settings UI toggle. GPO is re-checked at render time inside each provider (e.g.
`GetConfiguredPdfThumbnailsEnabledValue()` inside `DoGetThumbnail`), not only at registration.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### WebView2 `NavigateToString` byte-vs-character limit (crash on large/CJK files)
- **Symptom:** Markdown/SVG preview crashes (`ArgumentException`) on files that are ~2 MB on disk
  but under ~1.5 M characters — e.g. CJK-heavy content — because the char-count guard let them
  through.
- **Where:** `MarkdownPreviewHandlerControl.cs` (~line 187); parallel guard in
  `SvgThumbnailProvider.cs::GetThumbnailImpl`.
- **Root cause:** `NavigateToString`'s ~1.5 MB limit is measured in **UTF-8 bytes**, but the guard
  used `string.Length` (UTF-16 code-unit count). Multi-byte characters inflate byte size above the
  limit while the char count stays under it.
- **Guardrail:** gate on `System.Text.Encoding.UTF8.GetByteCount(html) > 1_500_000`; over the
  limit, write a temp `.html` and navigate to its file URI instead of `NavigateToString`. Add a
  regression test for the multi-byte case (char count under, byte count over). Evidence:
  [PR #47391](https://github.com/microsoft/PowerToys/pull/47391) (Copilot flagged the missing
  multi-byte test; coverage added in `MarkdownPreviewHandlerTest.cs`).

### Untrusted-link URI-scheme execution from the preview pane (security)
- **Symptom:** clicking a link in a Markdown/HTML preview could launch **arbitrary** protocol
  handlers (`calculator:`, `search-ms:`, custom schemes) — the preview renders untrusted files.
- **Where:** `MarkdownPreviewHandlerControl.cs` `NavigationStarting` handler.
- **Root cause:** external navigation was launched via `Launcher.LaunchUriAsync` without validating
  the URI scheme.
- **Guardrail:** on user-initiated navigation, cancel it and only `LaunchUriAsync` when
  `uri.Scheme == Uri.UriSchemeHttp || Uri.UriSchemeHttps`; block everything else. Previews render
  untrusted input — treat every navigation/resource request as hostile.
  ([OWASP URL validation](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html);
  fix [PR #45801](https://github.com/microsoft/PowerToys/pull/45801)).

### WebView2 sandbox weakened (untrusted SVG/HTML rendered with capabilities)
- **Symptom:** an untrusted SVG/Markdown could fetch remote resources, run script, or show
  autofill/DevTools/context menus in the preview.
- **Where:** WebView2 setup in `SvgPreviewControl.cs`, `SvgThumbnailProvider.cs::GetThumbnailImpl`,
  `MarkdownPreviewHandlerControl.cs` — the `CoreWebView2.Settings.*` block + the
  `AddWebResourceRequestedFilter("*")` / `WebResourceRequested` 403 filter.
- **Root cause:** WebView2 defaults are permissive; each new render path must re-disable script,
  web messages, host objects, autofill, DevTools, default dialogs/context menus, and block **all**
  resource requests except the single local file URI.
- **Guardrail:** keep the full "deny" settings block and the resource filter that returns HTTP 403
  for any URI ≠ `_localFileURI`; use `--block-new-web-contents` env option and
  `CoreWebView2HostResourceAccessKind.Deny`. Never relax these to "fix" a rendering gap.
  Evidence: SVG blocked-element info-bar path (`CheckBlockedElements` blocks `<script>`) in
  `SvgPreviewHandlerHelper.cs`.

### SVG parse/namespace/content fragility (blank or mis-rendered preview)
- **Symptom:** SVG previewed as raw text or blank — e.g. Inkscape v1.1 files (swapped default/svg
  namespace order), or SVGs containing characters that break string-formatting of the HTML wrapper
  (`{`/`}` treated as format placeholders).
- **Where:** `SvgPreviewHandlerHelper.cs` `SwapNamespaces` (fixes #17527), `AddStyleSVG`;
  wrapper `string.Format` sites (`SvgHTMLPreviewGenerator.cs`, `SvgThumbnailProvider.WrapSVGInHTML`).
- **Root cause:** browsers/parsers reject reordered namespaces; and passing raw SVG through
  `string.Format` misinterprets literal `{`/`}` as format items.
- **Guardrail:** route SVG through `SwapNamespaces`+`AddStyleSVG` before rendering; when composing
  HTML, ensure braces in SVG payload aren't parsed as format placeholders (escape or use a
  non-format concatenation). Evidence: issue
  [#43059](https://github.com/microsoft/PowerToys/issues/43059) (SVG thumbnail fails on `{`),
  in-code `Fixes #17527`/`#18286` comments.

### PDF/SVG resource exhaustion ("memory bomb", high power draw)
- **Symptom:** a crafted or huge PDF/SVG causes the thumbnail/preview process to consume excessive
  memory/CPU or draw high power continuously.
- **Where:** `PdfThumbnailProvider.cs::DoGetThumbnail`/`PageToImage`;
  `SvgThumbnailProvider.cs::GetThumbnailImpl` (busy `Application.DoEvents()` wait loop).
- **Root cause:** rendering only the needed page/size isn't bounded against pathological inputs;
  WebView2 spin-wait and per-thumbnail environment creation are costly at scale.
- **Guardrail:** enforce `MaxThumbnailSize` bounds (reject `cx == 0 || cx > 10000`), render only
  page 0, cap render dimensions, and clean the temp user-data folder; be conservative adding work
  to the render path. Evidence: issues
  [#42732](https://github.com/microsoft/PowerToys/issues/42732) (PDF memory bomb),
  [#46386](https://github.com/microsoft/PowerToys/issues/46386) (SVG thumbnail high power draw).

### Handler not registered / preview broken after install, upgrade, or GPO change
- **Symptom:** a format stops previewing or generating thumbnails after install/upgrade, or when its
  per-utility GPO is toggled; Explorer shows the generic icon.
- **Where:** `powerpreview/powerpreview.cpp` per-handler `registryChanges` + GPO rule function; the
  render-time GPO re-check inside each provider; native COM registration
  (`*Cpp/dllmain.cpp`, `GlobalExportFunctions.def`, `CLSID.h`).
- **Root cause:** the settingName / CLSID / change-set / GPO-rule quartet drifted out of sync, or a
  new format was added without all four.
- **Guardrail:** keep `settingName`, GPO rule, registry change-set, and `CLSID.h` entries
  consistent; verify both register **and** unregister paths; confirm the render-time GPO check
  matches the registration GPO rule.

## Review Rules

Enforce these when reviewing or authoring PreviewPane changes:

- **Guard WebView2 size on bytes, not characters.** Any `NavigateToString` path must gate on
  `Encoding.UTF8.GetByteCount(...)` and fall back to a temp-file URI; ship a multi-byte (CJK)
  regression test ([PR #47391](https://github.com/microsoft/PowerToys/pull/47391)).
- **Validate URI schemes before launching.** External navigation from a preview may only open
  http/https; block all other schemes to stop arbitrary protocol-handler execution
  ([PR #45801](https://github.com/microsoft/PowerToys/pull/45801)).
- **Never weaken the WebView2 sandbox.** Keep script/web-message/host-object/autofill/DevTools
  disabled and the `WebResourceRequested` 403 filter that permits only `_localFileURI`. Previews
  render untrusted files.
- **Re-check GPO at render time, not just registration.** Each provider must early-return when its
  `GetConfigured<Fmt>…EnabledValue()` is `Disabled` (pattern in `PdfThumbnailProvider.DoGetThumbnail`).
- **A new format is a quartet + CLSID + toggle.** New handler ⇒ add `m_fileExplorerModules` entry
  (settingName, GPO rule, change-set) in `powerpreview.cpp`, CLSID pair in `CLSID.h`, and a Settings
  UI toggle — all consistent.
- **Bound thumbnail work against hostile inputs.** Enforce `MaxThumbnailSize`, render only page 0 /
  fixed dimensions, and clean temp folders — assume PDFs/SVGs may be memory bombs (#42732, #46386).
- **Keep SVG sanitize/namespace helpers in the path.** Route through
  `SvgPreviewHandlerHelper.SwapNamespaces`/`AddStyleSVG`/`CheckBlockedElements`; don't hand-roll
  per-handler SVG munging (#17527, #43059).
- **PowerShell build steps: quote path args; don't suppress warnings.** Disable module auto-loading
  instead of `$WarningPreference='SilentlyContinue'`, and quote `$(MSBuildThisFileDirectory)`
  ([PR #46729](https://github.com/microsoft/PowerToys/pull/46729)).
- **Use `$(RepoRoot)`, not `$(ProjectDir)$(RepoRoot)` or `..\..\`** in preview vcxproj/csproj
  include/output paths ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).
- **Ship a test with every fix.** Suites live in `src/modules/previewpane/UnitTests-*`.

## Pitfalls

- **`NavigateToString`'s limit is bytes, not `string.Length`.** A 700K-CJK-char string is ~2.1 MB
  of UTF-8 and crashes the API though `.Length` is under 1.5M. Always measure with
  `Encoding.UTF8.GetByteCount` (#47391).
- **Preview pane renders untrusted, attacker-controlled files.** Treat every link, resource
  request, external entity, and URI scheme as hostile — restrict schemes to http/https, deny all
  WebView2 resource loads except the local file, and disable script (#45801).
- **Two generations coexist.** Most formats have both a managed `<Fmt>PreviewHandler/` and a native
  `<Fmt>PreviewHandlerCpp/`. Confirm which one is registered/shipping before "fixing" the wrong one.
- **Raw SVG through `string.Format` breaks on `{`/`}`.** Literal braces in SVG are misread as format
  placeholders — escape or concatenate instead (#43059).
- **Reordered SVG namespaces render as text.** Inkscape v1.1 swapped default/svg namespace order;
  `SwapNamespaces` exists specifically to fix this (#17527) — keep it in the pipeline.
- **Thumbnail providers spin-wait with `Application.DoEvents()`.** `SvgThumbnailProvider` pumps
  messages every 75 ms until render completes; per-thumbnail WebView2 environment creation is
  expensive — a driver of high power draw (#46386). Don't add heavy work to this loop.
- **GPO is enforced twice.** Registration uses `checkModuleGPOEnabledRuleFunction`; the provider
  **also** early-returns on `GpoRuleConfigured.Disabled` at render time. Update both together.
- **Handlers are out-of-proc COM servers** (`Program.cs` / native `dllmain.cpp` class factory).
  Registration lives in `powerpreview.cpp` + `CLSID.h`, not in the handler project alone.

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + key decisions.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a PreviewPane PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/previewpane/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/previewpane)
- [WebView2 NavigateToString](https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatetostring) · [IPreviewHandler](https://learn.microsoft.com/windows/win32/api/shobjidl_core/nn-shobjidl_core-ipreviewhandler) · [IThumbnailProvider](https://learn.microsoft.com/windows/win32/api/thumbcache/nn-thumbcache-ithumbnailprovider) · [OWASP Input Validation](https://cheatsheetseries.owasp.org/cheatsheets/Input_Validation_Cheat_Sheet.html)
