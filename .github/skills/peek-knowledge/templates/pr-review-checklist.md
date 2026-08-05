# Peek — PR Review Checklist

Apply **after** reading the diff cold (see anti-anchoring in SKILL.md). Only check rows whose code
paths the diff actually touches.

## Activation / hotkey (`peek/dllmain.cpp`, `FileExplorerHelper.cs`)
- [ ] Space-to-activate change still suppressed during Explorer inline rename & search box via `CaretVisible` (Edit/Input class match) (#45133, #45642).
- [ ] IME composition still protected — Space doesn't interrupt CJK candidate selection (#45346, #48189).
- [ ] `CaretVisible` still **fails open** on `GetGUIThreadInfo` failure (returns false = allow).
- [ ] When no items found, `MainWindow.Initialize` returns silently without stealing focus.
- [ ] GPO gate (`getConfiguredPeekEnabledValue`) honored; `get_hotkeys`/`on_hotkey` consistent with `m_hotkey`.

## Previewers (`PreviewerFactory.cs` + `Previewers/*`)
- [ ] `PreviewerFactory.Create` ordering unchanged unless intended (Image→Video→Audio→WebBrowser→Archive→ShellPreviewHandler→Drive→SpecialFolder→Unsupported).
- [ ] New type gated by its own `IsItemSupported`; Unsupported fallback still logs `FailureType.FileNotSupported`.

## Shell preview-handler COM lifecycle (`ShellPreviewHandlerPreviewer.cs`)
- [ ] Every acquired RCW released in `try/finally`; `ownsHandler` path releases handler + closes `fileStream` on cancel/init failure.
- [ ] `IShellItem` from `SHCreateItemFromParsingName` released after `IInitializeWithItem.Initialize`.
- [ ] `ReleaseHandlerFactories` drains `HandlerFactories` via `TryRemove` (not snapshot+Clear); mirrors `LockServer(true)` with `LockServer(false)` + `FinalReleaseComObject`.
- [ ] `Clear` keeps `Unload()` and `FinalReleaseComObject` in separate try blocks (PR #48564).

## Teardown / lifecycle (`MainWindow.xaml.cs`)
- [ ] Each `Uninitialize` step runs through `TryRunUninitializeStep` so `ReleaseHandlerFactories()` always runs.
- [ ] `AppWindow_Closing` cannot throw (WinRT callback throw fail-fasts the process).
- [ ] `_exitAfterClose`/CLI `-FilePath` exit-after-close contract preserved.

## Media (`VideoPreviewer.cs`, `AudioPreviewer.cs`)
- [ ] `MediaSource` disposed deterministically on previewer change / teardown (no lingering playback or SMTC session) (PR #46899).

## Archive encoding (`ArchivePreviewer.cs`)
- [ ] CP437-probe → `CharsetDetector` → re-decode pipeline intact; no blind UTF-8 fallback when detection uncertain (PR #44799).

## Settings (`UserSettings.cs`, `PeekViewModel.cs`)
- [ ] Settings still read at `Initialize()` (per-activation) — no unintended live propagation to open window (PR #46624).
- [ ] `Changed` event not raised while holding `_settingsLock` (capture delegate, invoke after release) (PR #44645).
- [ ] Nullable annotations consistent (nullable-enabled, warnings-as-errors).

## If adding a low-level keyboard hook (e.g. for Ctrl+W after WebView2 focus — #48274 / PR #48293)
- [ ] Real module handle to `SetWindowsHookEx` (not `IntPtr.Zero`); return validated; `Marshal.GetLastWin32Error()` logged on failure.
- [ ] Handler subscription idempotent (remove before add); hook structure marshalled safely before
      current-source `vkCode` filtering.
- [ ] Win key treated as modifier (don't swallow Win+Arrow snapping); Shift semantics match XAML accelerators.
- [ ] Key consumed only when `DispatcherQueue.TryEnqueue` succeeds; hook uninstalled in `Dispose`.

## Build hygiene / tests
- [ ] Project files use `$(RepoRoot)`, not bare `..\..\..\` (PR #44639).
- [ ] Behavior change covered by `Peek.UITests` where feasible (reviewers repeatedly ask).
