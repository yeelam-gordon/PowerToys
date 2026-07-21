# Bug to fix: "Something went wrong" after waking from sleep.

(Module: fancyzones. The repository is checked out at commit `4771f15b6c2629c81df37664de55527ae275f034` — the bug is present and UNFIXED. Do NOT look for the fix in git history; it does not exist yet at this checkout.)

## Symptom / report

### Microsoft PowerToys version

0.94.0

### Installation method

PowerToys auto-update

### Area(s) with issue?

General

### Steps to reproduce

Computer woke up from sleep and error message was present.

### ✔️ Expected Behavior

N/A

### ❌ Actual Behavior

N/A

### Upload Bug Report ZIP-file

[PowerToysReport_2026-06-12-08-15-18.zip](https://github.com/user-attachments/files/28878738/PowerToysReport_2026-06-12-08-15-18.zip)

### Additional Information



Version: 0.94.0.0
OS Version: Microsoft Windows NT 10.0.26200.0
IntPtr Length: 8
x64: True
Date: 6/12/2026 2:19:10 AM
Exception:
System.Reflection.TargetInvocationException: Exception has been thrown by the target of an invocation.
 ---> System.Runtime.InteropServices.COMException (0x80263001): {Desktop composition is disabled} The operation could not be completed because desktop composition is disabled. (0x80263001)
   at Standard.NativeMethods.DwmExtendFrameIntoClientArea(IntPtr hwnd, MARGINS& pMarInset)
   at System.Windows.Appearance.WindowBackdropManager.UpdateGlassFrame(IntPtr hwnd, WindowBackdropType backdropType)
   at System.Windows.Appearance.WindowBackdropManager.ApplyBackdrop(IntPtr hwnd, WindowBackdropType backdropType)
   at System.Windows.Appearance.WindowBackdropManager.SetBackdrop(Window window, WindowBackdropType backdropType)
   at System.Windows.ThemeManager.ApplyStyleOnWindow(Window window, Boolean useLightColors)
   at System.Windows.ThemeManager.OnApplicationThemeChanged(ThemeMode oldThemeMode, ThemeMode newThemeMode)
   at PowerLauncher.Helper.ThemeManager.SetSystemTheme(Theme theme)
   at PowerLauncher.Helper.ThemeManager.<>c__DisplayClass14_0.<UpdateTheme>b__0()
   at System.Windows.Threading.Dispatcher.Invoke(Action callback, DispatcherPriority priority, CancellationToken cancellationToken, TimeSpan timeout)
   at System.Windows.Threading.Dispatcher.Invoke(Action callback)
   at PowerLauncher.Helper.ThemeManager.UpdateTheme()
   at PowerLauncher.Helper.ThemeManager.OnUserPreferenceChanged(Object sender, UserPreferenceChangedEventArgs e)
   at InvokeStub_UserPreferenceChangedEventHandler.Invoke(Object, Span`1)
   at System.Reflection.MethodBaseInvoker.InvokeWithFewArgs(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
   --- End of inner exception stack trace ---
   at System.Reflection.MethodBaseInvoker.InvokeWithFewArgs(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
   at System.Reflection.RuntimeMethodInfo.Invoke(Object obj, BindingFlags invokeAttr, Binder binder, Object[] parameters, CultureInfo culture)
   at System.Delegate.DynamicInvokeImpl(Object[] args)
   at Microsoft.Win32.SystemEvents.SystemEventInvokeInfo.InvokeCallback(Object arg)
   at System.Windows.Threading.ExceptionWrapper.InternalRealCall(Delegate callback, Object args, Int32 numArgs)
   at System.Windows.Threading.ExceptionWrapper.TryCatchWhen(Object source, Delegate callback, Object args, Int32 numArgs, Delegate catchHandler)



### Other Software

_No response_

## Your task

1. Identify the culprit file(s) and function(s) that must change.
2. Describe the fix (what to change and why).
3. If you can, cite the historical PR/commit that fixed this.

Working tree: `C:\s\Demo\SkillForDistill\benchmark\results\b1\48542\worktree`
