# PR #46593 — [QuickAccent] Fix UI glitches, DPI-related issues, selection bugs, and add hardware shift key state fallback

Base: main  Head: fix-quick-accent-ui

## Description

## Summary of the Pull Request
This PR fixes several issues around the popup selection window's size and position, selection-related issues which result in flashing or glitching, and includes more reliable detection of the Shift key.

<!-- Please review the items on the PR checklist before submitting-->
## PR Checklist

- [x] Closes: #44332
- [x] Closes: #44980
- [x] Closes: #35094 
- [x] Closes: #40498
- [ ] **Communication:** I've discussed this with core contributors already. If the work hasn't been agreed, this work might be rejected
- [ ] **Tests:** Added/updated and all pass
- [ ] **Localization:** All end-user-facing strings can be localized
- [ ] **Dev docs:** Added/updated
- [ ] **New binaries:** Added on the required places
   - [ ] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json) for new binaries
   - [ ] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs) for new binaries and localization folder
   - [ ] [YML for CI pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml) for new test projects
   - [ ] [YML for signed pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/release.yml)
- [ ] **Documentation updated:** If checked, please file a pull request on [our docs repo](https://github.com/MicrosoftDocs/windows-uwp/tree/docs/hub/powertoys) and link it here: #xxx

<!-- Provide a more detailed description of the PR, other things fixed, or any additional comments/features here -->
## Detailed Description of the Pull Request / Additional comments

This PR includes fixes for the Quick Accent's selection window position, its width measurement, and letter selection-related issues. In addition, glitches such as the window flashing the selection colour and the window appearing blank should be reduced or eliminated entirely.

### Popup width bug

When opening Quick Accent from a letter with many mappings, it would appear too wide for the display. Even though letters could be selected, they may be entirely off-screen:

<img width="1578" height="134" alt="image" src="https://github.com/user-attachments/assets/cfcb2ddb-3cf3-47d5-9386-133a2fc70550" />

This was because of this flaw in `GetDisplayMaxWidth`, which is used directly by the popup to set the maximum width of the characters area:

```csharp
    // In Selector.xaml.cs
    private void SetWindowsSize()
    {
        this.characters.MaxWidth = _powerAccent.GetDisplayMaxWidth();
    }

...
    // In PowerAccent.cs
    public double GetDisplayMaxWidth()
    {
        return WindowsFunctions.GetActiveDisplay().Size.Width - ScreenMinPadding;
    }
```

`GetActiveDisplay` uses the `GetMonitorInfo` API, which exposes the working area of the display. It returns its values in _raw unscaled pixel_ values:

```csharp
    public static (Point Location, Size Size, double Dpi) 

## Changed files (unified diff)

### src/modules/poweraccent/PowerAccent.Core/NativeMethods.txt  (+3/-2)
```diff
@@ -1,6 +1,7 @@
-GetDpiForWindow
 GetGUIThreadInfo
 GetKeyState
 GetMonitorInfo
 MonitorFromWindow
-SendInput
\ No newline at end of file
+SendInput
+GetAsyncKeyState
+GetDpiForMonitor
\ No newline at end of file
```
### src/modules/poweraccent/PowerAccent.Core/PowerAccent.cs  (+48/-13)
```diff
@@ -5,7 +5,6 @@
 using System.Globalization;
 using System.Text;
 using System.Unicode;
-using System.Windows;
 
 using ManagedCommon;
 using PowerAccent.Core.Services;
@@ -27,6 +26,7 @@ public partial class PowerAccent : IDisposable
     private string[] _characterDescriptions = Array.Empty<string>();
     private int _selectedIndex = -1;
     private bool _showUnicodeDescription;
+    private bool _initialShiftState; // Was shift held down when the toolbar was summoned?
 
     public LetterKey[] LetterKeysShowingDescription => _letterKeysShowingDescription;
 
@@ -95,6 +95,7 @@ private void SetEvents()
 
     private void ShowToolbar(LetterKey letterKey)
     {
+        _initialShiftState = WindowsFunctions.IsShiftState();
         _visible = true;
 
         _characters = GetCharacters(letterKey);
@@ -240,21 +241,30 @@ private void SendInputAndHideToolbar(InputType inputType)
 
     private void ProcessNextChar(TriggerKey triggerKey, bool shiftPressed)
     {
+        // Use an async hardware check as a fallback in case the keyboard hook misses a
+        // quick Shift press. If the popup was opened while holding Shift (e.g., typing a
+        // capital letter), ignore the hardware check so we don't accidentally trigger a
+        // backwards navigation.
+        bool isHardwareShiftPressed = WindowsFunctions.IsShiftState() && !_initialShiftState;
+        shiftPressed = shiftPressed || isHardwareShiftPressed;
+
         if (_visible && _selectedIndex == -1)
         {
-            if (triggerKey == TriggerKey.Left)
+            if (triggerKey == TriggerKey.Space)
             {
-                _selectedIndex = (_characters.Length / 2) - 1;
+                _selectedIndex = shiftPressed ? (_characters.Length - 1) : 0;
             }
-
-            if (triggerKey == TriggerKey.Right)
+            else if (_settingService.StartSelectionFromTheLeft)
             {
-                _selectedIndex = _characters.Length / 2;
+                _selectedIndex = 0;
             }
-
-            if (triggerKey == TriggerKey.Space || _settingService.StartSelectionFromTheLeft)
+            else if (triggerKey == TriggerKey.Left)
             {
-                _selectedIndex = 0;
+                _selectedIndex = (_characters.Length / 2) - 1;
+            }
+            else if (triggerKey == TriggerKey.Right)
+            {
+                _selectedIndex = _characters.Length / 2;
             }
 
             if (_selectedIndex < 0)
@@ -321,22 +331,47 @@ private void ProcessNextChar(TriggerKey triggerKey, bool shiftPressed)
         OnSelectCharacter?.Invoke(_selectedIndex, _characters[_selectedIndex]);
     }
 
+    /// <summary>
+    /// Calculates the coordinates at which a window of the specified size should be
+    /// displayed, based on the current display settings and user preferences.
+    /// </summary>
+    /// <remarks>The calculated coordinates take into account the active display's
+    /// location, size, DPI, and the user's configured position preferences.</remarks>
+    /// <param name="window">The size of the window for which to calculate display
+    /// coordinates.</param>
+    /// <returns>A point representing the top-left coordinates where the window should be
+    /// positioned on the active display, in physical/raw coordinates suitable for Win32
+    /// APIs like SetWindowPos.</returns>
     public Point GetDisplayCoordinates(Size window)
     {
         (Point Location, Size Size, double Dpi) activeDisplay = WindowsFunctions.GetActiveDisplay();
         Rect screen = new(activeDisplay.Location, activeDisplay.Size);
         Position position = _settingService.Position;
 
-        /* Debug.WriteLine("Dpi: " + activeDisplay.Dpi); */
-
-        return Calculation.GetRawCoordinatesFromPosition(position, screen, window, activeDisplay.Dpi) / activeDisplay.Dpi;
+        return Calculation.GetRawCoordinatesFromPosition(position, screen, window, activeDisplay.Dpi);
     }
 
+    /// <summary>
+    /// Gets the maximum width for the toolbar display based on the active screen
+    /// dimensions.
+    /// </summary>
+    /// <returns>The maximum width in logical pixels, accounting for screen padding.
+    /// </returns>
     public double GetDisplayMaxWidth()
     {
-        return WindowsFunctions.GetActiveDisplay().Size.Width - ScreenMinPadding;
+        // Note: activeDisplay.Size.Width is in raw physical pixels.
+        // We divide by DPI to convert to WPF logical pixels (Device-Independent Pixels),
+        // because ScreenMinPadding is a logical pixel value and WPF MaxWidth expects
+        // logical pixels.
+        var activeDisplay = WindowsFunctions.GetActiveDisplay();
+        return (activeDisplay.Size.Width / activeDisplay.Dpi) - ScreenMinPadding;
     }
 
+    /// <summary>
+    /// Gets the user-configured position preference for the toolbar display. For example
+    /// <see cref="Position.TopLeft"/>.
+    /// </summary>
+    /// <returns>The preferred location for the toolbar.</returns>
     public Position GetToolbarPosition()
     {
         return _settingService.Position;
```
### src/modules/poweraccent/PowerAccent.Core/Tools/WindowsFunctions.cs  (+19/-12)
```diff
@@ -6,6 +6,7 @@
 
 using Windows.Win32;
 using Windows.Win32.Graphics.Gdi;
+using Windows.Win32.UI.HiDpi;
 using Windows.Win32.UI.Input.KeyboardAndMouse;
 using Windows.Win32.UI.WindowsAndMessaging;
 
@@ -51,36 +52,36 @@ public static void Insert(string s, bool back = false)
                 Thread.Sleep(1); // Some apps, like Terminal, need a little wait to process the sent backspace or they'll ignore it.
             }
 
-            foreach (char c in s)
+            if (s.Length > 0)
             {
-                // Letter
-                var inputsInsert = new INPUT[]
+                var inputsInsert = new INPUT[s.Length * 2];
+                for (int i = 0; i < s.Length; i++)
                 {
-                    new INPUT
+                    inputsInsert[i * 2] = new INPUT
                     {
                         type = INPUT_TYPE.INPUT_KEYBOARD,
                         Anonymous = new INPUT._Anonymous_e__Union
                         {
                             ki = new KEYBDINPUT
                             {
-                                wScan = c,
+                                wScan = s[i],
                                 dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE,
                             },
                         },
-                    },
-                    new INPUT
+                    };
+                    inputsInsert[(i * 2) + 1] = new INPUT
                     {
                         type = INPUT_TYPE.INPUT_KEYBOARD,
                         Anonymous = new INPUT._Anonymous_e__Union
                         {
                             ki = new KEYBDINPUT
                             {
-                                wScan = c,
+                                wScan = s[i],
                                 dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_UNICODE | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
                             },
                         },
-                    },
-                };
+                    };
+                }
 
                 _ = PInvoke.SendInput(inputsInsert, Marshal.SizeOf<INPUT>());
             }
