---
name: zoomit-knowledge
description: 'PowerToys ZoomIt module knowledge: feature->file/function map, regression playbooks (hotkey XOR-modifier derivation collisions, toggle/save hotkey coupling registering VK=0, recording lifecycle + audio-init race, recording/screenshot filename suffixes, cursor/overlay visibility on multi-monitor & fractional DPI, international/AltGr hotkey conflicts), maintainer review rules, and pitfalls. Load when planning, implementing, fixing, triaging, or reviewing changes under src/modules/ZoomIt — live zoom, draw/annotate, break timer, DemoType, screen/GIF recording, webcam + background blur, mic noise cancellation, OCR/snip, panorama, hotkey registration, settings, DPI. Keywords: ZoomIt, live zoom, magnification, annotate, break timer, screen recording, GIF, webcam, background blur, noise cancellation, OCR, snip, panorama, hotkey, RegisterHotKey, MOD_ALT, DPI, multi-monitor, PR review, regression.'
license: Complete terms in LICENSE.txt
---

# PowerToys ZoomIt Knowledge

Grounded engineering knowledge for the PowerToys **ZoomIt** module (source dir
`src/modules/ZoomIt/`) — the Sysinternals ZoomIt screen-zoom / annotation / break-timer /
screen-recording tool ported into PowerToys. ZoomIt is a single large Win32 message-pump app
(`ZoomIt/Zoomit.cpp`, ~12k lines) driven entirely by global-scope hotkeys, wrapped by a PowerToys
module interface (`ZoomItModuleInterface/`) and a WinUI 3 settings page. Use this skill to localize
code fast, avoid the recurring regression traps (mostly hotkey and recording lifecycle), and enforce
the conventions maintainers already established.

## When to Use This Skill

- Planning or implementing a change under `src/modules/ZoomIt/` and needing prior art.
- Fixing/triaging a ZoomIt bug: hotkey not registering / conflicting / capturing bare keys, "shortcut
  already in use" errors, recording crash or GIF stuck, audio device not opening, cursor invisible,
  webcam overlay mis-positioned, OCR/snip breaking on non-US keyboards, break/live-zoom/draw glitches.
- Reviewing a ZoomIt PR against maintainer conventions and the recurring regression classes.
- Touching hotkey registration (the XOR-derived Record/LiveDraw/DemoType-Reset variants), the
  recording session lifecycle, webcam/audio pipelines, DPI scaling of the options dialog, or the
  registry-backed settings round-trip.

## Module Map (feature -> file/function)

Localization aid. Treat as **hypotheses to confirm in source**, not ground truth (see anti-anchoring
below). Source root `src/modules/ZoomIt/`; unqualified files live in `ZoomIt/`.

