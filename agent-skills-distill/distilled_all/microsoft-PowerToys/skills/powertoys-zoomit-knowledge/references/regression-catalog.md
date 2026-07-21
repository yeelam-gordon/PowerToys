# ZoomIt Regression Catalog (Progressive Disclosure)

Fuller regression + decision list. Read the row for the area your change touches; confirm each claim
in source before acting. Symptoms map to `src/modules/ZoomIt/` (unqualified files in `ZoomIt/`).
Issue bodies in this corpus are largely title-only; technical rows were verified directly against
source (`Zoomit.cpp` line numbers are approximate — grep the named symbol).

## Key Decisions (context for the playbooks)

- **Single global-state message pump.** `ZoomIt/Zoomit.cpp` (~12k lines) holds nearly all feature
  state as file-scope `g_*` globals and dispatches everything through `MainWndProc`'s `WM_HOTKEY`
  switch (`Zoomit.cpp:7501`, `7870`). There is no per-feature object model — changing one mode often
  ripples across the switch.
- **Hotkeys derived by XOR from a base modifier.** Record-crop = `base ^ MOD_SHIFT`, record-window =
  `base ^ MOD_ALT`, live-draw = `liveBase ^ MOD_SHIFT`, demotype-reset = `demoBase ^ MOD_SHIFT`
  (`RegisterAllHotkeys`, `Zoomit.cpp:3580-3612`). Compact, but a base equal to the XORed bit collapses
  the modifier to zero.
- **Registration logic duplicated across four sites.** `RegisterAllHotkeys` (3569), `OptionsProc`
  validation (~5520-5620), `MainWndProc` startup (~7691-7708), and `WM_USER_RELOAD_SETTINGS`
  (~10356-10373). All four must agree; fixes must be mirrored.
