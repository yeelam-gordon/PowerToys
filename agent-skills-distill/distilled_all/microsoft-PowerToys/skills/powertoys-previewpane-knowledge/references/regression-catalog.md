# PreviewPane Regression Catalog

Fuller catalog behind the SKILL.md playbooks. Every entry is grounded in the module's own
history (PRs/issues) or in-source `Fixes #NNNN` comments. Confirm in source before acting.

## Reliability

### R1 — WebView2 `NavigateToString` byte-vs-character limit
- **Class:** reliability / globalization (multi-byte encoding).
- **Symptom:** preview crash (`ArgumentException`) on ~2 MB files with < 1.5 M characters (CJK).
- **Root cause:** guard used `string.Length` (UTF-16 units); the API limit is ~1.5 MB of UTF-8 bytes.
- **Guardrail:** `Encoding.UTF8.GetByteCount(html) > 1_500_000` → temp-file URI fallback; regression
  test for multi-byte content.
- **Evidence:** [PR #47391](https://github.com/microsoft/PowerToys/pull/47391); mirrored guard in
  `SvgThumbnailProvider.cs::GetThumbnailImpl`.

### R2 — PDF memory bomb
- **Class:** reliability / resource exhaustion.
- **Symptom:** crafted PDF drives excessive memory in the thumbnail process.
- **Guardrail:** reject `cx == 0 || cx > MaxThumbnailSize (10000)`; render only page 0 with bounded
  `DestinationHeight`.
- **Evidence:** issue [#42732](https://github.com/microsoft/PowerToys/issues/42732);
  `PdfThumbnailProvider.cs::DoGetThumbnail`/`PageToImage`.

### R3 — SVG thumbnail high power draw
- **Class:** performance / scalability.
- **Symptom:** `PowerToys.SvgThumbnailProvider` continuously high power/CPU.
- **Root cause:** per-thumbnail WebView2 environment creation + `Application.DoEvents()` spin-wait.
- **Guardrail:** don't add work to the pump loop; be conservative with per-thumbnail WebView2 setup.
- **Evidence:** issue [#46386](https://github.com/microsoft/PowerToys/issues/46386).

## Security

### S1 — Arbitrary URI-scheme execution from Markdown preview
- **Class:** security.
- **Symptom:** clicking a preview link launches non-web protocol handlers (`calculator:`, `search-ms:`).
- **Guardrail:** cancel user-initiated navigation, allow only `Uri.UriSchemeHttp`/`UriSchemeHttps`.
- **Evidence:** [PR #45801](https://github.com/microsoft/PowerToys/pull/45801);
  `MarkdownPreviewHandlerControl.cs` `NavigationStarting`.

### S2 — WebView2 sandbox hardening (untrusted file rendering)
- **Class:** security.
- **Guardrail:** disable script, web messages, host objects, autofill, DevTools, default dialogs and
  context menus; `AddWebResourceRequestedFilter("*")` + `WebResourceRequested` returning 403 for any
  URI ≠ local file; `--block-new-web-contents`; host mapping `...HostResourceAccessKind.Deny`.
- **Evidence:** in-source setup in `SvgPreviewControl.cs`, `SvgThumbnailProvider.cs`,
  `MarkdownPreviewHandlerControl.cs`; SVG `<script>` blocked via `SvgPreviewHandlerHelper.CheckBlockedElements`.

## Format correctness

### F1 — SVG namespace reorder renders as text
- **Symptom:** Inkscape v1.1 SVGs (default namespace before svg namespace) preview as raw text.
- **Guardrail:** keep `SvgPreviewHandlerHelper.SwapNamespaces` in the render pipeline.
- **Evidence:** in-source `Fixes #17527` comment.

### F2 — SVG containing `{`/`}` breaks HTML wrapping
- **Symptom:** SVG thumbnail/preview fails when the file contains a brace character.
- **Root cause:** raw SVG passed to `string.Format` — `{`/`}` misparsed as format placeholders.
- **Guardrail:** escape braces or avoid `string.Format` for untrusted payloads.
- **Evidence:** issue [#43059](https://github.com/microsoft/PowerToys/issues/43059).

### F3 — Scrollbar in captured SVG thumbnail
- **Symptom:** stray scrollbar in the generated thumbnail image.
- **Guardrail:** `document.querySelector('body').style.overflow='hidden'` before capture.
- **Evidence:** in-source `fixes #18286` comment in `SvgThumbnailProvider.cs`.

## Registration / configuration

### C1 — Quartet drift when adding/altering a format
- **Symptom:** format not registered / not GPO-gated after change.
- **Guardrail:** keep `settingName` + GPO rule + registry change-set (`powerpreview.cpp`) + CLSID pair
  (`CLSID.h`) + Settings toggle consistent; re-check GPO at render time to match registration.
- **Evidence:** structure of `PowerPreviewModule` ctor and `CLSID.h`.

## Cross-cutting build/infra (touch previewpane files incidentally)

- **B1 — PowerShell resx→rc build step reliability:** quote `$(MSBuildThisFileDirectory)`; disable
  PowerShell module auto-loading instead of `$WarningPreference='SilentlyContinue'` (which hides
  `convert-resx-to-rc.ps1` warnings). [PR #46729](https://github.com/microsoft/PowerToys/pull/46729).
- **B2 — Project path hygiene:** use `$(RepoRoot)`; avoid `$(ProjectDir)$(RepoRoot)` concatenation
  and extra `..\` segments. [PR #44639](https://github.com/microsoft/PowerToys/pull/44639).
- **B3 — MTP migration:** preview UnitTests migrated to Microsoft.Testing.Platform; MSTest projects
  build as executables. [PR #37651](https://github.com/microsoft/PowerToys/pull/37651).

## Excluded as noise (not distilled)
Spell-check/`check-spelling` allowlist churn (#47119), MSTEST0017 assertion-order fixes (#46712),
`.NET 10`/CppWinRT/VS2026 mechanical bumps (#41280, #45420, #44304) except where they gate coroutine
ABI, generic `async void`/singleton-thread-safety comments on shared Settings code (#44064) that
don't touch a preview/thumbnail render path, and one-off duplicate/author-feedback issues.