| Sub-feature | Implementation (file · function) |
|---|---|
| PowerToys module wrapper, GPO gate, enable/disable, launch ZoomIt.exe | `ZoomItModuleInterface/dllmain.cpp` `enable/disable`, `gpo_policy_enabled_configuration` → `getConfiguredZoomItEnabledValue` |
| Hotkey IDs (all 22) | `Zoomit.cpp:83-104` `ZOOM_HOTKEY..WEBCAM_TOGGLE_HOTKEY` |
| Central hotkey registration + logging lambda | `Zoomit.cpp:3569` `RegisterAllHotkeys` (local `registerHotkey`) |
| **XOR-derived hotkey variants** (crop/window/live-draw/DemoType-reset) | `RegisterAllHotkeys`: `cropMod = g_RecordToggleMod ^ MOD_SHIFT`, `windowMod = g_RecordToggleMod ^ MOD_ALT` (3606-3612); `LIVE_DRAW = g_LiveZoomToggleMod ^ MOD_SHIFT` (3580); `DEMOTYPE_RESET = g_DemoTypeToggleMod ^ MOD_SHIFT` (3586) |
| Other 3 registration sites (must stay in sync) | Options-dialog validation `OptionsProc` (~`Zoomit.cpp:5520-5620`); startup `MainWndProc WM_CREATE` (~`7691-7708`); `WM_USER_RELOAD_SETTINGS` (~`10356-10373`) |
| Main window message pump / hotkey dispatch | `Zoomit.cpp:7501` `MainWndProc`, `WM_HOTKEY` (~`7870`) |
| Zoom mode (static zoom + pan/draw) | `MainWndProc` `ZOOM_HOTKEY` path; `DrawWndProc`/draw state in `Zoomit.cpp` |
| Live Zoom (real-time magnifier) | `g_hWndLiveZoom`/`g_hWndLiveZoomMag` (`Zoomit.cpp:188-189`) via Magnification API `pMagSetWindowSource/pMagSetWindowTransform` (225-243); `g_ZoomOnLiveZoom` |
| LiveDraw (annotate over live zoom, layered window) | `LIVE_DRAW_HOTKEY`; layered-window note `Zoomit.cpp:5941`; pen-width scaling by `g_LiveZoomLevel` (~6428-6438) |
| Draw / annotate shapes, blur, highlight | `Zoomit.cpp:1135` `DrawBlurredShape`, `:1300` `DrawHighlightedShape`; pen/marker state globals |
| Break timer (countdown screen) | `ZoomItBreak/BreakTimer.cpp`, `BreakTimer.h`; `BREAK_HOTKEY`; `g_BreakTimerPosition` |
| DemoType (typed-text playback) | `DemoType.cpp/.h`; `DEMOTYPE_HOTKEY`/`DEMOTYPE_RESET_HOTKEY`; `TypeModeState` |
| Screen recording (MP4) session | `VideoRecordingSession.cpp/.h` `Create`; orchestrated by `Zoomit.cpp` `StartRecordingAsync` (`winrt::fire_and_forget`) |
| GIF recording session | `GifRecordingSession.cpp/.h` `Create`; `g_GifRecordingSession` |
| Frame capture (Windows.Graphics.Capture) | `CaptureFrameWait.cpp/.h` |
| Audio capture + generation (mic + system loopback) | `AudioSampleGenerator.cpp/.h` `InitializeAsync`; `LoopbackCapture.cpp/.h`; started early in `StartRecordingAsync` (~`Zoomit.cpp:6996-7001`) |
| Mic noise cancellation (RNNoise) | `NoiseSuppressor.cpp/.h`; flag `g_NoiseCancellation` |
| Webcam overlay capture + compositing | `WebcamCapture.cpp/.h`, `WebcamPreviewWindow.cpp/.h`, `WebcamComposite.hlsl`/`WebcamCompositePS.h`/`WebcamCompositeVS.h`; `WEBCAM_TOGGLE_HOTKEY` |
| Webcam background blur (mediapipe selfie segmentation) | `BackgroundBlur.cpp/.h`, `BoxBlurCS.hlsl`/`BoxBlurCS.h`, model `selfie_segmentation.onnx` |
| Unique recording filename (timestamp vs numeric suffix) | `Zoomit.cpp:6893` `GetUniqueRecordingFilename`, `IsDefaultRecordingFilename`, `GetTimestampSuffix` |
| Unique screenshot filename | `Zoomit.cpp` `GetUniqueScreenshotFilename` |
| Screenshot save/copy + WebP/JPG/PNG encoding | `ImageEncoder.cpp/.h`; `SAVE_IMAGE_HOTKEY`/`SAVE_CROP_HOTKEY`/`COPY_IMAGE_HOTKEY`/`COPY_CROP_HOTKEY` |
| Snip (region select) | `SelectRectangle.cpp/.h`; `SNIP_HOTKEY`/`SNIP_SAVE_HOTKEY` |
| OCR / text extraction from snip | `Zoomit.cpp:1558` `OcrFromHBITMAP` (Windows.Media.Ocr `OcrEngine::TryCreateFromUserProfileLanguages`, `MaxImageDimension`); `SNIP_OCR_HOTKEY` |
| Panorama (scrolling screenshot) | `PanoramaCapture.cpp/.h`; `SNIP_PANORAMA_HOTKEY`/`SNIP_PANORAMA_SAVE_HOTKEY` |
| DPI scaling of options dialog | `GetDpiForWindowHelper`, `ScaleDialogForDpi`, `ScaleForDpi`, `WM_DPICHANGED`, `DPI_BASELINE` (`Zoomit.cpp` throughout) |
| Multi-monitor targeting | `MonitorFromPoint(MONITOR_DEFAULTTONEAREST)` + `GetMonitorInfo` (~`Zoomit.cpp:2258`) |
| Registry-backed settings model | `ZoomItSettings.h` `REG_SETTING RegSettings[]`; `Registry.h` |
| Settings reload from PowerToys | `MainWndProc` `WM_USER_RELOAD_SETTINGS` (~`Zoomit.cpp:10356`) |
| Settings UI (WinUI 3) | `src/settings-ui/Settings.UI/ViewModels/ZoomItViewModel.cs`, `.../SettingsXAML/Views/ZoomItPage.xaml` (was `.../Views/ZoomItPage.xaml`); native bridge `ZoomItSettingsInterop/ZoomItSettings.cpp/.idl` |
| Telemetry | `ZoomItModuleInterface/trace.cpp` |

