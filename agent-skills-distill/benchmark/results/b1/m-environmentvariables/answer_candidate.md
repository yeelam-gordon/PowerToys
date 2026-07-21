# Bug Localization Answer: EnvironmentVariables

**CULPRIT_FILES:**
1. `src/modules/EnvironmentVariables/EnvironmentVariables/EnvironmentVariablesXAML/MainWindow.xaml.cs` (primary)

**CULPRIT_FUNCTIONS:**
- `MainWindow()` constructor (lines 23-44, specifically line 33)

**FIX:**
When running as administrator, `ResourceLoader.GetString("WindowAdminTitle")` can return an empty string if the resource map fails to resolve, and WinUI's TitleBar faults on an empty `AppWindow.Title`, crashing the app. Add a non-empty fallback before `SetTitleBar()`: if the loaded title is empty or null, default to a hardcoded non-empty string like "Environment Variables" or "PowerToys Environment Variables".

**CITED_FIX_PR:**
#49069

**CONFIDENCE:**
high

**USED_SKILL:**
The skill's playbook "Crash immediately when launched as Administrator" matched the symptom exactly, identifying that `ResourceLoader.GetString` for `WindowAdminTitle` can return empty string in elevated mode, causing a native window title fault. The guardrail is to never leave the native window Title empty—fall back to a non-empty product name.
