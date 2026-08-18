# Polls until SendInput fully injects a non-modifier probe key (VK_F24 down+up,
# ret==2). Reports lastErr each cycle so a locked workstation (ERROR_ACCESS_DENIED=5)
# is visible. Exits 0 when input is alive, 1 when the budget elapses.
param([int]$Minutes = 10, [int]$IntervalSec = 15)
if (-not [Environment]::Is64BitProcess) {
    throw "wait_input.ps1 requires a 64-bit PowerShell host: the INPUT struct layout (Size=40, FieldOffset(8)) is x64-only. Re-run under 64-bit PowerShell."
}
if (-not ('Pr' -as [type])) {
Add-Type @'
using System;using System.Runtime.InteropServices;
public class Pr{
 [StructLayout(LayoutKind.Sequential)] public struct KEYBDINPUT{ public ushort wVk; public ushort wScan; public uint dwFlags; public uint time; public IntPtr dwExtraInfo; }
 [StructLayout(LayoutKind.Explicit,Size=40)] public struct INPUT{ [FieldOffset(0)] public uint type; [FieldOffset(8)] public KEYBDINPUT ki; }
 [DllImport("user32.dll",SetLastError=true)] public static extern uint SendInput(uint n, INPUT[] p, int cb);
 [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
 [DllImport("kernel32.dll")] public static extern uint GetLastError();
 public static uint LastErr; public static long Fg;
 public static uint Probe(){ INPUT[] a=new INPUT[2]; a[0].type=1; a[0].ki.wVk=0x87; a[1].type=1; a[1].ki.wVk=0x87; a[1].ki.dwFlags=2; uint r=SendInput(2,a,Marshal.SizeOf(typeof(INPUT))); LastErr=GetLastError(); Fg=(long)GetForegroundWindow(); return r; }
}
'@
}
$deadline = (Get-Date).AddMinutes($Minutes)
while ((Get-Date) -lt $deadline) {
  $r = [Pr]::Probe()
  Write-Host ("probe ret={0} lastErr={1} fg={2} time={3}" -f $r,[Pr]::LastErr,[Pr]::Fg,(Get-Date -Format HH:mm:ss))
  if ($r -eq 2 -and [Pr]::Fg -ne 0) { Write-Host "INPUT: OK"; exit 0 }
  Start-Sleep $IntervalSec
}
Write-Host "INPUT: STILL BLOCKED after $Minutes min"; exit 1
