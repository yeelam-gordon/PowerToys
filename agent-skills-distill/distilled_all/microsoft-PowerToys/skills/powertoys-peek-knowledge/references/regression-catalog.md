# Peek — Regression Catalog

Fuller, progressively-disclosed list behind the SKILL.md playbooks. Every entry is grounded in the
module source under `src/modules/peek/` and/or the mined issue/PR history. Confirm in source before
acting — several mined issue bodies were sparse (non-English or terse), so entries marked
"grounding: title(s)+source" lean on issue titles plus verified source behavior.

## Activation: Space vs typing (the dominant issue cluster)

- **Space fires (or pops an error window) during Explorer inline rename / search box.** The
  first-run default is single-Space activation (`dllmain.cpp` `m_enableSpaceToActivate`,
  `JSON_KEY_ENABLE_SPACE_TO_ACTIVATE`). The upstream gate that keeps Space out of a text field is
  `FileExplorerHelper.CaretVisible`: it reads `GetGUIThreadInfo`, restricts to the same top-level
  window (`gi.hwndActive == hwnd`), treats any focused control whose class name contains `Edit` or
  `Input` as "typing" (returns true → `GetSelectedItems` returns `null`), and falls back to the
  `GUI_CARETBLINKING` flag. It **fails open** (returns false) when `GetGUIThreadInfo` fails. The
  actual bug fixed by [PR #44995](https://github.com/microsoft/PowerToys/pull/44995) was **not** in
  `CaretVisible` but in `MainWindow.xaml.cs::Initialize`: [PR #44703](https://github.com/microsoft/PowerToys/pull/44703)
  had added an error window in the `ViewModel.CurrentItem == null` branch (`ShowError` + `this.Show()`
  + `WindowHelpers.BringToForeground`) for empty virtual folders, and because the typing-suppression
  also yields `CurrentItem == null`, that window popped during rename/search and stole focus. #44995
  restores the silent `return`. Evidence: [#45133](https://github.com/microsoft/PowerToys/issues/45133),
  [#45137](https://github.com/microsoft/PowerToys/issues/45137),
  [#45145](https://github.com/microsoft/PowerToys/issues/45145),
  [#45383](https://github.com/microsoft/PowerToys/issues/45383),
  [#45642](https://github.com/microsoft/PowerToys/issues/45642),
  [#45667](https://github.com/microsoft/PowerToys/issues/45667),
  [#45886](https://github.com/microsoft/PowerToys/issues/45886) (search box); fix
  [PR #44995](https://github.com/microsoft/PowerToys/pull/44995). Grounding: source + issue titles.

- **Regression after toggling the Space switch off/on.** `init_settings` in `dllmain.cpp` forces a
  bare-Space hotkey when the toggle is ON and stores/reverts the previous combination; the
  off→on→off transitions have their own revert policy (lines ~140-190). A user reported Space
  re-triggering during rename after toggling the switch. Evidence:
  [#49013](https://github.com/microsoft/PowerToys/issues/49013). Grounding: source + title.

- **IME (CJK) candidate selection interrupted by Space.** With an East-Asian IME, Space commits a
  candidate; because the IME candidate UI is not the rename `Edit` control, the class-name heuristic
  can miss it and Space leaks to activation. Evidence:
  [#45346](https://github.com/microsoft/PowerToys/issues/45346),
  [#48189](https://github.com/microsoft/PowerToys/issues/48189). Grounding: titles + source.

- **Silent no-op when there's nothing to preview.** `MainWindow.xaml.cs::Initialize` early-returns
  (`if (ViewModel.CurrentItem == null) return;`) when selection is empty/suppressed (typing in
  rename/search, or virtual folders) specifically to avoid stealing focus. History: #44703 briefly
  replaced this silent return with a focus-stealing error window for empty virtual folders (Home/Recent);
  [PR #44995](https://github.com/microsoft/PowerToys/pull/44995) reverted it back to the silent return.
  Guardrail: never surface Peek UI (`Show`/`BringToForeground`/`ShowError`) on an empty selection.
  Grounding: source + PR diff.

## Keyboard shortcuts vs hosted content

- **Ctrl+W / Esc / arrows die after focus enters the preview.** Peek's shortcuts are XAML
  `KeyboardAccelerator`s (`MainWindow` `CloseInvoked`, `PreviousNavigationInvoked`,
  `NextNavigationInvoked`). Hosted WebView2/Monaco content and the out-of-process shell preview-handler
  window capture keyboard focus and bypass the parent WinUI focus tree. Evidence:
  [#48274](https://github.com/microsoft/PowerToys/issues/48274); in-progress fix via a low-level
  keyboard hook [PR #48293](https://github.com/microsoft/PowerToys/pull/48293). Grounding: source + issue/PR.

- **Global-hook conventions (from PR #48293 review).** If restoring shortcuts with a low-level hook:
  use a real module handle (not `IntPtr.Zero`) and validate it; log `Marshal.GetLastWin32Error()`;
  make the content `KeyUp` subscription idempotent (Peek re-`Initialize`s while visible when the
  selected file changes, so `this.Content.KeyUp += ...` can double-subscribe); fast-path filter
  `vkCode` before marshalling `KBDLLHOOKSTRUCT`; include LWin/RWin as modifiers (don't swallow
  Win+Arrow snapping); only mark a key handled when `DispatcherQueue.TryEnqueue` succeeds; uninstall
  in `Dispose`. Mirrors `cmdpal`/`ColorPicker`/`Mouse` hook patterns in-repo.
  ([lowlevelkeyboardproc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)).
  Grounding: review comments + repo patterns.

## Preview-handler COM lifecycle (PR #48564)

- **Factory cache is a locked local server.** `ShellPreviewHandlerPreviewer` caches `IClassFactory`
  RCWs in a static `HandlerFactories` `ConcurrentDictionary` and `LockServer(true)`s them.
  `ReleaseHandlerFactories` must drain via `TryRemove` (a prior snapshot+`Clear` raced a concurrent
  `LoadPreviewAsync` `AddOrUpdate`, dropping an entry without its `LockServer(false)`/`FinalRelease`
  → leaked locked host). Each drained factory gets `LockServer(false)` then `FinalReleaseComObject`,
  both wrapped (RCW may be unreachable during teardown).

- **Per-load cleanup.** `LoadPreviewAsync` uses an `ownsHandler` flag in `try/finally`: on
  cancellation or init failure before assigning `Preview`, it `FinalReleaseComObject`s the handler and
  disposes `fileStream`. The `IInitializeWithItem` path releases the `IShellItem` created by
  `SHCreateItemFromParsingName` after `Initialize`.

- **`Clear` splits `Unload()` and `FinalReleaseComObject`** into separate try blocks: if the
  preview-handler host process crashed mid-teardown and `Unload` throws, the RCW must still be
  released or the cache/process can't tear down cleanly.

- **Best-effort teardown + no fail-fast.** `MainWindow.Uninitialize` runs each step through
  `TryRunUninitializeStep` so a failure (e.g. `Restore`/`Hide` on a window mid-teardown) doesn't skip
  `ReleaseHandlerFactories()`; the `finally` preserves `_exitAfterClose` → `Environment.Exit(0)`.
  `AppWindow_Closing` is fully wrapped because a throw in a CsWinRT event callback marshals as a
  failed HRESULT and CFlat fail-fasts the process. Grounding: source + PR #48564 review.

## Media / SMTC (PR #46899)

- **Player/media session outlives the window.** `MediaSource` (backing `MediaPlayerElement`, which
  registers System Media Transport Controls) must be disposed when the previewer changes or Peek
  closes; `VideoPreviewer`/`AudioPreviewer` `Dispose`/`Unload` and `OnPreviewChanged` drive this. The
  original bug: audio kept playing / Peek persisted as a media player after close, plus a redundant
  SMTC registration. Grounding: source (`_mediaSource?.Dispose()`) + PR #46899 title/discussion.

## Archive encoding (PR #44799)

- **CP437 probe pipeline.** `ArchivePreviewer` opens `.zip` with `ArchiveEncoding.Forced = CP437`; a
  strict CP437 (`EncoderExceptionFallback`/`DecoderExceptionFallback`) round-trip of the joined entry
  keys distinguishes "SharpCompress used our CP437 fallback (encoding unknown → run `UtfUnknown`
  `CharsetDetector`, re-decode from raw CP437 bytes)" from "SharpCompress already resolved UTF-8 via
  EFS bit 11 (`EncoderFallbackException` thrown → use keys as-is)". Reviewer note: when the strict
  round-trip succeeds, UTF-8 is guaranteed wrong for non-ASCII names, so don't fall back to UTF-8 on
  low detection confidence. Evidence: [#44790](https://github.com/microsoft/PowerToys/issues/44790),
  [PR #44799](https://github.com/microsoft/PowerToys/pull/44799). Grounding: source + review.

## Formatting / math

- **`Math.Abs(int.MinValue)` overflow.** `MathHelper.NumberOfDigits(int)` calls `Math.Abs(num)`,
  which throws `OverflowException` for `int.MinValue`; reached via `ReadableStringHelper.GetPrecision`
  ((int)number cast). Evidence: [#46960](https://github.com/microsoft/PowerToys/issues/46960).
  Grounding: source + issue title.

## Metadata tooltip toggle (PR #46624)

- **Async re-attach race + read-at-Initialize.** `FilePreview.UpdateTooltipAsync` checks
  `ShowFilePreviewTooltip` at start; if toggled off during in-flight async work it could still assign
  a non-null `InfoTooltip` and re-attach the tooltip — a second check before the final assign fixes
  it. The setting is copied into `FilePreviewer` only in `MainWindow.Initialize`, so toggling while a
  Peek window is open is intentionally not live (maintainers rejected live propagation as inconsistent
  with other Peek settings). Grounding: source + PR #46624 review.

## Excluded as noise (not distilled)

- Repo-wide build/infra PRs that merely touched Peek among many modules: .NET 10 upgrade
  ([#41280](https://github.com/microsoft/PowerToys/pull/41280)), CppWinRT bump
  ([#45420](https://github.com/microsoft/PowerToys/pull/45420)), VS 2026 support
  ([#44304](https://github.com/microsoft/PowerToys/pull/44304)); the `$(RepoRoot)` path convention
  ([#44639](https://github.com/microsoft/PowerToys/pull/44639)) is captured as a one-line Review Rule.
- Pure style/formatting nits (e.g. SA1513 blank-line, spell-check `, otherwise`), "LGTM", and
  release-label/versioning chatter.
- Duplicate / non-actionable user reports ("Peek not working", "Peek issue") lacking a reproducible,
  generalizable lesson.
