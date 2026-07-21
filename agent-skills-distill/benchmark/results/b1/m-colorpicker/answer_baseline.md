CULPRIT_FILES: src/modules/colorPicker/ColorPickerUI/Helpers/ZoomWindowHelper.cs

CULPRIT_FUNCTIONS: ZoomWindowHelper.ShowZoomWindow

FIX: The ShowZoomWindow method calls AppStateHandler.SetTopMost() which sets the MainWindow to topmost when the zoom window is shown, causing the main ColorPicker UI to appear in the zoomed view. The fix is to set the zoom window itself to topmost instead of calling SetTopMost on the main window.

CITED_FIX_PR: none

CONFIDENCE: high
