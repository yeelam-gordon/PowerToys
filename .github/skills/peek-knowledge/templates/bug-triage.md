# Peek — Bug Triage

Map a symptom to the most likely file/function via the Module Map. Treat as a **hypothesis to
confirm in source**, not ground truth (a thin map row can anchor you onto a wrong file).

| Symptom | Start here | Notes |
|---|---|---|
| Space opens Peek while renaming a file/folder in Explorer | `FileExplorerHelper.CaretVisible`; `dllmain.cpp` Space-mode `on_hotkey` | Suppress when focused class contains `Edit`/`Input`. #45133, #45137, #45383, #45642, #45667 |
| Space in Explorer search box triggers Peek | `FileExplorerHelper.CaretVisible` | Search box is also an Edit/Input control. #45886 |
| Space interrupts Chinese/Japanese IME candidate selection | `FileExplorerHelper.CaretVisible`; activation path | IME composition ≠ Edit focus signal. #45346, #48189 |
| Regresses after disabling and re-enabling Peek | module `enable`/`disable`, hook installation, and `FileExplorerHelper.CaretVisible` eligibility | Correlation only; investigate lifecycle and focus eligibility without assuming hotkey-state restoration. #49013 |
| Ctrl+W / Esc / arrows stop working after clicking into preview | `MainWindow.xaml(.cs)` `KeyboardAccelerator`s; hosted WebView2/shell preview focus | XAML accelerators bypassed by hosted content. #48274, PR #48293 |
| Preview-handler file leaks / stays locked / host process lingers / crash on close | `ShellPreviewHandlerPreviewer.cs` `LoadPreviewAsync`/`Clear`/`ReleaseHandlerFactories` | RCW/`LockServer` lifecycle; `TryRemove` drain. PR #48564 |
| Peek crashes on close (fail-fast) | `MainWindow.xaml.cs` `AppWindow_Closing`, `TryRunUninitializeStep` | WinRT callback throw → CsWinRT fail-fast. PR #48564 |
| Media keeps playing / Peek stays an active media session after close | `VideoPreviewer.cs` / `AudioPreviewer.cs` `Dispose`/`OnPreviewChanged`, `_mediaSource` | Dispose `MediaSource` (drops SMTC). PR #46899 |
| Zipped filenames garbled (non-UTF-8 / Chinese) | `ArchivePreviewer.cs` `.zip` branch | CP437 probe + `CharsetDetector` re-decode. #44790, PR #44799 |
| Unhandled `OverflowException` in size/format | `MathHelper.NumberOfDigits` (`Math.Abs`), `ReadableStringHelper.GetPrecision` | `Math.Abs(int.MinValue)` throws. #46960 |
| Metadata tooltip shows/hides at wrong time or empty popup | `FilePreview.xaml(.cs)` `UpdateTooltipAsync`, `InfoTooltip`, `ShowFilePreviewTooltip` | Setting read at Initialize; second check before assign. PR #46624 |
| Setting toggled while Peek open has no effect | `MainWindow.Initialize` (settings copied here); `UserSettings.cs` | By design — applies next activation. PR #46624 |
| AlwaysOnTop / ShowInTaskbar behavior wrong | `MainWindow.xaml.cs` window setup; `UserSettings.cs`; `PeekViewModel.cs` | Options added in PR #44645. |
| Nothing happens in Home/Recent (virtual folders) | `MainWindowViewModel.InitializeFromExplorer`; `MainWindow.Initialize` early-return | No shell items retrievable; silent/error. PR #44703 |
| Explorer selection highlight / "show in folder" broken | `FileExplorerHelper` selection query; `NeighboringItemsQuery` | Selection integration interference. #47618 |
| Wrong item shown / arrow navigation off-by-one or wraps wrong | `MainWindowViewModel.Navigate` (`MathHelper.Modulo`), `_deletedItemIndexes` | Index math with deletions. |
| Nothing opens from CLI `-FilePath`, arrows do nothing | `App.xaml.cs` CLI branch; `MainWindowViewModel` (`Items == null`) | CLI has no neighbor list (TODO). |
| Unsupported file view for a type that should preview | `PreviewerFactory.Create` ordering; the type's `IsItemSupported` | Fallback logs `FailureType.FileNotSupported`. |

## Triage steps
1. Reproduce and note: launched via Explorer vs Desktop vs CLI `-FilePath`; activation = Space vs custom hotkey; file type; single vs multi-selection; IME state.
2. Check Peek logs (ManagedCommon `Logger`) for the failing step name (e.g. `Uninitialize step '...'`).
3. Localize with the table above, then **confirm in source** before concluding — several symptom
   clusters (Space/rename, IME) share the same two files.
