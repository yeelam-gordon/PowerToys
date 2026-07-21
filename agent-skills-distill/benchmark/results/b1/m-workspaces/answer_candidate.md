CULPRIT_FILES: WorkspacesEditor/ViewModels/MainViewModel.cs

CULPRIT_FUNCTIONS: EnterSnapshotMode

FIX: The overlay window positioning directly assigns DPI-unaware coordinates from GetDpiUnawareScreens() to WPF window properties (Top/Left at lines 498-499). Since the Editor is PerMonitorV2 DPI-aware, WPF double-scales these coordinates causing the overlay to draw in the wrong position. The fix requires using SetWindowPos wrapped in SetThreadDpiAwarenessContext (like MonitorHelper does) to position windows with DPI-unaware coordinates, instead of assigning to WPF properties.

CITED_FIX_PR: #45183

CONFIDENCE: high

USED_SKILL: The skill's DPI regression playbook identified this exact issue (#45174), documenting that stored DPI-unaware coords must bypass WPF's automatic scaling via SetWindowPositionDpiUnaware helper to prevent double-scaling.