**Hotkey model (critical):** ZoomIt registers global hotkeys via Win32 `RegisterHotKey`. Several
hotkeys are **derived from one base modifier by XOR**, not stored independently: record-crop =
`base ^ MOD_SHIFT`, record-window = `base ^ MOD_ALT`, live-draw = `liveBase ^ MOD_SHIFT`,
DemoType-reset = `demoBase ^ MOD_SHIFT`. This same registration logic is **duplicated across four
sites** (`RegisterAllHotkeys`, `OptionsProc` validation, `MainWndProc` startup,
`WM_USER_RELOAD_SETTINGS`) — a fix in one must be mirrored in all four.

## Regression Playbooks

Rule by rule. Each: **Symptom → Where → Root cause → Guardrail**. Fuller catalog in
[references/regression-catalog.md](./references/regression-catalog.md). Most issue bodies in this
corpus are terse (title-level); technical rows were verified against source.

### XOR-derived hotkey modifier collides / registers a bare key
- **Symptom:** pressing an unmodified key (e.g. `5`) triggers window recording; or a recording
  variant silently fails to register.
- **Where:** `RegisterAllHotkeys` and the 3 mirror sites — `RECORD_WINDOW = g_RecordToggleMod ^ MOD_ALT`,
  `RECORD_CROP = g_RecordToggleMod ^ MOD_SHIFT`.
- **Root cause:** when the base modifier is **exactly** the XORed bit (e.g. Alt-only base),
  `base ^ MOD_ALT == 0`, registering a **modifier-less** hotkey that captures every bare keypress.
