CULPRIT_FILES: src/modules/CropAndLock/CropAndLock/OverlayWindow.cpp

CULPRIT_FUNCTIONS: OverlayWindow::OverlayWindow (constructor)

FIX: The OverlayWindow constructor hardcodes black and red colors for the shade and selection brushes without respecting the system theme. The fix is to use theme-aware colors that adapt to dark/light mode by querying the current theme or using system-defined color resources.

CITED_FIX_PR: none

CONFIDENCE: high
