# Shared SendInput + window helpers for the AdvancedPaste sign-off harness.
# The INPUT struct layout (Explicit union of MOUSEINPUT/KEYBDINPUT with IntPtr
# fields) and Marshal.SizeOf assume a 64-bit process; SendInput will reject the
# wrong cb size otherwise. Fail fast on a 32-bit host.
if (-not [Environment]::Is64BitProcess) {
    throw "input_helpers.ps1 requires a 64-bit PowerShell host: the INPUT struct layout and SendInput cb size are x64-only. Re-run under 64-bit PowerShell (pwsh) or Windows PowerShell x64."
}
# Guard Add-Type so repeated dot-sourcing in one session does not throw
# 'type already exists'.
if (-not ('WinInput' -as [type])) {
Add-Type @"
using System; using System.Runtime.InteropServices; using System.Text;
public class WinInput {
 [StructLayout(LayoutKind.Sequential)] public struct MOUSEINPUT { public int dx; public int dy; public uint mouseData; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
 [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT { public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
 [StructLayout(LayoutKind.Explicit)] public struct InputU { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; }
 [StructLayout(LayoutKind.Sequential)] public struct INPUT { public uint type; public InputU U; }
 [DllImport("user32.dll", SetLastError=true)] public static extern uint SendInput(uint n, INPUT[] p, int cb);
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h, int c);
 [DllImport("user32.dll")] public static extern bool BringWindowToTop(IntPtr h);
 [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, IntPtr pid);
 [DllImport("user32.dll")] public static extern bool AttachThreadInput(uint a, uint b, bool f);
 [DllImport("kernel32.dll")] public static extern uint GetCurrentThreadId();
 static int SZ = Marshal.SizeOf(typeof(INPUT));
 public static uint Key(ushort vk, bool up){ INPUT[] i=new INPUT[1]; i[0].type=1; i[0].U.ki.wVk=vk; i[0].U.ki.dwFlags=(uint)(up?2:0); return SendInput(1,i,SZ); }
 public static void Combo(ushort mod, ushort key){ Key(mod,false); Key(key,false); Key(key,true); Key(mod,true); }
 public static uint Type(string s){ uint tot=0; foreach(char c in s){ INPUT[] i=new INPUT[2];
   i[0].type=1; i[0].U.ki.wScan=(ushort)c; i[0].U.ki.dwFlags=0x0004;
   i[1].type=1; i[1].U.ki.wScan=(ushort)c; i[1].U.ki.dwFlags=0x0004|0x0002;
   tot+=SendInput(2,i,SZ);} return tot; }
 // Reliable foregrounding across processes via AttachThreadInput.
 public static bool ForceForeground(IntPtr h){
   ShowWindow(h,9); // SW_RESTORE
   uint fg = GetWindowThreadProcessId(GetForegroundWindow(), IntPtr.Zero);
   uint cur = GetCurrentThreadId();
   uint tgt = GetWindowThreadProcessId(h, IntPtr.Zero);
   AttachThreadInput(cur, fg, true); AttachThreadInput(cur, tgt, true);
   BringWindowToTop(h); SetForegroundWindow(h);
   AttachThreadInput(cur, tgt, false); AttachThreadInput(cur, fg, false);
   System.Threading.Thread.Sleep(200);
   return GetForegroundWindow()==h;
 }
}
"@
}

function Get-NotepadHwnd { (Get-Process notepad -ErrorAction SilentlyContinue | Where-Object {$_.MainWindowHandle -ne 0} | Select-Object -First 1).MainWindowHandle }

function Focus-Foreground([IntPtr]$h){
    for ($i=0; $i -lt 12; $i++) {
        if ([WinInput]::ForceForeground($h)) { return $true }
        Start-Sleep -Milliseconds 200
    }
    return ([WinInput]::GetForegroundWindow() -eq $h)
}

function Clear-Notepad([IntPtr]$h){
    $ok = Focus-Foreground $h
    if (-not $ok) { Write-Host "WARN: notepad not foreground for clear" }
    [WinInput]::Combo(0x11,0x41) | Out-Null   # Ctrl+A
    Start-Sleep -Milliseconds 250
    [WinInput]::Key(0x2E,$false) | Out-Null; [WinInput]::Key(0x2E,$true) | Out-Null  # Delete
    Start-Sleep -Milliseconds 250
    return $ok
}
