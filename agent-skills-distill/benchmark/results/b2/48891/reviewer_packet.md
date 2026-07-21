# PR #48891 — [Quick Accent] Migrate UI to WinUI 3

Base: main  Head: yuleng/m/qa/1

## Description

## Summary of the Pull Request

Migrates the **Quick Accent (PowerAccent)** module's UI from WPF (`System.Windows.*`) to **WinUI 3 (Windows App SDK)**, following the pattern used by other migrated modules (ImageResizer, PowerDisplay). The accent selector is now a self-contained WinUI 3 app (`PowerToys.PowerAccent.exe`) shipped under `WinUI3Apps`, and `PowerAccent.Core` is UI-framework-agnostic.


demo:
https://github.com/user-attachments/assets/400c33ee-0fc0-491e-841b-a546438edf91


## PR Checklist

- [x] Closes: #48889
- [x] **Communication:** Tracked task (#48889) agreed with core contributors
- [x] **Tests:** Added/updated and all pass — new `PowerAccent.Core.UnitTests` (21 tests for the positioning / DPI math); existing `PowerAccent.Common.UnitTests` unaffected
- [x] **Localization:** No new localizable end-user strings — new accessibility metadata uses non-localized `AutomationProperties.AutomationId`, and the window title is the brand name `"Quick Accent"` (literal, matching ColorPicker)
- [x] **Dev docs:** Updated `doc/devdocs/modules/quickaccent.md`
- [x] **New binaries:** Added on the required places
   - [x] [JSON for signing](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ESRPSigning_core.json): the `WinUI3Apps\` PowerAccent payloads are listed in `ESRPSigning_core.json`
   - [x] [WXS for installer](https://github.com/microsoft/PowerToys/blob/main/installer/PowerToysSetup/Product.wxs): no manual `Product.wxs` entry — the self-contained `WinUI3Apps` output (exe + `.pri` + Windows App SDK runtime) is harvested by the `WinUI3ApplicationsFiles` glob (same as ImageResizer)
   - [x] [YML for CI pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/ci/templates/build-powertoys-steps.yml): no change needed — `PowerAccent.Core.UnitTests` is discovered by the existing `**\*UnitTest*.dll` VSTest glob
   - [x] [YML for signed pipeline](https://github.com/microsoft/PowerToys/blob/main/.pipelines/release.yml): covered by `ESRPSigning_core.json`; no `release.yml` change needed
- [ ] **Documentation updated:** N/A — internal UI-framework migration with no user-facing behavior change

## Detailed Description of the Pull Request / Additional comments

The migration spans three areas (tracked in #48889):

**UI (WPF → WinUI 3)**
- `PowerAccent.UI` is now a WinUI 3 app shell (custom `Program.Main`, `WindowsPackageType=None`, `WindowsAppSDKSelfContained=true`).
- The accent selector is a non-activating `TransparentWindow` overlay shown with `SW_SHOWNA` (never steals focus). It is made always-on-top only while shown — the WinUIEx `WindowEx.IsAlwaysOnTop` property is toggled `true` on show / `false` on hide in `OnChangeDisplay` (matching the WPF original's `Topmost = isActive`), so the dormant, never-destroyed overlay does not pin a discrete GPU awake on hybrid-graphics laptops (issue #34849 / PR #41044).
- The accent "pill" selection visual is reproduced with `VisualStateManager` (WinUI 3 has no `Sty

## Changed files (unified diff)

### .pipelines/ESRPSigning_core.json  (+5/-5)
```diff
@@ -211,12 +211,12 @@
             "WinUI3Apps\\NewPlusPackage.msix",
             "WinUI3Apps\\PowerToys.NewPlus.ShellExtension.win10.dll",
 
-            "PowerAccent.Core.dll",
-            "PowerAccent.Common.dll",
-            "PowerToys.PowerAccent.dll",
-            "PowerToys.PowerAccent.exe",
+            "WinUI3Apps\\PowerAccent.Core.dll",
+            "WinUI3Apps\\PowerAccent.Common.dll",
+            "WinUI3Apps\\PowerToys.PowerAccent.dll",
+            "WinUI3Apps\\PowerToys.PowerAccent.exe",
             "PowerToys.PowerAccentModuleInterface.dll",
-            "PowerToys.PowerAccentKeyboardService.dll",
+            "WinUI3Apps\\PowerToys.PowerAccentKeyboardService.dll",
 
             "PowerToys.PowerDisplayModuleInterface.dll",
             "WinUI3Apps\\PowerToys.PowerDisplay.dll",
```
### PowerToys.slnx  (+4/-0)
```diff
@@ -806,6 +806,10 @@
       <Platform Solution="*|ARM64" Project="ARM64" />
       <Platform Solution="*|x64" Project="x64" />
     </Project>
+    <Project Path="src/modules/poweraccent/PowerAccent.Core.UnitTests/PowerAccent.Core.UnitTests.csproj">
+      <Platform Solution="*|ARM64" Project="ARM64" />
+      <Platform Solution="*|x64" Project="x64" />
+    </Project>
     <Project Path="src/modules/poweraccent/PowerAccent.Common/PowerAccent.Common.csproj">
       <Platform Solution="*|ARM64" Project="ARM64" />
       <Platform Solution="*|x64" Project="x64" />
```
### doc/devdocs/modules/quickaccent.md  (+28/-16)
```diff
@@ -15,14 +15,15 @@ Quick Accent (formerly known as Power Accent) is a PowerToys module that allows
 
 ## Architecture
 
-The Quick Accent module consists of four main components:
+The Quick Accent module consists of five projects:
 
 ```
 poweraccent/
-├── PowerAccent.Core/               # Core component containing Language Sets
-├── PowerAccent.UI/                 # The character selector UI
-├── PowerAccentKeyboardService/     # Keyboard Hook
-└── PowerAccentModuleInterface/     # DLL interface
+├── PowerAccent.Common/             # Language data, character mappings, LetterKey enum
+├── PowerAccent.Core/               # Accent logic, settings, positioning, usage statistics
+├── PowerAccent.UI/                 # WinUI 3 character selector app (PowerToys.PowerAccent.exe)
+├── PowerAccentKeyboardService/     # WinRT keyboard-hook component
+└── PowerAccentModuleInterface/     # Native runner module DLL
 ```
 
 ### Module Interface (PowerAccentModuleInterface)
@@ -32,21 +33,32 @@ The Module Interface, implemented in `PowerAccentModuleInterface/dllmain.cpp`, i
 - Managing module lifecycle (enable/disable/settings)
 - Launching and terminating the PowerToys.PowerAccent.exe process
 
+### Shared Data (PowerAccent.Common)
+
+`PowerAccent.Common` holds the UI- and runtime-agnostic data the other projects share:
+- The language / character-set definitions and per-letter accent mappings
+- The managed `LetterKey` enum (kept in sync with the WinRT `LetterKey` in `PowerAccentKeyboardService/KeyboardListener.idl`)
+
+It has no UI or WinRT dependencies and is unit-tested in isolation (`PowerAccent.Common.UnitTests`).
+
 ### Core Logic (PowerAccent.Core)
 
 The Core component contains:
-- Main accent character logic
-- Keyboard input detection
-- Character mappings for different languages
-- Management of language sets and special characters (currency, math symbols, etc.)
-- Usage statistics for frequently used characters
+- Main accent character logic, consuming the language data from `PowerAccent.Common`
+- Toolbar positioning math (9 anchor points with per-monitor DPI) and settings handling
+- Management of special characters (currency, math symbols, etc.) and usage statistics
+
+Core carries no UI-framework dependency: it raises events and accepts a UI-thread marshaller delegate instead of touching WPF/WinUI directly, and its positioning math is covered by `PowerAccent.Core.UnitTests`.
 
 ### UI Layer (PowerAccent.UI)
 
-The UI component is responsible for:
-- Displaying the toolbar with accent options
-- Handling user selection of accented characters
-- Managing the visual positioning of the toolbar
+The UI component is a self-contained **WinUI 3 (Windows App SDK)** app, migrated from WPF.
+It is responsible for:
+- Displaying the accent toolbar — a non-activating, always-on-top `TransparentWindow` overlay shown with `SW_SHOWNA` so it never steals focus from the app being typed into
+- Handling selection and the toolbar's sizing / positioning
+- Following the system theme while the long-lived process runs
+
+It builds to `PowerToys.PowerAccent.exe` together with its `.pri` and the bundled Windows App SDK runtime, all under the `WinUI3Apps` output folder.
 
 ### Keyboard Service (PowerAccentKeyboardService)
 
@@ -128,5 +140,5 @@ To directly debug the Quick Accent UI component:
 5. Start debugging by pressing `F5` or clicking the "*Start*" button
 6. Verify that the debugger breaks at your breakpoint and you can inspect variables and step through code
 
-**Known issue**: You may encounter approximately 78 errors during the start of debugging.<br>
-**Solution**: If you encounter errors, right-click on the **PowerAccent** folder in Solution Explorer and select "*Rebuild*". After rebuilding, start debugging again.
+**Known issue**: A first incremental build can surface transient errors (for example from CsWinRT projection / WinUI XAML codegen ordering).<br>
+**Solution**: Right-click the **PowerAccent** folder in Solution Explorer and select "*Rebuild*", then start debugging again.
```
### src/modules/poweraccent/PowerAccent.Common/PowerAccent.Common.csproj  (+5/-0)
```diff
@@ -1,5 +1,6 @@
 <Project Sdk="Microsoft.NET.Sdk">
   <!-- Look at Directory.Build.props in root for common stuff as well -->
+  <Import Project="$(RepoRoot)src\Common.Dotnet.AotCompatibility.props" />
 
   <PropertyGroup>
     <!-- Currently hard-coded, as this project does not target WinRT.
@@ -8,6 +9,10 @@
     <TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
     <ImplicitUsings>enable</ImplicitUsings>
     <Nullable>disable</Nullable>
+    <!-- Required by the CsWinRT AOT optimizer: marshaling generic collections (e.g. the
+         Dictionary<Language, LanguageInfo> in CharacterMappings) across the WinRT ABI emits
+         unsafe code. Matches the sibling AOT-compatible library PowerDisplay.Lib. -->
+    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
   </PropertyGroup>
 
 </Project>
```
### src/modules/poweraccent/PowerAccent.Core.UnitTests/CalculationTests.cs  (+148/-0)
```diff
@@ -0,0 +1,148 @@
+// Copyright (c) Microsoft Corporation
+// The Microsoft Corporation licenses this file to you under the MIT license.
+// See the LICENSE file in the project root for more information.
+
+using Microsoft.VisualStudio.TestTools.UnitTesting;
+using PowerAccent.Core;
+using PowerAccent.Core.Services;
+using PowerAccent.Core.Tools;
+
+namespace PowerAccent.Core.UnitTests;
+
+/// <summary>
+/// Exercises the pure anchor / DPI geometry in <see cref="Calculation"/>. These are the math that
+/// the WinUI 3 Selector feeds into AppWindow.Move/Resize, so a regression here silently mis-places
+/// the accent popup (the classic high-DPI / multi-monitor "double scaling" failure mode).
+/// </summary>
+[TestClass]
+public sealed class CalculationTests
+{
+    // offset baked into Calculation: the gap from the screen edge for the edge anchors.
+    private const int Offset = 24;
+
+    // A 1920x1080 primary monitor rooted at the virtual-desktop origin.
+    private static readonly Rect PrimaryScreen = new(0, 0, 1920, 1080);
+
+    // A one-row accent bar, in DIP.
+    private static readonly Size Window = new(200, 52);
+
+    // At 100% scaling (dpi = 1.0) the physical window size equals the DIP size, so each of the nine
+    // anchors lands at an easily hand-checkable coordinate.
+    [DataTestMethod]
+    [DataRow(Position.TopLeft, 24.0, 24.0)]
+    [DataRow(Position.Top, 860.0, 24.0)]
+    [DataRow(Position.TopRight, 1696.0, 24.0)]
+    [DataRow(Position.Left, 24.0, 514.0)]
+    [DataRow(Position.Center, 860.0, 514.0)]
+    [DataRow(Position.Right, 1696.0, 514.0)]
+    [DataRow(Position.BottomLeft, 24.0, 1004.0)]
+    [DataRow(Position.Bottom, 860.0, 1004.0)]
+    [DataRow(Position.BottomRight, 1696.0, 1004.0)]
+    public void GetRawCoordinatesFromPosition_AtDpi1_PlacesEachAnchor(Position position, double expectedX, double expectedY)
+    {
+        var point = Calculation.GetRawCoordinatesFromPosition(position, PrimaryScreen, Window, dpi: 1.0);
+
+        Assert.AreEqual(expectedX, point.X, "X for " + position);
+        Assert.AreEqual(expectedY, point.Y, "Y for " + position);
+    }
+
+    // At 150% scaling the physical window is 300x78. The centered anchors must subtract HALF of the
+    // scaled size (not the DIP size) and the right/bottom anchors must subtract the FULL scaled size
+    // plus the offset - this is exactly where a missing/extra dpi factor shows up.
+    [DataTestMethod]
+    [DataRow(Position.TopLeft, 24.0, 24.0)]
+    [DataRow(Position.Center, 810.0, 501.0)]
+    [DataRow(Position.BottomRight, 1596.0, 978.0)]
+    public void GetRawCoordinatesFromPosition_AtDpi150Percent_ScalesWindowFootprint(Position position, double expectedX, double expectedY)
+    {
+        var point = Calculation.GetRawCoordinatesFromPosition(position, PrimaryScreen, Window, dpi: 1.5);
+
+        Assert.AreEqual(expectedX, point.X, "X for " + position);
+        Assert.AreEqual(expectedY, point.Y, "Y for " + position);
+    }
+
+    // A secondary 2560x1440 monitor to the right of the primary at 200% scaling. Verifies the screen
+    // origin (screen.X / screen.Y) is honored for every anchor, not just the primary-at-origin case.
+    [DataTestMethod]
+    [DataRow(Position.TopLeft, 1944.0, 24.0)]
+    [DataRow(Position.Center, 3000.0, 668.0)]
+    [DataRow(Position.BottomRight, 4056.0, 1312.0)]
+    public void GetRawCoordinatesFromPosition_OnOffsetMonitor_HonorsScreenOrigin(Position position, double expectedX, double expectedY)
+    {
+        var secondaryScreen = new Rect(1920, 0, 2560, 1440);
+
+        var point = Calculation.GetRawCoordinatesFromPosition(position, secondaryScreen, Window, dpi: 2.0);
+
+        Assert.AreEqual(expectedX, point.X, "X for " + position);
+        Assert.AreEqual(expectedY, point.Y, "Y for " + position);
+    }
+
+    // A monitor positioned to the LEFT of the primary has a negative virtual-desktop X origin. The
+    // edge anchors must still be offset relative to that negative origin.
+    [TestMethod]
+    public void GetRawCoordinatesFromPosition_OnNegativeOriginMonitor_OffsetsFromScreenEdge()
+    {
+        var leftScreen = new Rect(-1920, 0, 1920, 1080);
+
+        var topLeft = Calculation.GetRawCoordinatesFromPosition(Position.TopLeft, leftScreen, Window, dpi: 1.0);
+        Assert.AreEqual(-1920 + Offset, topLeft.X);
+        Assert.AreEqual(Offset, topLeft.Y);
+
+        var bottomRight = Calculation.GetRawCoordinatesFromPosition(Position.BottomRight, leftScreen, Window, dpi: 1.0);
+        Assert.AreEqual(-1920 + 1920 - (Window.Width + Offset), bottomRight.X);
+        Assert.AreEqual(1080 - (Window.Height + Offset), bottomRight.Y);
+    }
+
+    [TestMethod]
+    public void GetRawCoordinatesFromPosition_UnknownPosition_Throws()
+    {
+        Assert.ThrowsException<NotImplementedException>(
+            () => Calculation.GetRawCoordinatesFromPosition((Position)999, PrimaryScreen, Window, dpi: 1.0));
+    }
+
+    // Caret-relative placement centers the window horizontally on the caret and sits it 20px above.
+    [TestMethod]
+    public void GetRawCoordinatesFromCaret_WithRoom_CentersAboveCaret()
+    {
+        var caret = new Point(960, 540);
+
+        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);
+
+        Assert.AreEqual(960 - (Window.Width / 2), point.X);   // 860
+        Assert.AreEqual(540 - Window.Height - 20, point.Y);   // 468
+    }
+
+    // Near the left edge the window would overflow off-screen, so X clamps to the screen's left edge.
+    [TestMethod]
+    public void GetRawCoordinatesFromCaret_NearLeftEdge_ClampsToScreenLeft()
+    {
+        var caret = new Point(50, 540);
+
+        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);
+
+        Assert.AreEqual(PrimaryScreen.X, point.X);
+    }
+
+    // Near the right edge X clamps so the window's right side sits on the screen's right edge.
+    [TestMethod]
+    public void GetRawCoordinatesFromCaret_NearRightEdge_ClampsToScreenRight()
+    {
+        var caret = new Point(1900, 540);
+
+        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);
+
+        Assert.AreEqual(PrimaryScreen.X + PrimaryScreen.Width - Window.Width, point.X);  // 1720
+    }
+
+    // When there is no room above the caret (top would land off-screen) the window flips to 20px
+    // BELOW the caret instead of being clipped at the top.
+    [TestMethod]
+    public void GetRawCoordinatesFromCaret_NoRoomAbove_FlipsBelowCaret()
+    {
+        var caret = new Point(960, 10);
+
+        var point = Calculation.GetRawCoordinatesFromCaret(caret, PrimaryScreen, Window);
+
+        Assert.AreEqual(caret.Y + 20, point.Y);   // 30
+    }
+}
```
### src/modules/poweraccent/PowerAccent.Core.UnitTests/PowerAccent.Core.UnitTests.csproj  (+34/-0)
```diff
@@ -0,0 +1,34 @@
+<Project Sdk="Microsoft.NET.Sdk">
+  <!-- Look at Directory.Build.props in root for common stuff as well -->
+  <Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />
+
+  <PropertyGroup>
+    <AssemblyName>PowerToys.PowerAccent.Core.UnitTests</AssemblyName>
+    <OutputType>Exe</OutputType>
+    <OutputPath>$(RepoRoot)$(Platform)\$(Configuration)\tests\PowerAccent.Core.UnitTests\</OutputPath>
+    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
+    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
+    <Nullable>enable</Nullable>
+    <ImplicitUsings>enable</ImplicitUsings>
+  </PropertyGroup>
+
+  <PropertyGroup>
+    <!--
+      Do NOT set CsWinRTIncludes here. PowerAccent.Core already projects PowerToys.GPOWrapper and
+      PowerToys.PowerAccentKeyboardService, and those managed projections arrive transitively through
+      the PowerAccent.Core project reference. Listing either here generates a SECOND copy and breaks
+      the build with CS0436 (matches PowerAccent.UI, which references Core the same way).
+    -->
+    <CsWinRTGeneratedFilesDir>$(OutDir)</CsWinRTGeneratedFilesDir>
+    <ErrorOnDuplicatePublishOutputFiles>false</ErrorOnDuplicatePublishOutputFiles>
+  </PropertyGroup>
+
+  <ItemGroup>
+    <PackageReference Include="MSTest" />
+  </ItemGroup>
+
+  <ItemGroup>
+    <ProjectReference Include="..\PowerAccent.Core\PowerAccent.Core.csproj" />
+  </ItemGroup>
+
+</Project>
```
### src/modules/poweraccent/PowerAccent.Core/Models/Point.cs  (+0/-34)
```diff
@@ -6,12 +6,6 @@ namespace PowerAccent.Core;
 
 public struct Point
 {
-    public Point()
-    {
-        X = 0;
-        Y = 0;
-    }
-
     public Point(double x, double y)
     {
         X = x;
@@ -24,35 +18,7 @@ public Point(int x, int y)
         Y = y;
     }
 
-    public Point(System.Drawing.Point point)
-    {
-        X = point.X;
-        Y = point.Y;
-    }
-
     public double X { get; init; }
 
     public double Y { get; init; }
-
-    public static implicit operator Point(System.Drawing.Point point) => new Point(point.X, point.Y);
-
-    public static Point operator /(Point point, double divider)
-    {
-        if (divider == 0)
-        {
-            throw new DivideByZeroException();
-        }
-
-        return new Point(point.X / divider, point.Y / divider);
-    }
-
-    public static Point operator /(Point point, Point divider)
-    {
-        if (divider.X == 0 || divider.Y == 0)
-        {
-            throw new DivideByZeroException();
-        }
-
-        return new Point(point.X / divider.X, point.Y / divider.Y);
-    }
 }
```
### src/modules/poweraccent/PowerAccent.Core/Models/Rect.cs  (+0/-36)
```diff
@@ -6,14 +6,6 @@ namespace PowerAccent.Core;
 
 public struct Rect
 {
-    public Rect()
-    {
-        X = 0;
-        Y = 0;
-        Width = 0;
-        Height = 0;
-    }
-
     public Rect(int x, int y, int width, int height)
     {
         X = x;
@@ -22,14 +14,6 @@ public Rect(int x, int y, int width, int height)
         Height = height;
     }
 
-    public Rect(double x, double y, double width, double height)
-    {
-        X = x;
-        Y = y;
-        Width = width;
-        Height = height;
-    }
-
     public Rect(Point coord, Size size)
     {
         X = coord.X;
@@ -45,24 +29,4 @@ public Rect(Point coord, Size size)
     public double Width { get; init; }
 
     public double Height { get; init; }
-
-    public static Rect operator /(Rect rect, double divider)
-    {
-        if (divider == 0)
-        {
-            throw new DivideByZeroException();
-        }
-
-        return new Rect(rect.X / divider, rect.Y / divider, rect.Width / divider, rect.Height / divider);
-    }
-
-    public static Rect operator /(Rect rect, Rect divider)
-    {
-        if (divider.X == 0 || divider.Y == 0)
-        {
-            throw new DivideByZeroException();
-        }
-
-        return new Rect(rect.X / divider.X, rect.Y / divider.Y, rect.Width / divider.Width, rect.Height / divider.Height);
-    }
 }
```
### src/modules/poweraccent/PowerAccent.Core/Models/Size.cs  (+0/-28)
```diff
@@ -6,12 +6,6 @@ namespace PowerAccent.Core;
 
 public struct Size
 {
-    public Size()
-    {
-        Width = 0;
-        Height = 0;
-    }
-
     public Size(double width, double height)
     {
         Width = width;
@@ -27,26 +21,4 @@ public Size(int width, int height)
     public double Width { get; init; }
 
     public double Height { get; init; }
-
-    public static implicit operator Size(System.Drawing.Size size) => new Size(size.Width, size.Height);
-
-    public static Size operator /(Size size, double divider)
-    {
-        if (divider == 0)
-        {
-            throw new DivideByZeroException();
-        }
-
-        return new Size(size.Width / divider, size.Height / divider);
-    }
-
-    public static Size operator /(Size size, Size divider)
-    {
-        if (divider.Width == 0 || divider.Height == 0 || divider.Width == 0 || divider.Height == 0)
-        {
-            throw new DivideByZeroException();
-        }
-
-        return new Size(size.Width / divider.Width, size.Height / divider.Height);
-    }
 }
```
### src/modules/poweraccent/PowerAccent.Core/PowerAccent.Core.csproj  (+7/-2)
```diff
@@ -8,8 +8,6 @@
     <ImplicitUsings>enable</ImplicitUsings>
     <Nullable>disable</Nullable>
     <AllowUnsafeBlocks>True</AllowUnsafeBlocks>
-    <UseWPF>true</UseWPF>
-    <UseWindowsForms>true</UseWindowsForms>
   </PropertyGroup>
 
   <PropertyGroup>
@@ -26,6 +24,13 @@
     <PackageReference Include="UnicodeInformation" />
   </ItemGroup>
 
+  <ItemGroup>
+    <!-- Expose internal helpers (e.g. Tools.Calculation) to the unit-test assembly. -->
+    <AssemblyAttribute Include="System.Runtime.CompilerServices.InternalsVisibleToAttribute">
+      <_Parameter1>PowerToys.PowerAccent.Core.UnitTests</_Parameter1>
+    </AssemblyAttribute>
+  </ItemGroup>
+
   <ItemGroup>
     <ProjectReference Include="..\..\..\common\GPOWrapper\GPOWrapper.vcxproj" />
     <ProjectReference Include="..\..\..\common\interop\PowerToys.Interop.vcxproj" />
```
### src/modules/poweraccent/PowerAccent.Core/PowerAccent.cs  (+15/-12)
```diff
@@ -45,8 +45,12 @@ public partial class PowerAccent : IDisposable
 
     private readonly CharactersUsageInfo _usageInfo;
 
-    public PowerAccent()
+    private readonly Action<Action> _runOnUiThread;
+
+    public PowerAccent(Action<Action> runOnUiThread)
     {
+        _runOnUiThread = runOnUiThread ?? throw new ArgumentNullException(nameof(runOnUiThread));
+
         Logger.InitializeLogger("\\QuickAccent\\Logs");
 
         LoadUnicodeInfoCache();
@@ -68,23 +72,23 @@ private void SetEvents()
     {
         _keyboardListener.SetShowToolbarEvent(new PowerToys.PowerAccentKeyboardService.ShowToolbar((LetterKey letterKey) =>
         {
-            System.Windows.Application.Current.Dispatcher.Invoke(() =>
+            _runOnUiThread(() =>
             {
                 ShowToolbar(letterKey);
             });
         }));
 
         _keyboardListener.SetHideToolbarEvent(new PowerToys.PowerAccentKeyboardService.HideToolbar((InputType inputType) =>
         {
-            System.Windows.Application.Current.Dispatcher.Invoke(() =>
+            _runOnUiThread(() =>
             {
                 SendInputAndHideToolbar(inputType);
             });
         }));
 
         _keyboardListener.SetNextCharEvent(new PowerToys.PowerAccentKeyboardService.NextChar((TriggerKey triggerKey, bool shiftPressed) =>
         {
-            System.Windows.Application.Current.Dispatcher.Invoke(() =>
+            _runOnUiThread(() =>
             {
                 ProcessNextChar(triggerKey, shiftPressed);
             });
@@ -236,13 +240,13 @@ private void SendInputAndHideToolbar(InputType inputType)
 
             case InputType.Right:
                 {
-                    SendKeys.SendWait("{RIGHT}");
+                    WindowsFunctions.SendArrowKey(left: false);
                     break;
                 }
 
             case InputType.Left:
                 {
-                    SendKeys.SendWait("{LEFT}");
+                    WindowsFunctions.SendArrowKey(left: true);
                     break;
                 }
 
@@ -391,14 +395,13 @@ public Point GetDisplayCoordinates(Size window)
     /// Gets the maximum width for the toolbar display based on the active screen
     /// dimensions.
     /// </summary>
-    /// <returns>The maximum width in logical pixels, accounting for screen padding.
-    /// </returns>
+    /// <returns>The maximum width in DIPs (device-independent pixels), accounting for
+    /// screen padding.</returns>
     public double GetDisplayMaxWidth()
     {
-        // Note: activeDisplay.Size.Width is in raw physical pixels.
-        // We divide by DPI to convert to WPF logical pixels (Device-Independent Pixels),
-        // because ScreenMinPadding is a logical pixel value and WPF MaxWidth expects
-        // logical pixels.
+        // activeDisplay.Size.Width is in raw physical pixels; divide by the DPI scale to
+        // convert to DIPs (device-independent pixels), since ScreenMinPadding and the
+        // consuming window width are both expressed in DIPs.
         var activeDisplay = WindowsFunctions.GetActiveDisplay();
         return (activeDisplay.Size.Width / activeDisplay.Dpi) - ScreenMinPadding;
     }
```
### src/modules/poweraccent/PowerAccent.Core/Tools/WindowsFunctions.cs  (+36/-1)
```diff
@@ -88,6 +88,40 @@ public static void Insert(string s, bool back = false)
         }
     }
 
+    public static void SendArrowKey(bool left)
+    {
+        var key = left ? VIRTUAL_KEY.VK_LEFT : VIRTUAL_KEY.VK_RIGHT;
+        var inputs = new INPUT[]
+        {
+            new INPUT
+            {
+                type = INPUT_TYPE.INPUT_KEYBOARD,
+                Anonymous = new INPUT._Anonymous_e__Union
+                {
+                    ki = new KEYBDINPUT
+                    {
+                        wVk = key,
+                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY,
+                    },
+                },
+            },
+            new INPUT
+            {
+                type = INPUT_TYPE.INPUT_KEYBOARD,
+                Anonymous = new INPUT._Anonymous_e__Union
+                {
+                    ki = new KEYBDINPUT
+                    {
+                        wVk = key,
+                        dwFlags = KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY | KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP,
+                    },
+                },
+            },
+        };
+
+        _ = PInvoke.SendInput(inputs, Marshal.SizeOf<INPUT>());
+    }
+
     public static (Point Location, Size Size, double Dpi) GetActiveDisplay()
     {
         GUITHREADINFO guiInfo = default;
@@ -107,7 +141,8 @@ public static (Point Location, Size Size, double Dpi) GetActiveDisplay()
 
         double dpi = dpiRaw / 96d;
         var location = new Point(monitorInfo.rcWork.left, monitorInfo.rcWork.top);
-        return (location, monitorInfo.rcWork.Size, dpi);
+        var size = new Size(monitorInfo.rcWork.right - monitorInfo.rcWork.left, monitorInfo.rcWork.bottom - monitorInfo.rcWork.top);
+        return (location, size, dpi);
     }
 
     public static bool IsCapsLockState()
```
### src/modules/poweraccent/PowerAccent.UI/App.xaml  (+0/-6)
```diff
@@ -1,6 +0,0 @@
-﻿<Application
-    x:Class="PowerAccent.UI.App"
-    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
-    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
-    StartupUri="Selector.xaml"
-    ThemeMode="System" />
\ No newline at end of file
```
### src/modules/poweraccent/PowerAccent.UI/App.xaml.cs  (+0/-64)
```diff
@@ -1,64 +0,0 @@
-// Copyright (c) Microsoft Corporation
-// The Microsoft Corporation licenses this file to you under the MIT license.
-// See the LICENSE file in the project root for more information.
-
-using System;
-using System.Threading;
-using System.Windows;
-
-using ManagedCommon;
-using Microsoft.PowerToys.Telemetry;
-
-namespace PowerAccent.UI
-{
-    /// <summary>
-    /// Interaction logic for App.xaml
-    /// </summary>
-    public partial class App : Application, IDisposable
-    {
-        private static Mutex _mutex;
-        private bool _disposed;
-        private ETWTrace _etwTrace = new ETWTrace();
-
-        protected override void OnStartup(StartupEventArgs e)
-        {
-            _mutex = new Mutex(true, "QuickAccent", out bool createdNew);
-
-            if (!createdNew)
-            {
-                Logger.LogWarning("Another running QuickAccent instance was detected. Exiting QuickAccent");
-                Application.Current.Shutdown();
-            }
-
-            base.OnStartup(e);
-        }
-
-        protected override void OnExit(ExitEventArgs e)
-        {
-            _mutex?.ReleaseMutex();
-            base.OnExit(e);
-        }
-
-        protected virtual void Dispose(bool disposing)
-        {
-            if (_disposed)
-            {
-                return;
-            }
-
-            if (disposing)
-            {
-                _mutex?.Dispose();
-                _etwTrace?.Dispose();
-            }
-
-            _disposed = true;
-        }
-
-        public void Dispose()
-        {
-            Dispose(disposing: true);
-            GC.SuppressFinalize(this);
-        }
-    }
-}
```
### src/modules/poweraccent/PowerAccent.UI/AssemblyInfo.cs  (+0/-10)
```diff
@@ -1,10 +0,0 @@
-// Copyright (c) Microsoft Corporation
-// The Microsoft Corporation licenses this file to you under the MIT license.
-// See the LICENSE file in the project root for more information.
-
-using System.Windows;
-
-[assembly: ThemeInfo(
-    ResourceDictionaryLocation.None, // where theme specific resource dictionaries are located (used if a resource is not found in the page, or application resource dictionaries)
-    ResourceDictionaryLocation.SourceAssembly) // where the generic resource dictionary is locate (used if a resource is not found in the page, app, or any theme specific resource dictionaries)
-]
```
### src/modules/poweraccent/PowerAccent.UI/NativeMethods.txt  (+0/-2)
```diff
@@ -1,2 +0,0 @@
-﻿SetWindowPos
-GetSystemMetrics
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccent.UI.csproj  (+106/-35)
```diff
@@ -2,39 +2,110 @@
   <!-- Look at Directory.Build.props in root for common stuff as well -->
   <Import Project="$(RepoRoot)src\Common.Dotnet.CsWinRT.props" />
   <Import Project="$(RepoRoot)src\Common.SelfContained.props" />
-	
-	<PropertyGroup>
-		<OutputType>WinExe</OutputType>
-		<Nullable>disable</Nullable>
-		<UseWPF>true</UseWPF>
-		<AllowUnsafeBlocks>True</AllowUnsafeBlocks>
-		<ApplicationIcon>icon.ico</ApplicationIcon>
-		<ApplicationManifest>app.manifest</ApplicationManifest>
-		<AssemblyName>PowerToys.PowerAccent</AssemblyName>
-		<XamlDebuggingInformation>True</XamlDebuggingInformation>
-		<StartupObject>PowerAccent.UI.Program</StartupObject>
-		<OutputPath>$(RepoRoot)$(Platform)\$(Configuration)</OutputPath>
-		<AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
-		<AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
-	</PropertyGroup>
-
-	<ItemGroup>
-		<Resource Include="icon.ico">
-			<CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
-		</Resource>
-	</ItemGroup>
-
-	<ItemGroup>
-		<PackageReference Include="Microsoft.Windows.CsWin32">
-			<PrivateAssets>all</PrivateAssets>
-			<IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
-		</PackageReference>
-	</ItemGroup>
-
-	<ItemGroup>
-		<ProjectReference Include="..\..\..\common\Common.UI\Common.UI.csproj" />
-		<ProjectReference Include="..\..\..\common\interop\PowerToys.Interop.vcxproj" />
-		<ProjectReference Include="..\PowerAccent.Core\PowerAccent.Core.csproj" />
-		<ProjectReference Include="..\PowerAccentKeyboardService\PowerAccentKeyboardService.vcxproj" />
-	</ItemGroup>
+  <Import Project="$(RepoRoot)src\Common.Dotnet.AotCompatibility.props" />
+
+  <PropertyGroup>
+    <OutputType>WinExe</OutputType>
+    <RootNamespace>PowerAccent.UI</RootNamespace>
+    <AssemblyName>PowerToys.PowerAccent</AssemblyName>
+    <Nullable>disable</Nullable>
+    <AllowUnsafeBlocks>True</AllowUnsafeBlocks>
+    <!-- Required so CommunityToolkit.Mvvm's source generator emits the WinRT-correct partial
+         property implementations for [ObservableProperty] (avoids MVVMTK0045 / CS9248).
+         Matches the sibling WinUI 3 module PowerDisplay. -->
+    <LangVersion>preview</LangVersion>
+    <UseWinUI>true</UseWinUI>
+    <!--
+      App.xaml and the windows live under PowerAccentXAML\ (not the project root). Nesting the XAML
+      in a named subfolder is the repo convention for WinUI 3 apps that share the WinUI3Apps output
+      folder (see Peek's PeekXAML\, PowerDisplay's PowerDisplayXAML\): it keeps the compiled .xbf out
+      of the WinUI3Apps root, so the "Audit WinAppSDK applications path asset conflicts" pipeline step
+      passes. Disable the default ApplicationDefinition glob so the explicit
+      <ApplicationDefinition Include="PowerAccentXAML\App.xaml" /> below is the single one.
+    -->
+    <EnableDefaultApplicationDefinition>false</EnableDefaultApplicationDefinition>
+    <EnablePreviewMsixTooling>true</EnablePreviewMsixTooling>
+    <WindowsPackageType>None</WindowsPackageType>
+    <WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>
+    <ApplicationIcon>icon.ico</ApplicationIcon>
+    <ApplicationManifest>app.manifest</ApplicationManifest>
+    <StartupObject>PowerAccent.UI.Program</StartupObject>
+    <DefineConstants>DISABLE_XAML_GENERATED_MAIN</DefineConstants>
+    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
+    <AppendRuntimeIdentifierToOutputPath>false</AppendRuntimeIdentifierToOutputPath>
+    <OutputPath>$(RepoRoot)$(Platform)\$(Configuration)\WinUI3Apps</OutputPath>
+    <ProjectPriFileName>PowerToys.PowerAccent.pri</ProjectPriFileName>
+  </PropertyGroup>
+
+  <!-- Native AOT Configuration. Mirrors the sibling WinUI 3 module PowerDisplay so the app is
+       compiled with ILC on publish, surfacing trim/AOT problems that the analyzers alone miss. -->
+  <PropertyGroup>
+    <PublishSingleFile>false</PublishSingleFile>
+    <DisableRuntimeMarshalling>false</DisableRuntimeMarshalling>
+    <PublishAot>true</PublishAot>
+    <TrimmerSingleWarn>false</TrimmerSingleWarn>
+    <SuppressTrimAnalysisWarnings>false</SuppressTrimAnalysisWarnings>
+  </PropertyGroup>
+
+  <PropertyGroup>
+    <!--
+      Do NOT set CsWinRTIncludes here. Both WinRT components the UI touches - PowerToys.GPOWrapper
+      (called directly from Program.cs) and PowerToys.PowerAccentKeyboardService (used by Core) -
+      are already projected by PowerAccent.Core, which the UI references, so their managed
+      projections arrive transitively. Listing either here generates a SECOND copy of the same
+      types and breaks the build with CS0436 (e.g. GpoRuleConfigured defined both in this project's
+      generated files and in PowerAccent.Core). This matches the original WPF UI, which had no
+      CsWinRTIncludes at all.
+    -->
+    <CsWinRTGeneratedFilesDir>$(OutDir)</CsWinRTGeneratedFilesDir>
+    <ErrorOnDuplicatePublishOutputFiles>false</ErrorOnDuplicatePublishOutputFiles>
+  </PropertyGroup>
+
+  <ItemGroup>
+    <Resource Include="icon.ico">
+      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
+    </Resource>
+  </ItemGroup>
+
+  <Target Name="CopyPRIFileToOutputDir" AfterTargets="Build">
+    <ItemGroup>
+      <PRIFile Include="$(OutDir)**\PowerToys.PowerAccent.pri" />
+    </ItemGroup>
+    <Copy SourceFiles="@(PRIFile)" DestinationFolder="$(OutDir)" />
+  </Target>
+
+  <ItemGroup>
+    <Page Remove="PowerAccentXAML\App.xaml" />
+  </ItemGroup>
+  <ItemGroup>
+    <ApplicationDefinition Include="PowerAccentXAML\App.xaml" />
+  </ItemGroup>
+
+  <ItemGroup>
+    <PackageReference Include="Microsoft.WindowsAppSDK" />
+    <PackageReference Include="Microsoft.Windows.SDK.BuildTools" />
+    <PackageReference Include="WinUIEx" />
+    <PackageReference Include="CommunityToolkit.Mvvm" />
+    <PackageReference Include="CommunityToolkit.WinUI.Animations" />
+    <PackageReference Include="CommunityToolkit.WinUI.Converters" />
+
+    <Manifest Include="$(ApplicationManifest)" />
+  </ItemGroup>
+
+  <ItemGroup Condition="'$(DisableMsixProjectCapabilityAddedByProject)'!='true' and '$(EnableMsixTooling)'=='true'">
+    <ProjectCapability Include="Msix" />
+  </ItemGroup>
+
+  <ItemGroup>
+    <ProjectReference Include="..\..\..\common\Common.UI\Common.UI.csproj" />
+    <ProjectReference Include="..\..\..\common\Common.UI.Controls\Common.UI.Controls.csproj" />
+    <ProjectReference Include="..\..\..\common\ManagedCommon\ManagedCommon.csproj" />
+    <ProjectReference Include="..\..\..\common\interop\PowerToys.Interop.vcxproj" />
+    <ProjectReference Include="..\PowerAccent.Core\PowerAccent.Core.csproj" />
+    <ProjectReference Include="..\PowerAccentKeyboardService\PowerAccentKeyboardService.vcxproj" />
+  </ItemGroup>
+
+  <PropertyGroup Condition="'$(DisableHasPackageAndPublishMenuAddedByProject)'!='true' and '$(EnableMsixTooling)'=='true'">
+    <HasPackageAndPublishMenu>true</HasPackageAndPublishMenu>
+  </PropertyGroup>
 </Project>
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccentXAML/App.xaml  (+13/-0)
```diff
@@ -0,0 +1,13 @@
+<?xml version="1.0" encoding="utf-8" ?>
+<Application
+    x:Class="PowerAccent.UI.App"
+    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
+    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
+    <Application.Resources>
+        <ResourceDictionary>
+            <ResourceDictionary.MergedDictionaries>
+                <XamlControlsResources xmlns="using:Microsoft.UI.Xaml.Controls" />
+            </ResourceDictionary.MergedDictionaries>
+        </ResourceDictionary>
+    </Application.Resources>
+</Application>
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccentXAML/App.xaml.cs  (+60/-0)
```diff
@@ -0,0 +1,60 @@
+// Copyright (c) Microsoft Corporation
+// The Microsoft Corporation licenses this file to you under the MIT license.
+// See the LICENSE file in the project root for more information.
+
+using System;
+
+using ManagedCommon;
+using Microsoft.PowerToys.Telemetry;
+using Microsoft.UI.Dispatching;
+using Microsoft.UI.Xaml;
+
+namespace PowerAccent.UI;
+
+public partial class App : Application, IDisposable
+{
+    private readonly ETWTrace _etwTrace = new ETWTrace();
+    private bool _disposed;
+
+    public static new App Current => (App)Application.Current;
+
+    public DispatcherQueue DispatcherQueueForApp { get; private set; }
+
+    public static MainWindow Window { get; private set; }
+
+    public App()
+    {
+        InitializeComponent();
+        UnhandledException += (s, e) => Logger.LogError("Unhandled exception", e.Exception);
+    }
+
+    protected override void OnLaunched(LaunchActivatedEventArgs args)
+    {
+        DispatcherQueueForApp = DispatcherQueue.GetForCurrentThread();
+        Window = new MainWindow();
+
+        // Quick Accent has no visible main window until summoned by the keyboard hook;
+        // the accent selector keeps itself hidden (TransparentWindow hides its AppWindow on init).
+    }
+
+    protected virtual void Dispose(bool disposing)
+    {
+        if (_disposed)
+        {
+            return;
+        }
+
+        if (disposing)
+        {
+            _etwTrace?.Dispose();
+        }
+
+        _disposed = true;
+    }
+
+    public void Dispose()
+    {
+        Dispose(disposing: true);
+        GC.SuppressFinalize(this);
+    }
+}
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccentXAML/MainWindow.xaml  (+19/-0)
```diff
@@ -0,0 +1,19 @@
+﻿<?xml version="1.0" encoding="utf-8" ?>
+<common:TransparentWindow
+    x:Class="PowerAccent.UI.MainWindow"
+    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
+    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
+    xmlns:common="using:Microsoft.PowerToys.Common.UI.Controls.Window"
+    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
+    xmlns:local="using:PowerAccent.UI"
+    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
+    mc:Ignorable="d">
+
+    <!--
+        The content lives in a UserControl (SelectorControl) rather than inline here so its x:Bind
+        bindings initialize on the control's Loading pass - which fires when this SW_SHOWNA overlay is
+        first laid out - instead of on Window.Activated, which never fires for a window shown without
+        activation. That removes the need to call Bindings.Update() by hand.
+    -->
+    <local:SelectorControl x:Name="Selector" />
+</common:TransparentWindow>
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccentXAML/MainWindow.xaml.cs  (+177/-0)
```diff
@@ -0,0 +1,177 @@
+// Copyright (c) Microsoft Corporation
+// The Microsoft Corporation licenses this file to you under the MIT license.
+// See the LICENSE file in the project root for more information.
+
+using System;
+
+using Microsoft.PowerToys.Common.UI.Controls.Window;
+using Microsoft.UI.Dispatching;
+using Microsoft.UI.Windowing;
+using Windows.Graphics;
+using CoreSize = PowerAccent.Core.Size;
+
+namespace PowerAccent.UI;
+
+public sealed partial class MainWindow : TransparentWindow, IDisposable
+{
+    // Accent-bar geometry (DIP). Width is derived from the item count (count * ItemWidthDip), not
+    // measured from the ListView: its DesiredSize (wrapped in a ScrollViewer) is racy while item
+    // containers realize and intermittently reports 0, yielding a blank/clipped bar. The one-row bar
+    // hugs its content like the WPF original, capped at the monitor width; beyond that it scrolls
+    // and ScrollIntoView reveals the selected glyph.
+    private const double RowHeightDip = 92;          // one row of accent pills (item Height=48 + card border)
+    private const double DescriptionHeightDip = 36;  // extra row shown when the Unicode description is on
+    private const double ItemWidthDip = 48;            // one accent cell (ListViewItem Grid MinWidth=48)
+    private const double DescriptionMinWidthDip = 648; // min bar width while the description row shows (WPF parity)
+
+    private readonly Core.PowerAccent _powerAccent;
+    private int _selectedIndex = -1;
+    private bool _active;
+
+    // The view model lives on the SelectorControl (the x:Bind target); expose it here for the
+    // PowerAccent event handlers that populate the accent list and description.
+    private SelectorViewModel ViewModel => Selector.ViewModel;
+
+    public MainWindow()
+    {
+        InitializeComponent();
+
+        // Give the overlay a stable UIA identity (window name) for accessibility tools (Narrator,
+        // Accessibility Insights) and the release-verification harness. "Quick Accent" is the
+        // user-facing feature name.
+        AppWindow.Title = "Quick Accent";
+
+        // The accent popup is shown/hidden instantly (no slide/fade) for typing-aid
+        // responsiveness. TransientSurface defaults to Transition.None (no animation);
+        // SubscribeSurfaceTo forwards to the inner surface so it follows this window's Show/Hide.
+        Selector.SubscribeSurfaceTo(this);
+
+        _powerAccent = new Core.PowerAccent(RunOnUiThread);
+        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
+        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectCharacter;
+
+        // No manual theme handling: App.xaml leaves RequestedTheme unset, so WinUI follows the system
+        // theme and re-resolves the {ThemeResource} brushes (and retints the acrylic) on a live
+        // light/dark switch, even for this never-activated SW_SHOWNA overlay.
+    }
+
+    // Marshal keyboard-hook callbacks (ShowToolbar / HideToolbar / NextChar) onto the UI thread. The
+    // hook runs on this UI thread, so callbacks arrive here already; run them inline (not via
+    // TryEnqueue, which would defer) so the accent injection stays ordered before the hook returns
+    // and the trigger key-up propagates. Fall back to enqueueing if ever called off-thread.
+    private void RunOnUiThread(Action action)
+    {
+        if (DispatcherQueue.HasThreadAccess)
+        {
+            action();
+        }
+        else
+        {
+            DispatcherQueue.TryEnqueue(() => action());
+        }
+    }
+
+    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
+    {
+        if (!isActive)
+        {
+            _active = false;
+
+            // Release always-on-top before hiding so the dormant overlay does not keep a discrete
+            // GPU awake on hybrid-graphics laptops (issue #34849 / PR #41044). IsAlwaysOnTop is the
+            // WinUIEx WindowEx property (same as the sibling PowerDisplay).
+            IsAlwaysOnTop = false;
+            Hide();
+            ViewModel.Characters.Clear();
+            _selectedIndex = -1;
+            return;
+        }
+
+        _active = true;
+        ViewModel.ShowDescription = _powerAccent.ShowUnicodeDescription;
+
+        ViewModel.Characters.Clear();
+        foreach (var c in chars)
+        {
+            ViewModel.Characters.Add(c);
+        }
+
+        Selector.SetSelectedIndex(_selectedIndex);
+        ViewModel.Description = (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
+            ? _powerAccent.CharacterDescriptions[_selectedIndex]
+            : string.Empty;
+
+        // Always-on-top only while shown, so the overlay sits above the foreground app (Show uses
+        // SW_SHOWNA and never activates it); released on hide (see above). Then size and show.
+        IsAlwaysOnTop = true;
+        SizeAndPosition();
+        Show();
+
+        DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () =>
+        {
+            if (_active)
+            {
+                Selector.ScrollSelectedIntoView(_selectedIndex);
+            }
+        });
+
+        Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new Core.Telemetry.PowerAccentShowAccentMenuEvent());
+    }
+
+    private void PowerAccent_OnSelectCharacter(int index, string character)
+    {
+        _selectedIndex = index;
+        Selector.SetSelectedIndex(index);
+
+        if (index >= 0 && index < _powerAccent.CharacterDescriptions.Length)
+        {
+            ViewModel.Description = _powerAccent.CharacterDescriptions[index];
+        }
+
+        Selector.ScrollSelectedIntoView(index);
+    }
+
+    private void SizeAndPosition()
+    {
+        // Width hugs the content: item count * ItemWidthDip (see the class-level note on why the
+        // ListView is not measured), capped at the monitor's max usable width so long lists scroll.
+        double maxWidthDip = _powerAccent.GetDisplayMaxWidth();
+        double contentWidthDip = ViewModel.Characters.Count * ItemWidthDip;
+
+        // The Unicode description row needs room for a readable line; the WPF original gave it a
+        // 600px MinWidth. Widen a short accent bar to match when the row is shown (the accent bar
+        // itself stays centered within the wider window).
+        if (ViewModel.ShowDescription)
+        {
+            contentWidthDip = Math.Max(contentWidthDip, DescriptionMinWidthDip);
+        }
+
+        double widthDip = Math.Clamp(contentWidthDip, ItemWidthDip, maxWidthDip);
+        double heightDip = RowHeightDip + (ViewModel.ShowDescription ? DescriptionHeightDip : 0);
+
+        // Calculation works in physical pixels; GetDisplayCoordinates multiplies the DIP size by
+        // the active monitor's DPI internally and returns the physical top-left for the anchor.
+        var coordinates = _powerAccent.GetDisplayCoordinates(new CoreSize(widthDip, heightDip));
+
+        var display = DisplayArea.GetFromPoint(
+            new PointInt32((int)Math.Round(coordinates.X), (int)Math.Round(coordinates.Y)),
+            DisplayAreaFallback.Nearest);
+
+        double dpiScale = FlyoutWindowHelper.GetDpiScale(display);
+
+        var rect = new RectInt32(
+            (int)Math.Round(coordinates.X),
+            (int)Math.Round(coordinates.Y),
+            (int)Math.Ceiling(widthDip * dpiScale),
+            (int)Math.Ceiling(heightDip * dpiScale));
+
+        FlyoutWindowHelper.MoveAndResizeOnDisplay(this, display, rect);
+    }
+
+    public void Dispose()
+    {
+        _powerAccent.SaveUsageInfo();
+        _powerAccent.Dispose();
+        GC.SuppressFinalize(this);
+    }
+}
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccentXAML/SelectorControl.xaml  (+141/-0)
```diff
@@ -0,0 +1,141 @@
+﻿<?xml version="1.0" encoding="utf-8" ?>
+<UserControl
+    x:Class="PowerAccent.UI.SelectorControl"
+    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
+    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
+    xmlns:controls="using:Microsoft.PowerToys.Common.UI.Controls"
+    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
+    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
+    mc:Ignorable="d">
+
+    <controls:TransientSurface x:Name="Surface" Margin="24,24,24,16">
+        <Grid x:Name="RootContent">
+            <Grid.RowDefinitions>
+                <RowDefinition Height="Auto" />
+                <RowDefinition Height="Auto" />
+            </Grid.RowDefinitions>
+
+            <Grid Background="{ThemeResource LayerOnAcrylicFillColorDefaultBrush}">
+                <ListView
+                    x:Name="CharactersList"
+                    Padding="0"
+                    HorizontalAlignment="Center"
+                    AutomationProperties.AutomationId="QuickAccentCharacterList"
+                    IsHitTestVisible="False"
+                    IsItemClickEnabled="False"
+                    ItemsSource="{x:Bind ViewModel.Characters, Mode=OneWay}"
+                    ScrollViewer.HorizontalScrollBarVisibility="Hidden"
+                    ScrollViewer.HorizontalScrollMode="Enabled"
+                    ScrollViewer.VerticalScrollBarVisibility="Disabled"
+                    ScrollViewer.VerticalScrollMode="Disabled"
+                    SelectionMode="Single">
+                    <!--
+                        Disable default ListView item animations: the bar is rebuilt on every keystroke,
+                        and the built-in slide/fade transitions read as lag on a typing aid.
+                    -->
+                    <ListView.ItemContainerTransitions>
+                        <TransitionCollection />
+                    </ListView.ItemContainerTransitions>
+                    <ListView.ItemsPanel>
+                        <ItemsPanelTemplate>
+                            <StackPanel Orientation="Horizontal" />
+                        </ItemsPanelTemplate>
+                    </ListView.ItemsPanel>
+                    <!--
+                        Custom container template reproducing the WPF accent "pill": an inset, rounded,
+                        accent-filled rectangle shown only on selection (via VisualStateManager, since
+                        WinUI 3 has no Style/ControlTemplate triggers), with the glyph turning on-accent.
+                    -->
+                    <ListView.ItemContainerStyle>
+                        <Style TargetType="ListViewItem">
+                            <Setter Property="IsTabStop" Value="False" />
+                            <Setter Property="Margin" Value="0" />
+                            <Setter Property="Padding" Value="0" />
+                            <!--
+                                WinUI's ListViewItem defaults MinWidth to 88; without pinning it to the
+                                48px cell width each glyph is padded out, leaving wide gaps between accents.
+                            -->
+                            <Setter Property="MinWidth" Value="48" />
+                            <Setter Property="Template">
+                                <Setter.Value>
+                                    <ControlTemplate TargetType="ListViewItem">
+                                        <Grid
+                                            Height="48"
+                                            MinWidth="48"
+                                            HorizontalAlignment="Center"
+                                            VerticalAlignment="Center">
+                                            <Border
+                                                x:Name="SelectionIndicator"
+                                                Margin="7"
+                                                Background="{ThemeResource AccentFillColorDefaultBrush}"
+                                                BorderBrush="{ThemeResource AccentControlElevationBorderBrush}"
+                                                BorderThickness="1"
+                                                CornerRadius="4"
+                                                Opacity="0" />
+                                            <ContentPresenter
+                                                x:Name="ContentPresenter"
+                                                Margin="12"
+                                                HorizontalAlignment="Center"
+                                                VerticalAlignment="Center"
+                                                Foreground="{ThemeResource TextFillColorPrimaryBrush}" />
+                                            <VisualStateManager.VisualStateGroups>
+                                                <VisualStateGroup x:Name="CommonStates">
+                                                    <VisualState x:Name="Normal" />
+                                                    <VisualState x:Name="Selected">
+                                                        <VisualState.Setters>
+                                                            <Setter Target="SelectionIndicator.Opacity" Value="1" />
+                                                            <Setter Target="ContentPresenter.Foreground" Value="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
+                                                        </VisualState.Setters>
+                                                    </VisualState>
+                                                    <VisualState x:Name="PointerOverSelected">
+                                                        <VisualState.Setters>
+                                                            <Setter Target="SelectionIndicator.Opacity" Value="1" />
+                                                            <Setter Target="ContentPresenter.Foreground" Value="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
+                                                        </VisualState.Setters>
+                                                    </VisualState>
+                                                    <VisualState x:Name="PressedSelected">
+                                                        <VisualState.Setters>
+                                                            <Setter Target="SelectionIndicator.Opacity" Value="1" />
+                                                            <Setter Target="ContentPresenter.Foreground" Value="{ThemeResource TextOnAccentFillColorPrimaryBrush}" />
+                                                        </VisualState.Setters>
+                                                    </VisualState>
+                                                </VisualStateGroup>
+                                            </VisualStateManager.VisualStateGroups>
+                                        </Grid>
+                                    </ControlTemplate>
+                                </Setter.Value>
+                            </Setter>
+                        </Style>
+                    </ListView.ItemContainerStyle>
+                    <ListView.ItemTemplate>
+                        <DataTemplate x:DataType="x:String">
+                            <TextBlock
+                                VerticalAlignment="Center"
+                                FontSize="18"
+                                Text="{x:Bind Mode=OneTime}"
+                                TextAlignment="Center" />
+                        </DataTemplate>
+                    </ListView.ItemTemplate>
+                </ListView>
+            </Grid>
+
+            <Grid Grid.Row="1" Visibility="{x:Bind ViewModel.DescriptionVisibility, Mode=OneWay}">
+                <TextBlock
+                    x:Name="CharacterName"
+                    MaxHeight="36"
+                    Margin="8"
+                    AutomationProperties.AutomationId="QuickAccentDescription"
+                    FontSize="12"
+                    Foreground="{ThemeResource TextFillColorSecondaryBrush}"
+                    Text="{x:Bind ViewModel.Description, Mode=OneWay}"
+                    TextAlignment="Center"
+                    TextTrimming="CharacterEllipsis"
+                    TextWrapping="Wrap" />
+                <Rectangle
+                    Height="1"
+                    VerticalAlignment="Top"
+                    Fill="{ThemeResource DividerStrokeColorDefaultBrush}" />
+            </Grid>
+        </Grid>
+    </controls:TransientSurface>
+</UserControl>
```
### src/modules/poweraccent/PowerAccent.UI/PowerAccentXAML/SelectorControl.xaml.cs  (+41/-0)
```diff
@@ -0,0 +1,41 @@
+// Copyright (c) Microsoft Corporation
+// The Microsoft Corporation licenses this file to you under the MIT license.
+// See the LICENSE file in the project root for more information.
+
+using Microsoft.PowerToys.Common.UI.Controls.Window;
+using Microsoft.UI.Xaml.Controls;
+
+namespace PowerAccent.UI;
+
+/// <summary>
+/// The accent selector content. Hosting it in a UserControl (rather than directly in the
+/// TransparentWindow) lets x:Bind initialize on the control's Loading pass - which fires when the
+/// SW_SHOWNA overlay is first laid out - instead of on Window.Activated (which never fires for a
+/// never-activated overlay). That removes the need to call Bindings.Update() by hand.
+/// </summary>
+public sealed partial class SelectorControl : UserControl
+{
+    public SelectorViewModel ViewModel { get; } = new();
+
+    public SelectorControl()
+    {
+        InitializeComponent();
+    }
+
+    // Number of items currently in the accent bar (mirrors the bound ObservableCollection).
+    public int ItemCount => CharactersList.Items.Count;
+
+    // Wire the inner TransientSurface to the hosting window's Show/Hide so it animates in/out.
+    // TransientSurface.SubscribeTo explicitly supports being "placed within" the window content.
+    public void SubscribeSurfaceTo(TransparentWindow host) => Surface.SubscribeTo(host);
+
+    public void SetSelectedIndex(int index) => CharactersList.SelectedIndex = index;
+
+    public void ScrollSelectedIntoView(int index)
+    {
+        if (index >= 0 && index < CharactersList.Items.Count)
+        {
+            CharactersList.ScrollIntoView(CharactersList.Items[index]);
+        }
+    }
+}
```
### src/modules/poweraccent/PowerAccent.UI/Program.cs  (+44/-31)
```diff
@@ -1,50 +1,61 @@
-﻿// Copyright (c) Microsoft Corporation
+// Copyright (c) Microsoft Corporation
 // The Microsoft Corporation licenses this file to you under the MIT license.
 // See the LICENSE file in the project root for more information.
 
 using System;
-using System.Diagnostics;
 using System.Threading;
 using System.Threading.Tasks;
-using System.Windows;
 
 using ManagedCommon;
+using Microsoft.UI.Dispatching;
 using PowerToys.Interop;
 
 namespace PowerAccent.UI;
 
 internal static class Program
 {
     private static readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
-    private static App _application;
+    private static Mutex _mutex;
     private static int _powerToysRunnerPid;
 
     [STAThread]
     public static void Main(string[] args)
     {
         Logger.InitializeLogger("\\QuickAccent\\Logs");
+        WinRT.ComWrappersSupport.InitializeComWrappers();
 
         if (PowerToys.GPOWrapper.GPOWrapper.GetConfiguredQuickAccentEnabledValue() == PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
         {
             Logger.LogWarning("Tried to start with a GPO policy setting the utility to always be disabled. Please contact your systems administrator.");
             return;
         }
 
+        _mutex = new Mutex(true, "QuickAccent", out bool createdNew);
+        if (!createdNew)
+        {
+            Logger.LogWarning("Another running QuickAccent instance was detected. Exiting QuickAccent");
+            return;
+        }
+
         Arguments(args);
+        InitExitListener();
 
-        InitEvents();
+        Microsoft.UI.Xaml.Application.Start((p) =>
+        {
+            var context = new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread());
+            SynchronizationContext.SetSynchronizationContext(context);
+            _ = new App();
+        });
 
-        _application = new App();
-        _application.InitializeComponent();
-        _application.Run();
+        _mutex?.ReleaseMutex();
     }
 
-    private static void InitEvents()
+    private static void InitExitListener()
     {
         Task.Run(
             () =>
             {
-                EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerAccentExitEvent());
+                using EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.PowerAccentExitEvent());
                 if (eventHandle.WaitOne())
                 {
                     Terminate();
@@ -55,39 +66,41 @@ private static void InitEvents()
 
     private static void Arguments(string[] args)
     {
-        if (args?.Length > 0)
+        if (args?.Length > 0 && int.TryParse(args[0], out _powerToysRunnerPid))
         {
-            try
-            {
-                if (int.TryParse(args[0], out _powerToysRunnerPid))
-                {
-                    Logger.LogInfo($"QuickAccent started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");
-
-                    RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
-                    {
-                        Logger.LogInfo("PowerToys Runner exited. Exiting QuickAccent");
-                        Terminate();
-                    });
-                }
-            }
-            catch (Exception ex)
+            Logger.LogInfo($"QuickAccent started from the PowerToys Runner. Runner pid={_powerToysRunnerPid}");
+            RunnerHelper.WaitForPowerToysRunner(_powerToysRunnerPid, () =>
             {
-                Debug.WriteLine(ex.Message);
-            }
+                Logger.LogInfo("PowerToys Runner exited. Exiting QuickAccent");
+                Terminate();
+            });
         }
         else
         {
-            Logger.LogInfo($"QuickAccent started detached from PowerToys Runner.");
+            Logger.LogInfo("QuickAccent started detached from PowerToys Runner.");
             _powerToysRunnerPid = -1;
         }
     }
 
     private static void Terminate()
     {
-        Application.Current.Dispatcher.BeginInvoke(() =>
+        var app = App.Current;
+        var queue = app?.DispatcherQueueForApp;
+
+        // If the exit signal arrives during the brief startup window before OnLaunched has set
+        // DispatcherQueueForApp (e.g. the runner dies, or disable() is called, right after launch),
+        // or the queue is already draining, TryEnqueue can't run our cleanup. Fall back to a hard
+        // exit so we never orphan the process with the low-level keyboard hook still installed. The
+        // OS releases the hook on process termination; usage stats are simply not saved on this path.
+        if (queue is null || !queue.TryEnqueue(() =>
         {
             _tokenSource.Cancel();
-            Application.Current.Shutdown();
-        });
+            App.Window?.Dispose();   // MainWindow.SaveUsageInfo + Core.PowerAccent.Dispose on the UI thread
+            app.Dispose();   // disposes ETWTrace (idempotent via _disposed guard)
+            app.Exit();
+        }))
+        {
+            Environment.Exit(0);
+        }
     }
 }
```
### src/modules/poweraccent/PowerAccent.UI/Selector.xaml  (+0/-129)
```diff
@@ -1,129 +0,0 @@
-﻿<Window
-    x:Class="PowerAccent.UI.Selector"
-    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
-    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
-    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
-    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006"
-    Title="MainWindow"
-    MinWidth="0"
-    MinHeight="0"
-    AllowsTransparency="True"
-    Background="Transparent"
-    DataContext="{Binding RelativeSource={RelativeSource Self}}"
-    ResizeMode="NoResize"
-    ShowInTaskbar="False"
-    SizeChanged="Window_SizeChanged"
-    SizeToContent="WidthAndHeight"
-    Visibility="Collapsed"
-    WindowStyle="None"
-    mc:Ignorable="d">
-    <Window.Resources>
-
-        <DataTemplate x:Key="DefaultKeyTemplate">
-            <TextBlock
-                VerticalAlignment="Center"
-                FontSize="18"
-                Foreground="{DynamicResource TextFillColorPrimaryBrush}"
-                Text="{Binding}"
-                TextAlignment="Center" />
-        </DataTemplate>
-
-        <DataTemplate x:Key="SelectedKeyTemplate">
-            <TextBlock
-                VerticalAlignment="Center"
-                FontSize="18"
-                Foreground="{DynamicResource TextOnAccentFillColorPrimaryBrush}"
-                Text="{Binding}"
-                TextAlignment="Center" />
-        </DataTemplate>
-
-    </Window.Resources>
-    <Border
-        Background="{DynamicResource SolidBackgroundFillColorBaseBrush}"
-        BorderBrush="{DynamicResource SurfaceStrokeColorDefaultBrush}"
-        BorderThickness="1"
-        CornerRadius="8">
-        <Grid>
-            <Grid.RowDefinitions>
-                <RowDefinition Height="Auto" />
-                <RowDefinition Height="Auto" />
-            </Grid.RowDefinitions>
-            <ListBox
-                x:Name="characters"
-                HorizontalAlignment="Center"
-                HorizontalContentAlignment="Stretch"
-                VerticalContentAlignment="Stretch"
-                Background="Transparent"
-                Focusable="False"
-                IsHitTestVisible="False"
-                ScrollViewer.HorizontalScrollBarVisibility="Auto">
-                <ListBox.ItemContainerStyle>
-                    <Style TargetType="ListBoxItem">
-                        <Setter Property="Focusable" Value="False" />
-                        <Setter Property="ContentTemplate" Value="{StaticResource DefaultKeyTemplate}" />
-                        <Setter Property="Template">
-                            <Setter.Value>
-                                <ControlTemplate TargetType="{x:Type ListBoxItem}">
-                                    <Grid
-                                        Height="48"
-                                        MinWidth="48"
-                                        Margin="0"
-                                        HorizontalAlignment="Center"
-                                        VerticalAlignment="Center"
-                                        SnapsToDevicePixels="true">
-                                        <Rectangle
-                                            x:Name="SelectionIndicator"
-                                            Margin="7"
-                                            HorizontalAlignment="Stretch"
-                                            VerticalAlignment="Stretch"
-                                            Fill="{DynamicResource AccentFillColorDefaultBrush}"
-                                            RadiusX="4"
-                                            RadiusY="4"
-                                            Stroke="{DynamicResource AccentControlElevationBorderBrush}"
-                                            StrokeThickness="1"
-                                            Visibility="Collapsed" />
-                                        <ContentPresenter Margin="12" />
-                                    </Grid>
-                                    <ControlTemplate.Triggers>
-                                        <Trigger Property="IsSelected" Value="true">
-                                            <Setter TargetName="SelectionIndicator" Property="Visibility" Value="Visible" />
-                                            <Setter Property="ContentTemplate" Value="{StaticResource SelectedKeyTemplate}" />
-                                        </Trigger>
-                                    </ControlTemplate.Triggers>
-                                </ControlTemplate>
-                            </Setter.Value>
-                        </Setter>
-                    </Style>
-                </ListBox.ItemContainerStyle>
-                <ListBox.ItemsPanel>
-                    <ItemsPanelTemplate>
-                        <StackPanel Orientation="Horizontal" />
-                    </ItemsPanelTemplate>
-                </ListBox.ItemsPanel>
-            </ListBox>
-
-            <Grid
-                Grid.Row="1"
-                MinWidth="600"
-                MaxWidth="{Binding ActualWidth, ElementName=characters}"
-                Background="{DynamicResource LayerOnAcrylicFillColorDefaultBrush}"
-                Visibility="{Binding CharacterNameVisibility, UpdateSourceTrigger=PropertyChanged}">
-                <TextBlock
-                    x:Name="characterName"
-                    MaxHeight="36"
-                    Margin="8"
-                    FontSize="12"
-                    Foreground="{DynamicResource TextFillColorSecondaryBrush}"
-                    Text="(U+0000) A COOL LETTER NAME COMES HERE"
-                    TextAlignment="Center"
-                    TextTrimming="CharacterEllipsis"
-                    TextWrapping="Wrap" />
-                <Rectangle
-                    Height="1"
-                    HorizontalAlignment="Stretch"
-                    VerticalAlignment="Top"
-                    Fill="{DynamicResource DividerStrokeColorDefaultBrush}" />
-            </Grid>
-        </Grid>
-    </Border>
-</Window>
\ No newline at end of file
```
### src/modules/poweraccent/PowerAccent.UI/Selector.xaml.cs  (+0/-185)
```diff
@@ -1,185 +0,0 @@
-﻿// Copyright (c) Microsoft Corporation
-// The Microsoft Corporation licenses this file to you under the MIT license.
-// See the LICENSE file in the project root for more information.
-
-using System;
-using System.ComponentModel;
-using System.Windows;
-using Windows.Win32;
-using Windows.Win32.Foundation;
-using Windows.Win32.UI.WindowsAndMessaging;
-using Point = PowerAccent.Core.Point;
-using Size = PowerAccent.Core.Size;
-
-namespace PowerAccent.UI;
-
-public partial class Selector : Window, IDisposable, INotifyPropertyChanged
-{
-    // When setting the position for the selector window, we do not alter the z-order,
-    // activation status, or size.
-    private const SET_WINDOW_POS_FLAGS WindowPosFlags =
-        SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE;
-
-    private readonly Core.PowerAccent _powerAccent = new();
-
-    private Visibility _characterNameVisibility = Visibility.Visible;
-
-    private int _selectedIndex = -1;
-
-    public event PropertyChangedEventHandler PropertyChanged;
-
-    public Visibility CharacterNameVisibility
-    {
-        get
-        {
-            return _characterNameVisibility;
-        }
-
-        set
-        {
-            _characterNameVisibility = value;
-            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CharacterNameVisibility)));
-        }
-    }
-
-    public Selector()
-    {
-        InitializeComponent();
-
-        Application.Current.MainWindow.ShowActivated = false;
-    }
-
-    protected override void OnSourceInitialized(EventArgs e)
-    {
-        base.OnSourceInitialized(e);
-        _powerAccent.OnChangeDisplay += PowerAccent_OnChangeDisplay;
-        _powerAccent.OnSelectCharacter += PowerAccent_OnSelectionCharacter;
-        this.Visibility = Visibility.Hidden;
-    }
-
-    private void PowerAccent_OnSelectionCharacter(int index, string character)
-    {
-        _selectedIndex = index;
-        characters.SelectedIndex = _selectedIndex;
-
-        if (_selectedIndex >= 0 && _selectedIndex < _powerAccent.CharacterDescriptions.Length)
-        {
-            characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
-        }
-
-        if (characters.Items.Count > _selectedIndex && _selectedIndex >= 0)
-        {
-            characters.ScrollIntoView(characters.Items[_selectedIndex]);
-        }
-    }
-
-    private void PowerAccent_OnChangeDisplay(bool isActive, string[] chars)
-    {
-        // Topmost is conditionally set here to address hybrid graphics issues on laptops.
-        this.Topmost = isActive;
-
-        CharacterNameVisibility = _powerAccent.ShowUnicodeDescription ? Visibility.Visible : Visibility.Collapsed;
-
-        if (isActive)
-        {
-            int offscreenX = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN) - 1000;
-            int offscreenY = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN) - 1000;
-
-            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
-            if (hwnd != IntPtr.Zero)
-            {
-                // Move off-screen to avoid flicker on previous monitor before Show() and
-                // UpdateLayout().
-                PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, offscreenX, offscreenY, 0, 0, WindowPosFlags);
-            }
-            else
-            {
-                this.Left = offscreenX;
-                this.Top = offscreenY;
-            }
-
-            Show();
-            SetWindowsSize();
-            characters.ItemsSource = chars;
-            characters.SelectedIndex = -1; // Reset before setting dynamically to avoid flashing
-
-            this.UpdateLayout(); // Required for filling the actual width/height before positioning.
-
-            characters.SelectedIndex = _selectedIndex;
-
-            if (_selectedIndex >= 0 && _selectedIndex < chars.Length)
-            {
-                characterName.Text = _powerAccent.CharacterDescriptions[_selectedIndex];
-                characters.ScrollIntoView(characters.Items[_selectedIndex]);
-                this.UpdateLayout(); // Re-layout after scrolling
-            }
-            else
-            {
-                characterName.Text = string.Empty;
-            }
-
-            SetWindowPosition();
-            Microsoft.PowerToys.Telemetry.PowerToysTelemetry.Log.WriteEvent(new PowerAccent.Core.Telemetry.PowerAccentShowAccentMenuEvent());
-        }
-        else
-        {
-            Hide();
-            characters.ItemsSource = null;
-            _selectedIndex = -1;
-        }
-    }
-
-    private void MenuExit_Click(object sender, RoutedEventArgs e)
-    {
-        Application.Current.Shutdown();
-    }
-
-    private void SetWindowPosition()
-    {
-        Size windowSize = new(((FrameworkElement)Application.Current.MainWindow.Content).ActualWidth, ((FrameworkElement)Application.Current.MainWindow.Content).ActualHeight);
-        Point physicalPosition = _powerAccent.GetDisplayCoordinates(windowSize);
-
-        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
-        if (hwnd != IntPtr.Zero)
-        {
-            PInvoke.SetWindowPos((HWND)hwnd, (HWND)IntPtr.Zero, (int)Math.Round(physicalPosition.X), (int)Math.Round(physicalPosition.Y), 0, 0, WindowPosFlags);
-        }
-    }
-
-    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
-    {
-        base.OnDpiChanged(oldDpi, newDpi);
-        if (this.Visibility == Visibility.Visible)
-        {
-            SetWindowsSize();
-            SetWindowPosition();
-        }
-    }
-
-    private void SetWindowsSize()
-    {
-        double maxWidth = _powerAccent.GetDisplayMaxWidth();
-        this.characters.MaxWidth = maxWidth;
-        this.MaxWidth = maxWidth;
-    }
-
-    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
-    {
-        if (this.Visibility == Visibility.Visible)
-        {
-            SetWindowPosition();
-        }
-    }
-
-    protected override void OnClosed(EventArgs e)
-    {
-        _powerAccent.SaveUsageInfo();
-        _powerAccent.Dispose();
-        base.OnClosed(e);
-    }
-
-    public void Dispose()
-    {
-        GC.SuppressFinalize(this);
-    }
-}
```
### src/modules/poweraccent/PowerAccent.UI/SelectorViewModel.cs  (+36/-0)
```diff
@@ -0,0 +1,36 @@
+// Copyright (c) Microsoft Corporation
+// The Microsoft Corporation licenses this file to you under the MIT license.
+// See the LICENSE file in the project root for more information.
+
+using System.Collections.ObjectModel;
+
+using CommunityToolkit.Mvvm.ComponentModel;
+using Microsoft.UI.Xaml;
+
+namespace PowerAccent.UI;
+
+public partial class SelectorViewModel : ObservableObject
+{
+    // Partial properties (not [ObservableProperty] fields): the CsWinRT generators need partial
+    // properties to emit correct WinRT marshalling for a WinUI 3 app (otherwise MVVMTK0045).
+    // Partial properties cannot carry field initializers, so initial values are set in the ctor.
+    [ObservableProperty]
+    public partial ObservableCollection<string> Characters { get; set; }
+
+    [ObservableProperty]
+    public partial string Description { get; set; }
+
+    // Exposed directly as a Visibility (rather than binding the bool through a
+    // BoolToVisibilityConverter) so the description row's visibility needs no converter resource.
+    [ObservableProperty]
+    [NotifyPropertyChangedFor(nameof(DescriptionVisibility))]
+    public partial bool ShowDescription { get; set; }
+
+    public SelectorViewModel()
+    {
+        Characters = new ObservableCollection<string>();
+        Description = string.Empty;
+    }
+
+    public Visibility DescriptionVisibility => ShowDescription ? Visibility.Visible : Visibility.Collapsed;
+}
```
### src/modules/poweraccent/PowerAccentKeyboardService/PowerAccentKeyboardService.vcxproj  (+1/-1)
```diff
@@ -46,7 +46,7 @@
   <PropertyGroup Label="UserMacros" />
   <PropertyGroup>
     <TargetName>PowerToys.PowerAccentKeyboardService</TargetName>
-    <OutDir>$(RepoRoot)$(Platform)\$(Configuration)\</OutDir>
+    <OutDir>$(RepoRoot)$(Platform)\$(Configuration)\WinUI3Apps\</OutDir>
   </PropertyGroup>
   <ItemDefinitionGroup>
     <ClCompile>
```
### src/modules/poweraccent/PowerAccentModuleInterface/dllmain.cpp  (+1/-1)
```diff
@@ -59,7 +59,7 @@ class PowerAccent : public PowertoyModuleIface
         unsigned long powertoys_pid = GetCurrentProcessId();
 
         std::wstring executable_args = L"" + std::to_wstring(powertoys_pid);
-        std::wstring application_path = L"PowerToys.PowerAccent.exe";
+        std::wstring application_path = L"WinUI3Apps\\PowerToys.PowerAccent.exe";
         std::wstring full_command_path = application_path + L" " + executable_args.data();
         Logger::trace(L"PowerToys QuickAccent launching: " + full_command_path);
 
```
### src/settings-ui/Settings.UI.Library/KeysDataModel.cs  (+0/-1)
```diff
@@ -9,7 +9,6 @@
 using System.Globalization;
 using System.IO;
 using System.Linq;
-using System.Management;
 using System.Text.Json;
 using System.Text.Json.Serialization;
 using System.Threading;
```
### tools/Verification scripts/verify-installation-script.ps1  (+10/-6)
```diff
@@ -432,12 +432,9 @@ function Test-CoreFiles {
         'PowerToys.MouseWithoutBordersHelper.dll',
         'PowerToys.MouseWithoutBordersHelper.exe',
         
-        # PowerAccent
-        'PowerAccent.Core.dll',
-        'PowerToys.PowerAccent.dll',
-        'PowerToys.PowerAccent.exe',
+        # PowerAccent - only the runner-loaded module interface ships in the install root.
+        # The app, core, common and keyboard-service binaries moved to WinUI3Apps (see $winUI3SignedFiles).
         'PowerToys.PowerAccentModuleInterface.dll',
-        'PowerToys.PowerAccentKeyboardService.dll',
         
         # Workspaces
         'PowerToys.WorkspacesSnapshotTool.exe',
@@ -500,7 +497,14 @@ function Test-CoreFiles {
         'PowerToys.RegistryPreviewExt.dll',
         'PowerToys.RegistryPreviewUILib.dll',
         'PowerToys.RegistryPreview.dll',
-        'PowerToys.RegistryPreview.exe'
+        'PowerToys.RegistryPreview.exe',
+
+        # PowerAccent (Quick Accent) - moved from the install root to WinUI3Apps
+        'PowerAccent.Core.dll',
+        'PowerAccent.Common.dll',
+        'PowerToys.PowerAccent.dll',
+        'PowerToys.PowerAccent.exe',
+        'PowerToys.PowerAccentKeyboardService.dll'
     )
     
     # Tools signed files (in Tools subdirectory)
```