- **Guardrail:** only register a derived variant when its computed modifier is non-zero —
  `if ((g_RecordToggleMod ^ MOD_ALT) != 0) registerHotkey(...)`. Apply at **all four** registration
  sites. Evidence: [PR #47388](https://github.com/microsoft/PowerToys/pull/47388); regression from
  commit `ba68b88` also fixed in [PR #48401](https://github.com/microsoft/PowerToys/pull/48401) and
  [PR #48266](https://github.com/microsoft/PowerToys/pull/48266) (shortcuts failing to register in the
  standalone build).

### Toggle & Save hotkeys coupled → VK=0 registration + misleading "already in use"
- **Symptom:** clearing the "Snip Save" (or "Panorama Save") hotkey field makes the options dialog /
  settings reload fail with a misleading *"snip hotkey is already in use"* error.
- **Where:** `OptionsProc` (~`5609`, `5620`), `MainWndProc` startup (~`7691`, `7708`),
  `WM_USER_RELOAD_SETTINGS` (~`10356`, `10373`): `SNIP_SAVE_HOTKEY` / `SNIP_PANORAMA_SAVE_HOTKEY`
  registered unconditionally whenever the *toggle* key is set.
- **Root cause:** the save hotkey is treated as a coupled pair with its toggle; when the save field is
  empty (`HKM_GETHOTKEY` returns 0) it attempts to register VK=0, which fails and mis-attributes the
  conflict to the toggle hotkey.
- **Guardrail:** validate/register toggle and save **independently**; only register a save hotkey when
  its key is non-zero; report conflicts against the save hotkey separately; use the existing
  `registerHotkey` helper (so outcomes are logged) rather than raw `RegisterHotKey`. Evidence:
  [PR #49075](https://github.com/microsoft/PowerToys/pull/49075) review comments; related
  [#46938](https://github.com/microsoft/PowerToys/issues/46938) (Ctrl+S not editable).

### International / AltGr keyboards: hotkeys hijack normal typing
- **Symptom:** on non-US layouts, ZoomIt shortcuts intercept characters typed with AltGr (Ctrl+Alt) —
  e.g. `AltGr+6/7` (pipe, etc.) triggers screenshot/record; text-extraction shortcut breaks
  international keyboard usage.
- **Where:** default hotkey assignments + `WM_HOTKEY` handling in `MainWndProc`; modifier parsing
  (`GetKeyMod`). AltGr surfaces as `Ctrl+Alt`, colliding with derived variants.
- **Root cause:** default bindings and the Ctrl+Alt-based modifiers overlap with AltGr composition on
  many layouts; ZoomIt consumes the key before the app sees it.
- **Guardrail:** when changing default hotkeys or modifier handling, validate on non-US layouts
  (French/German), avoid Ctrl+Alt defaults, and ensure a bare-key or AltGr combo isn't globally
  registered. Evidence: [#48377](https://github.com/microsoft/PowerToys/issues/48377),
  [#47491](https://github.com/microsoft/PowerToys/issues/47491),
  [#46656](https://github.com/microsoft/PowerToys/issues/46656),
  [#47836](https://github.com/microsoft/PowerToys/issues/47836),
  [#47072](https://github.com/microsoft/PowerToys/issues/47072) (see also
  [Win32 RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey)).

### Recording lifecycle: crash / GIF stuck / audio device race
- **Symptom:** Ctrl+5 MP4 record shows an orange frame then vanishes / crashes; GIF partial-capture
  gets stuck; trim then re-record fails to save; audio device intermittently not opened.
- **Where:** `StartRecordingAsync` (`winrt::fire_and_forget`), `VideoRecordingSession::Create`,
  `GifRecordingSession::Create`, `AudioSampleGenerator::InitializeAsync` (started early ~`7000`),
  session teardown (`g_RecordingSession`/`g_GifRecordingSession` reset ~`6749-7182`).
- **Root cause:** async startup/teardown ordering — audio init is kicked off in the background before
  D3D/capture-item creation to hide ~1400 ms latency, so lifecycle races and unhandled failures leave
  a half-initialized session.
- **Guardrail:** keep session create/close ordering explicit; await/join the audio init action before
  first sample use; on any startup failure fully null out the session and its captured-frame state.
  Test MP4 and GIF, with and without audio, plus record→trim→record-again. Evidence:
  [PR #48685](https://github.com/microsoft/PowerToys/pull/48685) (audio init race);
  [#48368](https://github.com/microsoft/PowerToys/issues/48368),
  [#47877](https://github.com/microsoft/PowerToys/issues/47877),
  [#47773](https://github.com/microsoft/PowerToys/issues/47773),
  [#47316](https://github.com/microsoft/PowerToys/issues/47316),
  [#46006](https://github.com/microsoft/PowerToys/issues/46006).

### Recording/screenshot filename suffix handling
- **Symptom:** ZoomIt strips a numeric suffix from a user-chosen recording name (`Clip1.mp4` →
  `Clip.mp4`), risking overwrite; default names don't sort chronologically.
- **Where:** `GetUniqueRecordingFilename` (`Zoomit.cpp:6893`), `IsDefaultRecordingFilename`,
  `GetTimestampSuffix`; screenshots in `GetUniqueScreenshotFilename`.
- **Root cause:** suffix logic was applied unconditionally instead of only to the *default* filename.
- **Guardrail:** only the default filename gets a timestamp; custom names get a numeric `(n)` suffix
  **only on collision** and never have an existing suffix stripped. Evidence:
  [PR #43236](https://github.com/microsoft/PowerToys/pull/43236) (closes
  [#43202](https://github.com/microsoft/PowerToys/issues/43202)).

### Cursor / overlay visibility on multi-monitor & fractional DPI
- **Symptom:** mouse cursor disappears while a mode is active or on a second display; webcam overlay
  is mis-positioned or squished when display scaling is < 100% or the cutout isn't rectangular.
- **Where:** DPI helpers (`GetDpiForWindowHelper`, `ScaleForDpi`, `ScaleDialogForDpi`, `WM_DPICHANGED`);
  monitor selection (`MonitorFromPoint`/`GetMonitorInfo` ~`2258`); webcam compositing
  (`WebcamCapture.cpp`, `WebcamPreviewWindow.cpp`, `WebcamComposite.hlsl`).
- **Root cause:** cursor show/hide and overlay geometry assume 100% scaling / primary monitor; per-DPI
  and per-monitor coordinate conversion is incomplete on some paths.
- **Guardrail:** convert coordinates per target monitor DPI; verify cursor visibility restore on every
  mode-exit path; test at 125%/150% fractional scaling and on secondary displays. Evidence:
  [#48823](https://github.com/microsoft/PowerToys/issues/48823),
  [#47736](https://github.com/microsoft/PowerToys/issues/47736),
  [#48508](https://github.com/microsoft/PowerToys/issues/48508),
  [#48529](https://github.com/microsoft/PowerToys/issues/48529),
  [#48857](https://github.com/microsoft/PowerToys/issues/48857) (live-zoom duplicate taskbar).

## Review Rules

Enforce these when reviewing or authoring ZoomIt changes:

- **Mirror every hotkey-registration change across all four sites.** `RegisterAllHotkeys`,
  `OptionsProc` validation, `MainWndProc` startup, and `WM_USER_RELOAD_SETTINGS` duplicate the same
  logic — a fix in one that isn't mirrored reintroduces the bug (see PR #47388's "4 hotkey
  registration sites").
- **Guard XOR-derived modifiers with a non-zero check.** Never register `base ^ MOD_x` without
  `!= 0` — a zero modifier grabs bare keys (#47388). Prefer extracting the derived modifier into a
  named local (`cropMod`, `windowMod`) with an explicit `!= 0` check over inlining the XOR in the
  `if` condition and the `RegisterHotKey` call (review consensus on PR #49075 / commit `c7a62d6`).
- **Register toggle and save hotkeys independently.** Only register a save hotkey when its VK is
  non-zero; attribute conflicts to the correct hotkey (#49075).
- **Use the `registerHotkey` helper, not raw `RegisterHotKey`.** The helper logs outcomes; raw calls
  make failures invisible and diverge from the other hotkeys (#49075 review).
- **All end-user strings must be localizable.** New UI text goes through `Resources.resw`, not
  hard-coded literals; use Sentence casing per modern Windows apps (repeated reviewer note on
  `ZoomItPage.xaml`, PR #47529).
- **Keep recording session teardown explicit and null-safe.** On stop/failure, `Close()` then null the
  `g_RecordingSession`/`g_GifRecordingSession` shared_ptr; don't leave a half-initialized session
  (#48685, #46006).
- **Await audio init before first sample use.** `AudioSampleGenerator::InitializeAsync` is launched
  early for latency; consumers must join it, not assume it's done (#48685).
- **Respect DPI + per-monitor coordinates.** Scale dialog/overlay geometry via the `*ForDpi` helpers
  and resolve the target monitor with `MonitorFromPoint`; don't assume 96 DPI or the primary monitor
  (#48508, #48823).
- **Settings must round-trip through the registry model.** New options belong in
  `ZoomItSettings.h RegSettings[]` and the `ZoomItViewModel.cs`/`ZoomItSettingsInterop` bridge, and
  must survive `WM_USER_RELOAD_SETTINGS`.
- **UI-display comments ≠ registration logic.** In `ZoomItViewModel.cs`, returning null suppresses
  *display* of a bare-key label (the converter), it does not affect native hotkey registration —
  word comments accordingly (PR #47539 review).

## Pitfalls

- **Never** register `base ^ MOD_ALT` / `base ^ MOD_SHIFT` without a non-zero guard — an Alt-only (or
  Shift-only) base collapses to a modifier-less hotkey that swallows every bare keypress (#47388).
- **Four copies of the registration logic.** Fixing only `RegisterAllHotkeys` leaves the options
  dialog, startup, and reload paths broken. Grep for `RECORD_WINDOW_HOTKEY` to find them all.
- **A cleared hotkey field returns VK=0** from `HKM_GETHOTKEY` — registering it fails and reports the
  *wrong* hotkey as "already in use" (#49075). Skip registration when the key is 0.
- **AltGr is Ctrl+Alt.** Any Ctrl+Alt default binding will hijack AltGr-composed characters on
  international keyboards (#48377, #47491, #46656). Avoid Ctrl+Alt defaults.
- **Audio init runs in the background to hide ~1400 ms of latency** — treat it as an in-flight
  `IAsyncAction`; using audio before joining it is the documented race (#48685).
- **Only the *default* recording/screenshot name gets a timestamp** — custom names keep their digits
  and only gain `(n)` on collision; don't strip user suffixes (#43236).
- **`Zoomit.cpp` is a ~12k-line global-state message pump** — most feature state is file-scope globals
  (`g_*`); changing one hotkey/mode often ripples through `MainWndProc`'s `WM_HOTKEY` switch.
- **Live Zoom uses the Magnification API via runtime-loaded pointers** (`pMagSetWindowSource/Transform`);
  they can be null on some SKUs — null-check before calling.
- **The options dialog is manually DPI-scaled** (`ScaleDialogForDpi` on `WM_DPICHANGED`); new controls
  must participate or they'll be mis-sized at fractional scaling (#48367, #48188 scroll glitches).

## Using This Skill in PR Review

Read the diff cold and form your own concerns **first**, then cross-check *only the touched paths*
against the Module Map and Regression Playbooks — skimming them first anchors you and lowers your
catch rate. Full method (anti-anchoring, bug localization, freshness):
[using these skills](../knowledge-skill-usage.md).

## References

- [references/regression-catalog.md](./references/regression-catalog.md) — fuller regression + decision list.
- [templates/pr-review-checklist.md](./templates/pr-review-checklist.md) — apply to a ZoomIt PR.
- [templates/bug-triage.md](./templates/bug-triage.md) — symptom → likely file/function.
- Source root: [`src/modules/ZoomIt/`](https://github.com/microsoft/PowerToys/tree/main/src/modules/ZoomIt)
- [Win32 RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey) ·
  [Magnification API](https://learn.microsoft.com/en-us/windows/win32/api/_magapi/) ·
  [Windows.Media.Ocr](https://learn.microsoft.com/en-us/uwp/api/windows.media.ocr) ·
  [High-DPI (WM_DPICHANGED)](https://learn.microsoft.com/en-us/windows/win32/hidpi/wm-dpichanged)
