# Bug Localization Answer: ColorPicker

**CULPRIT_FILES:**
1. `src/modules/colorPicker/ColorPickerUI/Helpers/ZoomWindowHelper.cs` (primary)
2. Missing: `src/modules/colorPicker/ColorPickerUI/Helpers/WindowCaptureExclusionHelper.cs` (needs to be created)

**CULPRIT_FUNCTIONS:**
- `ZoomWindowHelper.SetZoomImage()` (line 66-79, specifically line 73 `_graphics.CopyFromScreen()`)
- Missing: `WindowCaptureExclusionHelper.Exclude()` and `WindowCaptureExclusionHelper.Include()`

**FIX:**
The zoom magnifier captures the picker window itself because `CopyFromScreen` is called while the picker UI is visible on screen. Need to create `WindowCaptureExclusionHelper.cs` that calls `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` before the screen capture in `SetZoomImage`, then restores `WDA_NONE` in a finally block. This Win10 2004+ API excludes the window from all screen capture operations.

**CITED_FIX_PR:**
#48762

**CONFIDENCE:**
high

**USED_SKILL:**
The skill's regression playbook "Zoom (magnifier) view includes the Color Picker's own window" directly matched the symptom, pointing to `ZoomWindowHelper.SetZoomImage` and the missing `WindowCaptureExclusionHelper` wrapper for `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`.
