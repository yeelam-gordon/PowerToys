# AlwaysOnTop Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each claim
in source before acting. Symptoms map to `src/modules/alwaysontop/`.

## Key Decisions (context for the playbooks)

- **Extend the system menu, don't build a title-bar button.** The "Always on top" toggle is added to
  the window **system menu** (Dexpot-style) rather than injecting a real title-bar button, because a
  true button on third-party windows needs non-client-area hooking / cross-process manipulation —
  higher complexity and compatibility risk. Feature landed in
  [PR #45773](https://github.com/microsoft/PowerToys/pull/45773); it remains **opt-in (default
  `showInSystemMenu = false`)** and is the root of the "breaks other apps' menu" bug class.
- **Immutable settings snapshot behind an atomic.** `AlwaysOnTopSettings::settings()` returns
  `std::shared_ptr<const Settings>` loaded from `std::atomic<...>` (`Settings.h`). The file-watcher
  builds a new `Settings`, then `m_settings.store(...)` publishes it and notifies observers
  (`Settings.cpp`). A full strong-consistency fix would need synchronous IPC acks; the snapshot is a
  deliberate simplicity trade-off. [PR #45994](https://github.com/microsoft/PowerToys/pull/45994).
- **Owner-tagged system-menu item for safe dedup.** Inserted menu items carry
  `dwItemData = 0x414F5450` ("AOTP"); `IsAlwaysOnTopMenuCommand` checks that tag before update/remove
  so AlwaysOnTop never edits a same-id item it doesn't own.
  [PR #45845](https://github.com/microsoft/PowerToys/pull/45845).
- **Independently configurable opacity hotkeys.** Increase/decrease-opacity shortcuts were split out
  from the pin hotkey so users on conflicting layouts can rebind them.
  [PR #46410](https://github.com/microsoft/PowerToys/pull/46410).
- **Sound is success feedback.** Pin/unpin sound plays only when the state actually changed; playing
  on failure was removed so the sound doesn't imply success when a window couldn't be pinned.
  [PR #46910](https://github.com/microsoft/PowerToys/pull/46910). (Open UX question: still no
  feedback when sound is disabled.)
- **Border is a per-window layered tool window.** Each pinned window on the current virtual desktop
  gets a `WindowBorder` (layered, topmost, `WS_EX_TOOLWINDOW`) that redraws via Direct2D and refreshes
  on a 100 ms `WM_TIMER`, re-reading `DWMWA_EXTENDED_FRAME_BOUNDS`.

## Regression Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| System-menu integration | Custom-titlebar apps lose/glitch title-bar menu; `TrackPopupMenu` 1401 | `AlwaysOnTop.cpp` `UpdateSystemMenuItem`, `HandleWinHookEvent`, `SubscribeToEvents` | Mutating foreign windows' system menu + system-wide `EVENT_OBJECT_INVOKED` hook | Keep opt-in; hook only when enabled; verify ownership | [#46483](https://github.com/microsoft/PowerToys/issues/46483), [#46569](https://github.com/microsoft/PowerToys/issues/46569), [#46804](https://github.com/microsoft/PowerToys/issues/46804), [#46808](https://github.com/microsoft/PowerToys/issues/46808), [#47058](https://github.com/microsoft/PowerToys/issues/47058), [#47247](https://github.com/microsoft/PowerToys/issues/47247), [#47917](https://github.com/microsoft/PowerToys/issues/47917), [#48006](https://github.com/microsoft/PowerToys/issues/48006), [PR #45773](https://github.com/microsoft/PowerToys/pull/45773) |
| Command-ID collision | Toggle duplicated / clobbers another item | `UpdateSystemMenuItem`, `IsAlwaysOnTopMenuCommand` | Fixed `0xEFE0` id reused without ownership check | Owner tag `0x414F5450`; skip+log if id not ours | [PR #45845](https://github.com/microsoft/PowerToys/pull/45845) |
| Settings live-apply / race | On-the-fly settings change not applied; stale concurrent read | `Settings.cpp` `LoadSettings`/`InitFileWatcher`; `settings()` | Cross-thread read/write of settings | Atomic `shared_ptr<const Settings>` snapshot; load once/op | [#45993](https://github.com/microsoft/PowerToys/issues/45993), [PR #45994](https://github.com/microsoft/PowerToys/pull/45994) |
| Opacity hotkeys | +/- conflict on localized layout; numpad dead; hardcoded | `dllmain.cpp` `get_hotkeys`/`parse_hotkey`; `Settings` opacity hotkeys | Reused pin modifiers + hardcoded `VK_OEM_PLUS/MINUS` | Configurable opacity hotkeys; `isShown=(key!=0)`; fill `min(buffer,count)` | [#46135](https://github.com/microsoft/PowerToys/issues/46135), [#46209](https://github.com/microsoft/PowerToys/issues/46209), [#46300](https://github.com/microsoft/PowerToys/issues/46300), [#46387](https://github.com/microsoft/PowerToys/issues/46387), [#46391](https://github.com/microsoft/PowerToys/issues/46391), [PR #46410](https://github.com/microsoft/PowerToys/pull/46410) |
| Transparency restore | Opacity not restored on unpin; leaked `WS_EX_LAYERED` | `AlwaysOnTop.cpp` `ApplyWindowAlpha`/`RestoreWindowAlpha`/`ResolveTransparencyTargetWindow` | Apply/restore key mismatch; cache erased on failed restore | Only pinned windows; cache original state; restore same key; keep cache on failure | [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) |
| Border null-deref | Crash during 100 ms border refresh | `WindowBorder.cpp` `UpdateBorderPosition` | `m_frameDrawer` used without null-check | Guard `m_frameDrawer`/`m_window`/`m_trackingWindow` | [PR #48412](https://github.com/microsoft/PowerToys/pull/48412) |
| Elevated windows | No effect on admin apps | Pin path (`SetWindowPos`/`SetProp`/hooks) | UIPI blocks non-elevated → elevated manipulation | Document run-as-admin; don't silently no-op | [#46775](https://github.com/microsoft/PowerToys/issues/46775), [#47549](https://github.com/microsoft/PowerToys/issues/47549) |
| Sound-on-failure | Sound plays even when pin/unpin failed | `AlwaysOnTop.cpp` `ProcessCommand` | Sound not gated on `stateChanged` | Play only on real state change | [PR #46910](https://github.com/microsoft/PowerToys/pull/46910) |
| C++/C# default drift | Default border color differs from Settings UI | `Settings.h` (`RGB(0,173,239)`=`#00ADEF`) vs `AlwaysOnTopProperties.cs` (`#0099cc`) | Two hardcoded defaults out of sync | Single source / keep in lockstep (only shows when accent color off) | [#46961](https://github.com/microsoft/PowerToys/issues/46961) |
| Unterminated string_view | `HexToRGB` UB on non-null-terminated view | `Settings.cpp` `HexToRGB` (`std::stoll(hex.data())`) | `string_view::data()` not null-terminated | Construct `std::wstring(hex)` before `stoll` | [#46962](https://github.com/microsoft/PowerToys/issues/46962) |
| Reliability (LLKH thread) | Worker waits on null event handle | `AlwaysOnTop.cpp` `RegisterLLKH` | Failed `CreateEventW` leaves null in `handles[]` | Fail fast when a critical event can't be created | [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) |
| Win32 return values | Silent inconsistent window state | `RegisterHotkey`, `ApplyWindowAlpha`, `PinTopmostWindow` | `RegisterHotKey`/`SetWindowPos`/`SetLayeredWindowAttributes` failures ignored | Check + log return values | [PR #44815](https://github.com/microsoft/PowerToys/pull/44815) |

## Common Practices (enforced in review)

- **Settings live behind an immutable atomic snapshot.** Read via `AlwaysOnTopSettings::settings()`
  once per operation; observers implement `SettingsObserver::SettingsUpdate(SettingId)` and subscribe
  to the specific `SettingId`s they care about.
- **System-menu safety.** Tag owned items (`0x414F5450`), verify ownership before mutate, and scope
  global WinEvent hooks to when the feature is enabled.
- **Hotkey hygiene.** `get_hotkeys` returns a stable count but marks `isShown = (key != 0)`; opacity
  shortcuts are independent settings; numpad keys differ from `VK_OEM_*`.
- **Layered-window discipline.** Cache original `WS_EX_LAYERED`/alpha/color-key before the first
  opacity change; restore keyed to the same HWND; never drop the cache on a failed restore.
- **DWM/DPI correctness.** Border geometry from `DWMWA_EXTENDED_FRAME_BOUNDS`; scale thickness by
  `ScalingUtils::ScalingFactor`; rounded corners via `DWMWA_WINDOW_CORNER_PREFERENCE` (Win11 only,
  `E_INVALIDARG` on Win10 handled quietly).
- **Cross-language parity.** `Settings.h` struct is explicitly documented to be kept in sync with
  `AlwaysOnTopProperties.cs`; defaults and json keys must match.
- **Build hygiene.** Don't reorder `Microsoft.Cpp.*.props` imports; use `$(RepoRoot)` not relative
  paths; keep PowerShell build-step warnings visible so resource/localization issues surface.

---
*Corpus: 12 merged PRs, 84 review comments, 30 bug issues + source verification against
`src/modules/alwaysontop`.*
