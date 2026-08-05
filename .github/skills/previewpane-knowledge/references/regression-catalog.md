# PreviewPane — Evidence and Decision Ledger

[Return to actionable playbooks](../SKILL.md).

> **Split note:** `SKILL.md` owns operational regression playbooks, review rules, and guardrails.
> This companion retains the historical evidence, source anchors, chronology, decisions, unresolved
> clusters, and caveats without duplicating those explanations.

Confirm every anchor against the currently registered managed or native implementation before
acting; both generations coexist.

## Change and regression evidence

| Approximate chronology | Evidence | Source anchors | Historical finding or decision |
|---|---|---|---|
| Earlier fix | in-source `Fixes #17527` | `SvgPreviewHandlerHelper.SwapNamespaces` | Inkscape v1.1 SVGs could render as text when default and SVG namespaces appeared in an unexpected order. |
| Earlier fix | in-source `fixes #18286` | `SvgThumbnailProvider.cs`; pre-capture body overflow change | Captured SVG thumbnails could contain a scrollbar; the capture path hides body overflow. |
| 1 | [PR #37651](https://github.com/microsoft/PowerToys/pull/37651) | PreviewPane UnitTests project files | Preview tests migrated to Microsoft.Testing.Platform; MSTest projects build as executables. |
| 2 | issue [#42732](https://github.com/microsoft/PowerToys/issues/42732) | `PdfThumbnailProvider.cs::DoGetThumbnail`; `PageToImage`; `MaxThumbnailSize` | A crafted PDF could cause excessive memory use, motivating bounded requested dimensions and page-0-only rendering. |
| 3 | issue [#43059](https://github.com/microsoft/PowerToys/issues/43059) | `SvgHTMLPreviewGenerator.cs`; `SvgThumbnailProvider.WrapSVGInHTML`; raw SVG passed through `string.Format` | Literal `{` or `}` in SVG content could be parsed as format placeholders and break preview/thumbnail generation. |
| 4 | [PR #44639](https://github.com/microsoft/PowerToys/pull/44639) | PreviewPane vcxproj/csproj paths; `$(RepoRoot)` | Project-path cleanup rejected `$(ProjectDir)$(RepoRoot)` composition and unnecessary `..\` segments. |
| 5 | [PR #45801](https://github.com/microsoft/PowerToys/pull/45801) | `MarkdownPreviewHandlerControl.cs` `NavigationStarting` | User-initiated links from untrusted previews were restricted to HTTP/HTTPS after arbitrary URI schemes could launch protocol handlers. |
| 6 | issue [#46386](https://github.com/microsoft/PowerToys/issues/46386) | `SvgThumbnailProvider.cs::GetThumbnailImpl`; per-thumbnail WebView2 environment; `Application.DoEvents()` loop | SVG thumbnail generation showed sustained CPU/high-power behavior; setup and message-pump cost remain scalability evidence. |
| 7 | [PR #46729](https://github.com/microsoft/PowerToys/pull/46729) | resx-to-rc project step; `convert-resx-to-rc.ps1`; `$(MSBuildThisFileDirectory)` | Build reliability required quoted paths and disabling PowerShell module auto-loading rather than suppressing warnings. |
| 8 | [PR #47391](https://github.com/microsoft/PowerToys/pull/47391) | `MarkdownPreviewHandlerControl.cs`; mirrored guard in `SvgThumbnailProvider.cs::GetThumbnailImpl`; `MarkdownPreviewHandlerTest.cs` | `NavigateToString` limits are based on UTF-8 bytes, not `string.Length`; multi-byte coverage was added after review. |

## Source-state decision ledger

| Area | Source anchors | Decision retained |
|---|---|---|
| WebView2 isolation | `SvgPreviewControl.cs`; `SvgThumbnailProvider.cs`; `MarkdownPreviewHandlerControl.cs`; `AddWebResourceRequestedFilter("*")`; `WebResourceRequested`; `CoreWebView2HostResourceAccessKind.Deny`; `--block-new-web-contents` | Script, web messages, host objects, autofill, DevTools, dialogs, and context menus are disabled; resource requests other than the local file receive 403; host-resource access is denied; new web contents are blocked. |
| SVG blocked content | `SvgPreviewHandlerHelper.CheckBlockedElements` | SVG `<script>` is treated as blocked content before rendering. |
| SVG normalization | `SvgPreviewHandlerHelper.SwapNamespaces`; `AddStyleSVG` | Namespace and style normalization remain part of the render pipeline because earlier files depended on them. |
| PDF work bounds | `PdfThumbnailProvider.DoGetThumbnail`; `PageToImage`; `MaxThumbnailSize = 10000` | Zero or oversized requests are rejected and only page 0 is rendered to bounded dimensions. |
| Registration/configuration | `PowerPreviewModule` constructor in `powerpreview.cpp`; `CLSID.h`; Settings toggle; provider render-time GPO check | A format's setting name, GPO rule, registry change-set, handler/SHIM CLSIDs, UI toggle, and runtime policy check are one coordinated decision surface. |

## Unresolved clusters (at distillation time)

- **SVG thumbnail scalability:** [#46386](https://github.com/microsoft/PowerToys/issues/46386)
  records high power/CPU around per-thumbnail WebView2 creation and spin-waiting; the ledger does not
  establish that a later architectural fix removed the cost.
- **Hostile or pathological input:** PDF memory pressure
  [#42732](https://github.com/microsoft/PowerToys/issues/42732) and SVG wrapper fragility
  [#43059](https://github.com/microsoft/PowerToys/issues/43059) are separate manifestations of
  untrusted-file risk.
- **Managed/native divergence:** most formats have both managed and `*Cpp` implementations.
  Historical evidence may anchor the non-shipping generation unless registration is checked first.

## Caveats and excluded noise

- The WebView2 restrictions above are a snapshot of source state, not evidence that every future
  render path automatically inherits them.
- `NavigateToString` evidence applies to the final generated HTML byte count, not merely the input
  file size or character count.
- The registration “quartet” shorthand is incomplete unless the CLSID pair and Settings toggle are
  also checked.
- Build/infra PRs #44639, #46729, and #37651 touched PreviewPane but are not rendering regressions.
- Excluded as non-durable noise: check-spelling allowlist churn
  [#47119](https://github.com/microsoft/PowerToys/pull/47119), MSTEST0017 assertion ordering
  [#46712](https://github.com/microsoft/PowerToys/pull/46712), mechanical .NET/CppWinRT/VS upgrades
  [#41280](https://github.com/microsoft/PowerToys/pull/41280),
  [#45420](https://github.com/microsoft/PowerToys/pull/45420), and
  [#44304](https://github.com/microsoft/PowerToys/pull/44304), plus shared Settings comments in
  [#44064](https://github.com/microsoft/PowerToys/pull/44064) that do not touch a preview or thumbnail
  render path.
