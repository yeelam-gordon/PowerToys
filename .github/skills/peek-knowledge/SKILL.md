---
name: peek-knowledge
description: 'PowerToys Peek module knowledge: feature->file/function map, recurring regression playbooks (Space-to-activate firing during Explorer inline rename / IME composition, Ctrl+W & arrow shortcuts lost after focus enters WebView2/shell preview, preview-handler COM factory RCW leaks & fail-fast on teardown, media player / SMTC persisting after close, zip filename encoding garble, NumberOfDigits int.MinValue overflow, settings read only at Initialize), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/peek — quick file preview, previewers per type (image/video/audio/web-Monaco/archive/shell-handler/drive/special-folder), activation hotkey & Space mode, Explorer selection & neighboring-item navigation, delete, settings, GPO. Keywords: Peek, file preview, spacebar activation, rename conflict, IME, WebView2 Monaco, IPreviewHandler, SHCreateItemFromParsingName, LockServer, MediaSource, SMTC, CP437 zip encoding, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys Peek Knowledge

Grounded engineering knowledge for the PowerToys **Peek** module — a quick file previewer. On the
activation shortcut (default single **Space**) while a file is selected in File Explorer or on the
Desktop, Peek opens a lightweight window that renders the selected item via a type-specific
previewer, lets the user arrow between the neighboring/selected items, and closes on Ctrl+W/Esc.
Use this to localize code fast, avoid known regression traps, and enforce the conventions
maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/peek/` and needing prior art.
- Fixing/triaging a Peek bug: Space opens Peek while renaming a file/folder in Explorer or while
  typing in the search box; Space interrupts a Chinese/IME candidate selection; Ctrl+W / Esc /
  arrows stop working after clicking into the preview; a preview type renders blank or crashes;
  media keeps playing / media controls linger after Peek closes; zipped filenames show garbled
  (non-UTF-8) names; unhandled crash on activation; nothing happens in Home/Recent virtual folders.
- Reviewing a Peek PR against maintainer conventions and regression traps.
- Adding a new previewer type, touching the activation/hotkey plumbing, the Explorer selection
  query, the preview-handler COM lifecycle, or Peek settings.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring below).

| Sub-feature | Implementation (file · function) |
|---|---|
| Runner-side module, settings parse, hotkey, GPO gate, launch/terminate | `peek/dllmain.cpp` `PeekModule` `init_settings`/`parse_hotkey`/`get_hotkeys`/`on_hotkey`, `gpo_policy_enabled_configuration` → `powertoys_gpo::getConfiguredPeekEnabledValue` |
| **Space-to-activate** single-key mode (forces bare Space, stores prev combo; first-run default ON) | `peek/dllmain.cpp` `m_enableSpaceToActivate`, `JSON_KEY_ENABLE_SPACE_TO_ACTIVATE`, foreground hook `g_foregroundHook` (lines ~130-190, 249, 288, 628) |
| App bootstrap, single-instance, GPO check, Explorer-vs-CLI(`-FilePath`) branch, read foreground HWND pre-activation | `Peek.UI/PeekXAML/App.xaml.cs` `OnLaunched` (reads `GetForegroundWindow()` before activating to avoid focus-steal; `Environment.Exit(0)` guards) |
| Main window: show/hide, Initialize/Uninitialize, XAML `KeyboardAccelerator`s (Ctrl+W close, arrows navigate, Esc), Delete key, window sizing/centering | `Peek.UI/PeekXAML/MainWindow.xaml.cs` `Initialize`, `Uninitialize`, `Content_KeyUp`, `PreviousNavigationInvoked`/`NextNavigationInvoked`, `CloseInvoked`, `FilePreviewer_PreviewSizeChanged` |
| **Empty/suppressed-selection silent return** (activation must not steal focus while user is typing/renaming) | `Peek.UI/PeekXAML/MainWindow.xaml.cs` `Initialize` — the `if (ViewModel.CurrentItem == null) return;` guard; must **not** `ShowError`/`Show`/`BringToForeground` (that focus-stealing error window was the #44703 regression, reverted by [PR #44995](https://github.com/microsoft/PowerToys/pull/44995)) |
| Best-effort teardown (each step isolated so factory release always runs) + exit-after-close | `MainWindow.xaml.cs` `TryRunUninitializeStep`, `_exitAfterClose` → `Environment.Exit(0)` in `finally` |
| Closing handler that must never fail-fast the process | `MainWindow.xaml.cs` `AppWindow_Closing` (WinRT event exceptions marshal as failed HRESULT → CsWinRT fail-fast; wrapped in try/catch) |
| View model: neighboring items, navigation, current item, delete, index math | `Peek.UI/MainWindowViewModel.cs` `InitializeFromExplorer`, `Navigate` (`MathHelper.Modulo`), `CurrentItem`, `DeleteItem`, `_deletedItemIndexes` |
| **Explorer selection query** + typing-suppression heuristic | `Peek.UI/Helpers/FileExplorerHelper.cs` `GetSelectedItems`/`GetItems`, **`CaretVisible`** (suppress when focused control class contains `Edit`/`Input`, else `GUI_CARETBLINKING`) |
| Neighboring items (selected vs whole folder) | `Peek.UI/Services/NeighboringItemsQuery.cs` `GetNeighboringItems`; `Peek.UI/Models/NeighboringItems.cs`, `NeighboringItemsEnumerator.cs` |
| Selection source models | `Peek.UI/Models/SelectedItemByWindowHandle.cs`, `SelectedItemByPath.cs`, `SelectedItem.cs` |
| **Previewer dispatch (type → previewer, ordered)** | `Peek.FilePreviewer/Previewers/PreviewerFactory.cs` `Create` (Image→Video→Audio→WebBrowser→Archive→ShellPreviewHandler→Drive→SpecialFolder→Unsupported) |
| Image previewer (thumbnail + full, WIC) | `Previewers/MediaPreviewer/ImagePreviewer.cs`; `Previewers/Helpers/BitmapHelper.cs`, `ThumbnailHelper.cs`; `Peek.Common/WIC/*` |
| Video / Audio previewer (WinUI `MediaSource`/`MediaPlayerElement`) | `Previewers/MediaPreviewer/VideoPreviewer.cs`, `AudioPreviewer.cs` (`_mediaSource?.Dispose()`, `OnPreviewChanged`) |
| Web/text/code/markdown/html previewer (WebView2 + Monaco) | `Previewers/WebBrowserPreviewer/WebBrowserPreviewer.cs`; `Helpers/MonacoHelper.cs`, `MarkdownHelper.cs`, `ReadHelper.cs`; `Controls/BrowserControl.xaml.cs` |
| Archive/zip previewer + **filename encoding detection** | `Previewers/Archives/ArchivePreviewer.cs` (CP437 "probe" + `UtfUnknown` `CharsetDetector`); `Controls/ArchiveControl.xaml.cs` |
| **Shell IPreviewHandler previewer** (cached COM factories) | `Previewers/ShellPreviewHandlerPreviewer/ShellPreviewHandlerPreviewer.cs` `LoadPreviewAsync` (`IInitializeWithStream/Item/File`, `SHCreateItemFromParsingName`, `ownsHandler`), `Clear`, **`ReleaseHandlerFactories`** (drains `HandlerFactories` via `TryRemove` + `LockServer(false)` + `FinalReleaseComObject`) |
| Drive / special-folder previewers | `Previewers/Drive/DrivePreviewer.cs`, `Previewers/SpecialFolderPreviewer/SpecialFolderPreviewer.cs` |
| Unsupported fallback (+ FileNotSupported telemetry) | `Previewers/UnsupportedFilePreviewer/UnsupportedFilePreviewer.cs`; `CreateDefaultPreviewer` |
| Preview host control, size changes, metadata tooltip | `Peek.FilePreviewer/FilePreview.xaml(.cs)` `UpdateTooltipAsync`, `InfoTooltip`, `IsUnsupportedPreviewVisible` |
| File size / readable-string formatting | `Peek.Common/Helpers/ReadableStringHelper.cs`; `Peek.Common/Helpers/MathHelper.cs` `NumberOfDigits` (**`Math.Abs`**), `Modulo` |
| Settings (Space toggle, AlwaysOnTop, ShowInTaskbar, ConfirmFileDelete, CloseAfterLosingFocus, ShowFilePreviewTooltip), file watcher | `Peek.UI/Services/UserSettings.cs`, `IUserSettings.cs`; UI VM `src/settings-ui/.../ViewModels/PeekViewModel.cs` |
| UI tests | `Peek.UITests/PeekFilePreviewTests.cs`, `tests-checklist-template-peek.md` |

**Two truths to hold onto.** (1) Peek reads settings **once, at `Initialize()` time** (i.e. per
activation) — this is the established pattern (`CloseAfterLosingFocus`, `ConfirmFileDelete`, tooltip);
changing a setting while a Peek window is open is *intended* not to take effect until the next
activation. (2) Peek does **not** own a persistent process model for its window: `App.xaml.cs`
distinguishes an Explorer launch (foreground HWND drives selection) from a CLI `-FilePath` launch
(no neighbor navigation yet — `TODO`), and teardown may `Environment.Exit(0)`.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md).

### Space activation fires while renaming / typing in Explorer
- **Symptom:** the single-**Space** default opens Peek (or pops an error window) while the user is
  renaming a file/folder, or typing in the Explorer search / address box; the rename/search loses
  focus and is cancelled.
- **Where (culprit fixed by [PR #44995](https://github.com/microsoft/PowerToys/pull/44995)):**
  `Peek.UI/PeekXAML/MainWindow.xaml.cs::Initialize(SelectedItem)`, the `if (ViewModel.CurrentItem
  == null)` branch. Upstream, `Peek.UI/Helpers/FileExplorerHelper.cs::CaretVisible` →
  `GetSelectedItems` already returns `null` while the user is typing (focused control class contains
  `Edit`/`Input`, else `GUI_CARETBLINKING`), so `ViewModel.Initialize` leaves `CurrentItem == null`.
- **Root cause:** [PR #44703](https://github.com/microsoft/PowerToys/pull/44703) added an *error
  window* for empty virtual folders (Home/Recent): when `CurrentItem == null` it called
  `ViewModel.ShowError(...)`, `this.Show()`, and `WindowHelpers.BringToForeground(...)`. Because
  the typing-suppression also produces `CurrentItem == null`, that error path fired during rename /
  search too, stealing focus and cancelling the edit — defeating `CaretVisible`'s suppression.
- **Guardrail:** when `Initialize` finds `CurrentItem == null` it must **return silently** (no
  `Show`, no `BringToForeground`) — never surface UI on an empty/suppressed selection. Also keep
  `CaretVisible` suppression correct: the class-name match is deliberately broad (any `Edit`/`Input`
  focus in the same top-level window) and **fails open** (allows activation) only on
  `GetGUIThreadInfo` failure. Evidence: issues [#45133](https://github.com/microsoft/PowerToys/issues/45133),
  [#45137](https://github.com/microsoft/PowerToys/issues/45137),
  [#45383](https://github.com/microsoft/PowerToys/issues/45383),
  [#45642](https://github.com/microsoft/PowerToys/issues/45642),
  [#45667](https://github.com/microsoft/PowerToys/issues/45667),
  [#45886](https://github.com/microsoft/PowerToys/issues/45886); fix
  [PR #44995](https://github.com/microsoft/PowerToys/pull/44995). Disable/re-enable report #49013
  remains a separate lifecycle/focus-eligibility investigation without an established cause.

### Space interrupts IME (Chinese/Japanese) candidate selection
- **Symptom:** with the Space activation shortcut and an East-Asian IME, pressing Space to pick a
  candidate word during rename instead triggers Peek and interrupts composition.
- **Where:** same activation path (`dllmain.cpp` Space hook) + `CaretVisible`; the IME candidate
  window is a separate focus/class from the rename `Edit` control.
- **Root cause:** the suppression heuristic keys off the focused control class; an active IME
  composition is not the same signal, so Space can leak to activation mid-composition.
- **Guardrail:** treat IME composition as a first-class "user is typing" state, not just Edit/Input
  focus; verify with a CJK IME during inline rename before changing activation gating. Evidence:
  [#45346](https://github.com/microsoft/PowerToys/issues/45346),
  [#48189](https://github.com/microsoft/PowerToys/issues/48189).

### Ctrl+W / Esc / arrows dead after clicking into the preview
- **Symptom:** shortcuts work initially, but after the user clicks inside the preview (WebView2/Monaco
  content or a shell preview-handler surface) Ctrl+W (and arrows/Esc) no longer close/navigate Peek.
- **Where:** Peek's shortcuts are XAML `KeyboardAccelerator`s on the window content
  (`MainWindow.xaml`/`.cs` `CloseInvoked`, `*NavigationInvoked`). Once focus moves into the hosted
  WebView2 or the out-of-process preview-handler window, keystrokes route there and bypass the parent
  XAML accelerators.
- **Root cause:** hosted content (WebView2, shell preview host process) captures keyboard focus;
  XAML `KeyboardAccelerator`s only fire for the WinUI focus tree.
- **Guardrail:** restoring the shortcuts requires a mechanism above the XAML focus tree (e.g. a
  low-level keyboard hook while Peek is foreground) — and such a hook must follow repo hook
  conventions (see Review Rules). Evidence: issue
  [#48274](https://github.com/microsoft/PowerToys/issues/48274); merged fix
  [PR #48293](https://github.com/microsoft/PowerToys/pull/48293).

### Preview-handler COM factory leak / fail-fast on teardown
- **Symptom:** shell preview-handler files (e.g. Office/PDF via `IPreviewHandler`) leak COM objects,
  keep the selected file locked, keep a local-server host process alive, or crash Peek on close.
- **Where:** `ShellPreviewHandlerPreviewer.cs` — `LoadPreviewAsync` (`ownsHandler` try/finally
  releases the RCW + closes `fileStream` when init fails/cancels; `SHCreateItemFromParsingName`
  `IShellItem` must be released after `IInitializeWithItem.Initialize`), `Clear` (split `Unload()`
  and `FinalReleaseComObject` into separate try blocks), and `ReleaseHandlerFactories` (drain the
  `HandlerFactories` `ConcurrentDictionary` via `TryRemove`, then `LockServer(false)` +
  `FinalReleaseComObject`). Teardown is invoked from `MainWindow.Uninitialize` via `TryRunUninitializeStep`.
- **Root cause:** cached factories are `LockServer(true)`-locked local servers; snapshot-then-`Clear`
  raced a concurrent `LoadPreviewAsync` `AddOrUpdate`, dropping an entry without its matching
  `LockServer(false)`/`FinalRelease`. A single teardown exception could also skip later cleanup, and
  a throw inside a WinRT `Closing` callback fail-fasts the whole process.
- **Guardrail:** every acquired COM object released in `try/finally`; drain the dictionary with
  `TryRemove` (never snapshot+Clear); mirror `LockServer(true)` with `LockServer(false)`; keep each
  teardown step isolated so `ReleaseHandlerFactories()` always runs; never let `AppWindow_Closing`
  throw. Evidence: [PR #48564](https://github.com/microsoft/PowerToys/pull/48564).

### Media keeps playing / SMTC lingers after Peek closes
- **Symptom:** after closing Peek, audio/video keeps playing or Peek keeps appearing as an active
  media session (System Media Transport Controls) in the OS.
- **Where:** `MediaPreviewer/VideoPreviewer.cs`, `AudioPreviewer.cs` — `MediaSource` lifetime,
  `Dispose`/`Unload`, `OnPreviewChanged`; disposal is driven off previewer change.
- **Root cause:** the `MediaSource`/`MediaPlayerElement` (which registers SMTC) was not disposed when
  the previewer changed or Peek closed, so the player and its media session outlived the window.
- **Guardrail:** dispose the media source deterministically when the previewer changes or on
  teardown; don't rely on GC. Evidence:
  [PR #46899](https://github.com/microsoft/PowerToys/pull/46899).

### Zip filenames garbled for non-UTF-8 archives
- **Symptom:** previewing a zip created on a non-UTF-8 OS (e.g. Chinese filenames) shows garbled text
  entry names.
- **Where:** `ArchivePreviewer.cs` `.zip` branch.
- **Root cause:** zip stores no encoding unless the UTF-8 (EFS bit 11) flag is set; SharpCompress then
  falls back to a caller-provided encoding. Peek forces **CP437** as a reversible "probe": if a strict
  CP437 round-trip of the entry keys succeeds, SharpCompress did *not* detect UTF-8, so the real
  encoding is unknown and is detected with `UtfUnknown` `CharsetDetector`; entry keys are re-decoded
  from the raw CP437 bytes using the detected charset. If the strict round-trip throws
  (`EncoderFallbackException`), SharpCompress already resolved UTF-8 — use its keys as-is.
- **Guardrail:** preserve the CP437-probe + detect + re-decode pipeline; do **not** blindly fall back
  to UTF-8 when detection is uncertain (that is guaranteed wrong for non-ASCII CP437 names). Evidence:
  issue [#44790](https://github.com/microsoft/PowerToys/issues/44790); fix
  [PR #44799](https://github.com/microsoft/PowerToys/pull/44799).

### `NumberOfDigits(int.MinValue)` OverflowException
- **Symptom:** unhandled `OverflowException` from the readable-size/formatting path.
- **Where:** `Peek.Common/Helpers/MathHelper.cs::NumberOfDigits` uses `Math.Abs(num)`; called from
  `ReadableStringHelper.GetPrecision` with `(int)number`.
- **Root cause:** `Math.Abs(int.MinValue)` throws — `-2147483648` has no positive `int` counterpart.
- **Guardrail:** guard/compute digit count without `Math.Abs(int.MinValue)` (e.g. use `long`/`uint`
  or special-case `int.MinValue`); validate size inputs before casting `double`→`int`. Evidence:
  issue [#46960](https://github.com/microsoft/PowerToys/issues/46960).

## Review Rules

Enforce these when reviewing or authoring Peek changes:

- **Keep `CaretVisible` suppression intact and fail-open.** Any activation/hotkey change must not let
  Space leak into an Explorer rename/search box; verify Edit/Input class match and IME composition
  (#45133, #45346).
- **Read settings at `Initialize()`, consistent with existing Peek settings.** Don't add live
  settings propagation to an open window unless deliberately changing the architecture — maintainers
  reject it as inconsistent with `CloseAfterLosingFocus`/`ConfirmFileDelete`
  ([PR #46624](https://github.com/microsoft/PowerToys/pull/46624)).
- **Don't raise the settings `Changed` event while holding `_settingsLock`.** Capture the delegate
  under the lock, invoke after releasing — raising under the lock risks reentrancy/deadlock
  ([PR #44645](https://github.com/microsoft/PowerToys/pull/44645), `UserSettings.cs`).
- **Release every COM/RCW in `try/finally`; drain caches without races.** Preview-handler factories
  must mirror `LockServer(true)`/`LockServer(false)` and use `TryRemove`, not snapshot+`Clear`; the
  `IShellItem` from `SHCreateItemFromParsingName` must be released after `Initialize`
  ([PR #48564](https://github.com/microsoft/PowerToys/pull/48564)).
- **Never let a WinRT callback throw.** `AppWindow_Closing` and each `Uninitialize` step run through
  try/catch — a throw in a CsWinRT event marshals as a failed HRESULT and **fail-fasts** the process
  (`MainWindow.xaml.cs`; PR #48564).
- **Dispose `MediaSource` deterministically.** Release media (and its SMTC session) on previewer
  change / teardown, not via GC (PR #46899).
- **Follow repo global-hook conventions if you add a low-level keyboard hook.** Pass a real module
  handle to `SetWindowsHookEx` (not `IntPtr.Zero`) and validate the return; log
  `Marshal.GetLastWin32Error()` on failure; make handler subscriptions idempotent (remove before add);
  marshal `KBDLLHOOKSTRUCT`, then filter `vkCode` as current source does; include the **Win** key as a
  modifier so Win+Arrow snapping is not swallowed; only consume a key when `DispatcherQueue.TryEnqueue`
  succeeds — mirror `cmdpal`/`ColorPicker` hook patterns
  ([PR #48293](https://github.com/microsoft/PowerToys/pull/48293) review;
  [low-level keyboard hooks](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc)).
- **Respect nullable-enabled + warnings-as-errors.** Keep `string?`/null annotations consistent across
  DependencyProperty CLR wrappers and bindings (PR #46624).
- **Preview-type gating lives in each previewer's `IsItemSupported`; keep `PreviewerFactory.Create`
  ordering deliberate.** Image precedes Video/Audio precedes WebBrowser precedes Archive precedes the
  shell handler precedes Drive/SpecialFolder, then the Unsupported fallback (which logs
  `FailureType.FileNotSupported`).
- **Ship/consider a test.** Peek has a UITests project (`Peek.UITests`) and a manual checklist
  (`tests-checklist-template-peek.md`) — reviewers repeatedly ask for coverage on behavior changes.
- **No bare relative paths in project files.** Use `$(RepoRoot)`, not `..\..\..\`
  ([PR #44639](https://github.com/microsoft/PowerToys/pull/44639)).

## Pitfalls

- **The default activation is a bare Space** (`EnableSpaceToActivate`, first-run default ON in
  `dllmain.cpp`). It is the single most common source of "Peek opens while I'm renaming" reports;
  the only thing standing between Space and a rename is `FileExplorerHelper.CaretVisible` (#45133,
  #45642). Do not fold unresolved disable/re-enable report #49013 into this established cause.
- **`CaretVisible` fails open.** If `GetGUIThreadInfo` fails it returns `false` (allows activation) on
  purpose — don't "fix" that into fail-closed without understanding it blocks transient-failure
  lockout.
- **Settings changes don't apply to an already-open Peek window** — values are copied in
  `Initialize()`. This is by design; a "stale tooltip/setting" report is usually not a bug (PR #46624).
- **A throw inside `AppWindow_Closing` (or any WinRT event callback) fail-fasts Peek** — CsWinRT turns
  a managed exception in the dispatcher into a failed HRESULT that CFlat treats as fatal. Keep those
  handlers catch-all (PR #48564).
- **Cached preview-handler factories are `LockServer`-locked local servers.** Dropping one without
  `LockServer(false)`+`FinalReleaseComObject` leaks a locked out-of-process host; always drain via
  `TryRemove` (PR #48564).
- **Hosted preview surfaces (WebView2/Monaco, shell preview host) steal keyboard focus**, so XAML
  `KeyboardAccelerator`s (Ctrl+W/arrows/Esc) silently stop working after a click into the preview
  (#48274).
- **`Math.Abs(int.MinValue)` throws** — the size-formatting path hit this (#46960); never assume
  `Math.Abs` is total over `int`.
- **Zip has no guaranteed filename encoding.** The CP437 "probe" is intentional; UTF-8 is only correct
  when SharpCompress sets the EFS flag — don't shortcut to UTF-8 (PR #44799).
- **CLI (`-FilePath`) launches have no neighbor list** — navigation is a `TODO`; arrow handling must
  tolerate a null/absent `Items` list (`MainWindowViewModel`).

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression list + notes.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a Peek PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/peek/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/peek)
- [IPreviewHandler](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ipreviewhandler) ·
  [Low-level keyboard hook](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc) ·
  [System Media Transport Controls](https://learn.microsoft.com/en-us/windows/uwp/audio-video-camera/system-media-transport-controls)