- **`registerHotkey` helper logs outcomes.** `RegisterAllHotkeys` defines a local `registerHotkey`
  lambda that logs success/failure; new hotkeys added with raw `RegisterHotKey` diverge and hide
  failures (PR #49075 review).
- **Recording is fully async.** `StartRecordingAsync` is a `winrt::fire_and_forget`; audio
  (`AudioSampleGenerator::InitializeAsync`) is kicked off *before* D3D/capture-item creation to hide
  ~1400 ms of AudioGraph/mic latency (`Zoomit.cpp:6988-7001`), then MP4 (`VideoRecordingSession`) or
  GIF (`GifRecordingSession`) session is created. Teardown nulls `g_RecordingSession`/
  `g_GifRecordingSession`.
- **Filename uniqueness has two modes.** `GetUniqueRecordingFilename` (6893): default name → stem +
  `GetTimestampSuffix` + ext (chronological sort); custom name → numeric `(n)` suffix only on
  collision. Screenshots always timestamp (`GetUniqueScreenshotFilename`).
- **Live Zoom uses the Magnification API via runtime-loaded function pointers**
  (`pMagSetWindowSource`, `pMagSetWindowTransform`, `Zoomit.cpp:225-243`); LiveDraw annotates over it
  through a layered window (`Zoomit.cpp:5941`) with pen width scaled by `g_LiveZoomLevel`.
- **Options dialog is manually DPI-scaled.** `GetDpiForWindowHelper` + `ScaleDialogForDpi` on
  `WM_CREATE`/`WM_DPICHANGED` against `DPI_BASELINE`; controls that don't participate mis-size at
  fractional scaling.
- **Settings are registry-backed.** `ZoomItSettings.h RegSettings[]` + `Registry.h`; the WinUI 3
  page (`ZoomItViewModel.cs` / `ZoomItPage.xaml`) writes via `ZoomItSettingsInterop`, and the native
  app reloads on `WM_USER_RELOAD_SETTINGS`.

## Regression Table

| Class | Symptom | Where (file · function) | Root cause | Fix / Guardrail | Evidence |
|---|---|---|---|---|---|
| XOR modifier collision | Bare key triggers window record / variant fails to register | `RegisterAllHotkeys` + 3 mirrors, `^ MOD_ALT`/`^ MOD_SHIFT` | Base == XORed bit → modifier 0, grabs bare keys | Register derived variant only when `(mod ^ MOD_x) != 0`; mirror all 4 sites | [PR #47388](https://github.com/microsoft/PowerToys/pull/47388), [PR #48401](https://github.com/microsoft/PowerToys/pull/48401), [PR #48266](https://github.com/microsoft/PowerToys/pull/48266) |
| Toggle/Save coupling | "Snip hotkey already in use" after clearing Save field | `OptionsProc` ~5609/5620, startup ~7691/7708, reload ~10356/10373 | Save hotkey registered unconditionally with its toggle; VK=0 register fails | Register/validate toggle & save independently; skip VK=0; use `registerHotkey` helper | [PR #49075](https://github.com/microsoft/PowerToys/pull/49075), [#46938](https://github.com/microsoft/PowerToys/issues/46938) |
| International/AltGr | Shortcuts hijack AltGr-typed chars on non-US layouts | default bindings; `WM_HOTKEY`; `GetKeyMod` | Ctrl+Alt defaults == AltGr; globally consumed | Avoid Ctrl+Alt defaults; validate non-US layouts | [#48377](https://github.com/microsoft/PowerToys/issues/48377), [#47491](https://github.com/microsoft/PowerToys/issues/47491), [#46656](https://github.com/microsoft/PowerToys/issues/46656), [#47836](https://github.com/microsoft/PowerToys/issues/47836), [#47072](https://github.com/microsoft/PowerToys/issues/47072) |
| Recording lifecycle | MP4 crash/orange frame; GIF stuck; trim→record fails; audio race | `StartRecordingAsync`, `VideoRecordingSession::Create`, `GifRecordingSession::Create`, `AudioSampleGenerator::InitializeAsync` | Async create/teardown ordering; early audio init race | Explicit ordering; join audio init; null session on failure; test MP4+GIF±audio | [PR #48685](https://github.com/microsoft/PowerToys/pull/48685), [#48368](https://github.com/microsoft/PowerToys/issues/48368), [#47877](https://github.com/microsoft/PowerToys/issues/47877), [#47773](https://github.com/microsoft/PowerToys/issues/47773), [#47316](https://github.com/microsoft/PowerToys/issues/47316), [#46006](https://github.com/microsoft/PowerToys/issues/46006) |
| Filename suffix | User filename digits stripped; default not sortable | `GetUniqueRecordingFilename`, `IsDefaultRecordingFilename`, `GetTimestampSuffix` | Suffix logic applied unconditionally, not only to default | Default → timestamp; custom → `(n)` only on collision; never strip user digits | [PR #43236](https://github.com/microsoft/PowerToys/pull/43236), [#43202](https://github.com/microsoft/PowerToys/issues/43202) |
| Cursor/overlay/DPI | Cursor invisible on 2nd display; webcam overlay squished at <100%; dialog mis-sized | DPI helpers; `MonitorFromPoint`/`GetMonitorInfo`; `WebcamCapture.cpp` | 100%/primary-monitor assumptions; incomplete per-DPI coord conversion | Per-monitor DPI coords; restore cursor on all exits; test fractional scaling | [#48823](https://github.com/microsoft/PowerToys/issues/48823), [#47736](https://github.com/microsoft/PowerToys/issues/47736), [#48508](https://github.com/microsoft/PowerToys/issues/48508), [#48529](https://github.com/microsoft/PowerToys/issues/48529), [#48857](https://github.com/microsoft/PowerToys/issues/48857), [#48367](https://github.com/microsoft/PowerToys/issues/48367), [#48188](https://github.com/microsoft/PowerToys/issues/48188) |
| Panorama input | Panorama conflict popup blocks user input | `PanoramaCapture.cpp`; `WM_HOTKEY` panorama guard | Modal/blocking capture on conflict | Non-blocking conflict handling | [#47154](https://github.com/microsoft/PowerToys/issues/47154) |
| Annotate/draw | Semi-transparent highlight leaves straight drag traces; pen/touch draw on live zoom fails | `DrawHighlightedShape` (1300); LiveDraw path | Highlight compositing / touch input handling | Verify highlight redraw + pen/touch input | [#47329](https://github.com/microsoft/PowerToys/issues/47329), [#46369](https://github.com/microsoft/PowerToys/issues/46369) |
| Interop capture | Crop And Lock / screen capture gets black image of ZoomIt annotations | ZoomIt overlay window styles; capture affinity | Overlay excluded/unreadable by external capture | Verify overlay capturability contract | [#48850](https://github.com/microsoft/PowerToys/issues/48850) |

## Common Practices (enforced in review)

- **Mirror hotkey changes across all four registration sites** and guard XOR-derived modifiers with
  `!= 0` (#47388).
- **Register toggle and save hotkeys independently; skip VK=0**; use the logging `registerHotkey`
  helper (#49075).
- **All end-user strings localizable** via `Resources.resw`, Sentence casing (PR #47529, #47539).
- **Recording sessions:** explicit create/close ordering, join audio init, null-out on failure
  (#48685, #46006).
- **Filename uniqueness:** default → timestamp, custom → `(n)` on collision only (#43236).
- **DPI/multi-monitor:** scale via `*ForDpi` helpers, resolve monitor via `MonitorFromPoint`; no
  96-DPI/primary assumptions (#48508, #48823).
- **Settings round-trip** through `RegSettings[]` + `ZoomItSettingsInterop` + `WM_USER_RELOAD_SETTINGS`.
- **UI-display null in `ZoomItViewModel.cs`** suppresses a bare-key *label*, not native registration —
  comment accordingly (PR #47539).

---
*Corpus: 12 merged PRs (Apr–Jul 2026), 31 review comments, 32 conversation comments, 30 bug issues
(mostly title-level) + source verification against `src/modules/ZoomIt`. Technical regression rows
confirmed directly in source; issue-only rows are grounded on titles + code, not maintainer prose.
CI/`/azp run`, spell-check allowlist, and pure-typo comments were excluded as noise.*