@@ -98,7 +99,13 @@ public static (Point Location, Size Size, double Dpi) GetActiveDisplay()
         monitorInfo.cbSize = (uint)Marshal.SizeOf(monitorInfo);
         PInvoke.GetMonitorInfo(res, ref monitorInfo);
 
-        double dpi = PInvoke.GetDpiForWindow(guiInfo.hwndActive) / 96d;
+        uint dpiRaw = 96; // Safe default
+        if (PInvoke.GetDpiForMonitor(res, MONITOR_DPI_TYPE.MDT_EFFECTIVE_DPI, out uint dpiX, out _) == 0)
+        {
+            dpiRaw = dpiX;
+        }
+
+        double dpi = dpiRaw / 96d;
         var location = new Point(monitorInfo.rcWork.left, monitorInfo.rcWork.top);
         return (location, monitorInfo.rcWork.Size, dpi);
     }
@@ -111,7 +118,7 @@ public static bool IsCapsLockState()
 
     public static bool IsShiftState()
     {
-        var shift = PInvoke.GetKeyState((int)VIRTUAL_KEY.VK_SHIFT);
+        var shift = PInvoke.GetAsyncKeyState((int)VIRTUAL_KEY.VK_SHIFT);
         return shift < 0;
     }
 }
```
### src/modules/poweraccent/PowerAccent.UI/NativeMethods.txt  (+2/-0)
```diff
@@ -0,0 +1,2 @@
+﻿SetWindowPos
+GetSystemMetrics
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccent.UI.csproj  (+7/-0)
```diff
@@ -24,6 +24,13 @@
 		</Resource>
 	</ItemGroup>
 
+	<ItemGroup>
+		<PackageReference Include="Microsoft.Windows.CsWin32">
+			<PrivateAssets>all</PrivateAssets>
+			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
+		</PackageReference>
+	</ItemGroup>
+
 	<ItemGroup>
 		<ProjectReference Include="..\..\..\common\Common.UI\Common.UI.csproj" />
 		<ProjectReference Include="..\..\..\common\interop\PowerToys.Interop.vcxproj" />
```
### src/modules/poweraccent/PowerAccent.UI/Selector.xaml  (+12/-4)
```diff
@@ -12,6 +12,7 @@
     DataContext="{Binding RelativeSource={RelativeSource Self}}"
     ResizeMode="NoResize"
     ShowInTaskbar="False"
+    SizeChanged="Window_SizeChanged"
     SizeToContent="WidthAndHeight"
     Visibility="Collapsed"
     WindowStyle="None"
@@ -53,16 +54,19 @@
                 HorizontalContentAlignment="Stretch"
                 VerticalContentAlignment="Stretch"
                 Background="Transparent"
-                IsHitTestVisible="False">
+                Focusable="False"
+                IsHitTestVisible="False"
+                ScrollViewer.HorizontalScrollBarVisibility="Auto">
                 <ListBox.ItemContainerStyle>
                     <Style TargetType="ListBoxItem">
+                        <Setter Property="Focusable" Value="False" />
                         <Setter Property="ContentTemplate" Value="{StaticResource DefaultKeyTemplate}" />
                         <Setter Property="Template">
                             <Setter.Value>
                                 <ControlTemplate TargetType="{x:Type ListBoxItem}">
                                     <Grid
-                                        Width="48"
                                         Height="48"
+                                        MinWidth="48"
                                         Margin="0"
                                         HorizontalAlignment="Center"
                                         VerticalAlignment="Center"
@@ -93,23 +97,27 @@
                 </ListBox.ItemContainerStyle>
                 <ListBox.ItemsPanel>
                     <ItemsPanelTemplate>
-                        <VirtualizingStackPanel IsItemsHost="False" Orientation="Horizontal" />
+                        <StackPanel Orientation="Horizontal" />
                     </ItemsPanelTemplate>
                 </ListBox.ItemsPanel>
             </ListBox>
 
             <Grid
                 Grid.Row="1"
                 MinWidth="600"
+                MaxWidth="{Binding ActualWidth, ElementName=characters}"
                 Background="{DynamicResource LayerOnAcrylicFillColorDefaultBrush}"
                 Visibility="{Binding CharacterNameVisibility, UpdateSourceTrigger=PropertyChanged}">
                 <TextBlock
                     x:Name="characterName"
+                    MaxHeight="36"
                     Margin="8"
                     FontSize="12"
                     Foreground="{DynamicResource TextFillColorSecondaryBrush}"
                     Text="(U+0000) A COOL LETTER NAME COMES HERE"
-                    TextAlignment="Center" />
+                    TextAlignment="Center"
+                    TextTrimming="CharacterEllipsis"
+                    TextWrapping="Wrap" />
                 <Rectangle
                     Height="1"
                     HorizontalAlignment="Stretch"
```
### src/modules/poweraccent/PowerAccent.UI/Selector.xaml.cs  (+83/-11)
```diff
@@ -5,19 +5,26 @@
 using System;
 using System.ComponentModel;
 using System.Windows;
-
+using Windows.Win32;
+using Windows.Win32.Foundation;
+using Windows.Win32.UI.WindowsAndMessaging;
 using Point = PowerAccent.Core.Point;
 using Size = PowerAccent.Core.Size;
 
 namespace PowerAccent.UI;
 
 public partial class Selector : Window, IDisposable, INotifyPropertyChanged
 {
+    // When setting the position for the selector window, we do not alter the z-order,
+    // activation status, or size.
+    private const SET_WINDOW_POS_FLAGS WindowPosFlags =
+        SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
+
     private readonly Core.PowerAccent _powerAccent = new();
 
     private Visibility _characterNameVisibility = Visibility.Visible;
 
-    private int _selectedIndex;
+    private int _selectedIndex = -1;
 
     public event PropertyChangedEventHandler PropertyChanged;
 
@@ -54,8 +61,16 @@ private void PowerAccent_OnSelectionCharacter(int index, string character)
     {
         _selectedIndex = index;
         characters.SelectedIndex = _selectedIndex;
-        characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
-        characters.ScrollIntoView(character);
+
+        if (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
+        {
+            characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
+        }
+
+        if (characters.Items.Count > _selectedIndex && _selectedIndex >= 0)
+        {
+            characters.ScrollIntoView(characters.Items[_selectedIndex]);
+        }
     }
 
     private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
@@ -67,17 +82,50 @@ private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
 
         if (isActive)
         {
+            int offscreenX = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN) - 1000;
+            int offscreenY = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN) - 1000;
+
+            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
+            if (hwnd != IntPtr.Zero)
+            {
+                // Move off-screen to avoid flicker on previous monitor before Show() and
+                // UpdateLayout().
+                PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, offscreenX, offscreenY, 0, 0, WindowPosFlags);
+            }
+            else
+            {
+                this.Left = offscreenX;
+                this.Top = offscreenY;
+            }
+
+            Show();
+            SetWindowsSize();
             characters.ItemsSource = chars;
-            characters.SelectedIndex = _selectedIndex;
+            characters.SelectedIndex = -1; // Reset before setting dynamically to avoid flashing
+
             this.UpdateLayout(); // Required for filling the actual width/height before positioning.
-            SetWindowsSize();
+
+            characters.SelectedIndex = _selectedIndex;
+
+            if (_selectedIndex >= 0 && _selectedIndex < chars.Length)
+            {
+                characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
+                characters.ScrollIntoView(characters.Items[_selectedIndex]);
+                this.UpdateLayout(); // Re-layout after scrolling
+            }
+            else
+            {
+                characterName.Text = string.Empty;
+            }
+
             SetWindowPosition();
-            Show();
             Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new PowerAccent.Core.Telemetry.PowerAccentShowAccentMenuEvent());
         }
         else
         {
             Hide();
+            characters.ItemsSource = null;
+            _selectedIndex = -1;
         }
     }
 
@@ -89,14 +137,38 @@ private void MenuExit_Click(object sender, RoutedEventArgs e)
     private void SetWindowPosition()
     {
         Size windowSize = new(((FrameworkElement)Application.Current.MainWindow.Content).ActualWidth, ((FrameworkElement)Application.Current.MainWindow.Content).ActualHeight);
-        Point position = _powerAccent.GetDisplayCoordinates(windowSize);
-        this.Left = position.X;
-        this.Top = position.Y;
+        Point physicalPosition = _powerAccent.GetDisplayCoordinates(windowSize);
+
+        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
+        if (hwnd != IntPtr.Zero)
+        {
+            PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, (int)Math.Round(physicalPosition.X), (int)Math.Round(physicalPosition.Y), 0, 0, WindowPosFlags);
+        }
+    }
+
+    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
+    {
+        base.OnDpiChanged(oldDpi, newDpi);
+        if (this.Visibility == Visibility.Visible)
+        {
+            SetWindowsSize();
+            SetWindowPosition();
+        }
     }
 
     private void SetWindowsSize()
     {
-        this.characters.MaxWidth = _powerAccent.GetDisplayMaxWidth();
+        double maxWidth = _powerAccent.GetDisplayMaxWidth();
+        this.characters.MaxWidth = maxWidth;
+        this.MaxWidth = maxWidth;
+    }
+
+    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
+    {
+        if (this.Visibility == Visibility.Visible)
+        {
+            SetWindowPosition();
+        }
     }
 
     protected override void OnClosed(EventArgs e)
```