# PreviewPane Bug Triage — Symptom → Likely File/Function

Use the Module Map in SKILL.md to confirm in source. First decide **preview handler vs thumbnail
provider** and **managed (`<Fmt>PreviewHandler/`) vs native (`<Fmt>PreviewHandlerCpp/`)**.

| Symptom | Start here | Notes |
|---|---|---|
| Markdown/SVG preview crashes on large or CJK file | `MarkdownPreviewHandlerControl.cs` byte-count guard before `NavigateToString`; `SvgThumbnailProvider.cs::GetThumbnailImpl` | Byte-vs-char `NavigateToString` limit — use `Encoding.UTF8.GetByteCount` (#47391) |
| Clicking a link opens an app / custom protocol | `MarkdownPreviewHandlerControl.cs` `NavigationStarting` | Restrict to http/https (#45801) |
| Untrusted SVG/HTML runs script or loads remote content | WebView2 `Settings.*` block + `WebResourceRequested` filter in `SvgPreviewControl.cs` / `SvgThumbnailProvider.cs` / `MarkdownPreviewHandlerControl.cs` | Sandbox weakened; keep deny settings |
| SVG previews as raw text / blank | `SvgPreviewHandlerHelper.cs` `SwapNamespaces`, `AddStyleSVG` | Namespace reorder (#17527) |
| SVG thumbnail fails on file containing `{` | `string.Format` sites in `SvgHTMLPreviewGenerator.cs` / `SvgThumbnailProvider.WrapSVGInHTML` | Braces read as format placeholders (#43059) |
| Huge/crafted PDF exhausts memory | `PdfThumbnailProvider.cs::DoGetThumbnail`/`PageToImage` | Bound size, render page 0 only (#42732) |
| SVG thumbnails cause high CPU/power draw | `SvgThumbnailProvider.cs::GetThumbnailImpl` `Application.DoEvents()` loop | Per-thumbnail WebView2 env creation (#46386) |
| Monaco/source preview shows wrong encoding | `MonacoPreviewHandlerControl.cs`, `FileHandler.cs` (`UtfUnknown`) | Encoding detection |
| A format stops previewing after install/upgrade/GPO | `powerpreview/powerpreview.cpp` change-set + GPO rule; `CLSID.h`; `*Cpp/dllmain.cpp` | Quartet drift; check register + unregister |
| Blank thumbnail for one format only | that format's `<Fmt>ThumbnailProvider/*.cs` `GetThumbnail`; GPO early-return | Confirm `MaxThumbnailSize` and GPO check |
| Preview appears while renaming a file/folder | preview handler activation path | Reported #45672/#45186 (de-dup/triage) |

If the symptom doesn't map cleanly, reason from the symptom and verify in source — do not force-fit
a row (a thin map entry can anchor you onto a confident, wrong file).
