# AlwaysOnTop PR Review Checklist (template — modify per PR)

Apply **after** reading the diff cold. Check only the groups whose files the diff touches. Each item
links to the Regression Playbook / Review Rule it enforces.

## General (any AlwaysOnTop PR)
- [ ] Diff read cold first; concerns formed before consulting this checklist (anti-anchoring).
- [ ] New persisted setting has serialization/CLI-`set` test coverage (`Settings.UI.UnitTests`).
- [ ] C++ `Settings.h` defaults and C# `AlwaysOnTopProperties.cs` defaults agree (color, hotkeys).
- [ ] No reordered `Microsoft.Cpp.*.props` imports; paths use `$(RepoRoot)` not `..\..\`.

## Pin / unpin core (`AlwaysOnTop.cpp`)
- [ ] `WS_EX_TOPMOST` state and `AlwaysOnTop_Pinned` window prop kept consistent.
- [ ] `AlwaysOnTop_Pinned` string unchanged, or updated in both `AlwaysOnTop.cpp` and `dllmain.cpp`.
- [ ] Excluded-apps and game-mode gates honored in `ProcessCommand`.
- [ ] Sound only plays when pin/unpin actually succeeded (`stateChanged`).

## Settings snapshot / observer (`Settings.cpp`, `Settings.h`)
- [ ] `AlwaysOnTopSettings::settings()` bound to one local per operation (no repeated atomic loads).
- [ ] New setting added to `SettingId` enum, `LoadSettings`, and observer subscription lists.
- [ ] No `string_view::data()` passed to null-terminated-string APIs (`HexToRGB`/`stoll`).

## Hotkeys (`dllmain.cpp`, `AlwaysOnTop.cpp`)
- [ ] `get_hotkeys` fills `min(buffer_size, count)` and sets `isShown = (key != 0)`.
- [ ] Opacity hotkeys configurable independently of the pin hotkey.
- [ ] `RegisterHotKey` return value checked/logged on failure.

## Opacity / transparency (`AlwaysOnTop.cpp`)
- [ ] Transparency applied only to pinned windows (`ResolveTransparencyTargetWindow`).
- [ ] Apply and restore use the same HWND key; original layered state cached before first change.
- [ ] Cache not erased when a restore Win32 call fails.
- [ ] `SetLayeredWindowAttributes`/`SetWindowLong` return values considered.

## System menu (`AlwaysOnTop.cpp`)
- [ ] Menu items tagged with owner `dwItemData` and verified via `IsAlwaysOnTopMenuCommand`.
- [ ] Menu/`EVENT_OBJECT_INVOKED` hooks installed only while `ShowInSystemMenu` is enabled.
- [ ] Behavior verified against a custom-titlebar app (RDP/UWP/Firefox) — no menu breakage.

## Border / frame (`WindowBorder.cpp`, `FrameDrawer.cpp`)
- [ ] `m_frameDrawer`/`m_window`/`m_trackingWindow` null-guarded before use (timer refresh).
- [ ] DPI scaling applied (`ScalingUtils::ScalingFactor`); rounded corners degrade on Win10.
- [ ] Border teardown clean when frame disabled or window unpinned/moved off desktop.

## Build / deps
- [ ] After CppWinRT/SDK bump: module builds and pins a window in a smoke test.
- [ ] PowerShell build-step invocations reliable (no swallowed warnings that hide resource errors).
