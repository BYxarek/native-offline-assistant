$native = @'
using System;
using System.Runtime.InteropServices;
public static class OrbCheckNative {
    public delegate bool EnumProc(IntPtr window, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] public static extern int GetWindowRgn(IntPtr window, IntPtr region);
    [DllImport("gdi32.dll")] public static extern int GetRgnBox(IntPtr region, out Rect rect);
    [DllImport("gdi32.dll")] public static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);
    [DllImport("gdi32.dll")] public static extern bool PtInRegion(IntPtr region, int x, int y);
    [DllImport("gdi32.dll")] public static extern bool DeleteObject(IntPtr handle);
    [DllImport("user32.dll")] public static extern void keybd_event(byte key, byte scan, uint flags, IntPtr extra);
}
'@
Add-Type $native

if (Get-Process OfflineAssistant -ErrorAction SilentlyContinue) { throw 'Close the running assistant before checking the orb window' }
$settings = Join-Path $env:LOCALAPPDATA 'Sphere\settings.json'
$original = if (Test-Path $settings) { [IO.File]::ReadAllBytes($settings) } else { $null }
$exe = Join-Path $PSScriptRoot '..\OfflineAssistant\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\OfflineAssistant.exe'
$process = $null
try {
    New-Item (Split-Path $settings) -ItemType Directory -Force | Out-Null
    [IO.File]::WriteAllText($settings, '{"Mode":1,"Modifier":"None","Key":"F8","ShowOrb":true,"Corner":3,"WaitSeconds":5,"WakeWord":"\u0441\u0444\u0435\u0440\u0430"}')
    $process = Start-Process -FilePath $exe -ArgumentList '--background' -WorkingDirectory (Split-Path $exe) -PassThru
    $script:testPid = [uint32]$process.Id
    Start-Sleep -Seconds 8
    [OrbCheckNative]::keybd_event(0x77, 0, 0, [IntPtr]::Zero)
    [OrbCheckNative]::keybd_event(0x77, 0, 2, [IntPtr]::Zero)
    $script:orb = [IntPtr]::Zero
    $callback = [OrbCheckNative+EnumProc]{ param($window, $data)
        [uint32]$owner = 0
        [void][OrbCheckNative]::GetWindowThreadProcessId($window, [ref]$owner)
        if ($owner -eq $script:testPid -and [OrbCheckNative]::IsWindowVisible($window)) { $script:orb = $window }
        return $true
    }
    for ($attempt = 0; $attempt -lt 100 -and $script:orb -eq [IntPtr]::Zero; $attempt++) {
        [void][OrbCheckNative]::EnumWindows($callback, [IntPtr]::Zero)
        if ($script:orb -eq [IntPtr]::Zero) { Start-Sleep -Milliseconds 100 }
    }
    if ($script:orb -eq [IntPtr]::Zero) { throw 'Orb window not visible' }
    $region = [OrbCheckNative]::CreateRectRgn(0, 0, 0, 0)
    try {
        if ([OrbCheckNative]::GetWindowRgn($script:orb, $region) -lt 2) { throw 'Orb window has no circular region' }
        if ([OrbCheckNative]::PtInRegion($region, 0, 0)) { throw 'Orb corner is visible' }
        [OrbCheckNative+Rect]$bounds = New-Object OrbCheckNative+Rect
        [void][OrbCheckNative]::GetRgnBox($region, [ref]$bounds)
        if (-not [OrbCheckNative]::PtInRegion($region, [int](($bounds.Left + $bounds.Right) / 2), [int](($bounds.Top + $bounds.Bottom) / 2))) { throw 'Orb center is hidden' }
        if (-not [OrbCheckNative]::PtInRegion($region, [int]($bounds.Right * 0.08), [int](($bounds.Top + $bounds.Bottom) / 2))) { throw 'Orb orbit is clipped' }
        'Orb window region: transparent corner, visible sphere'
    }
    finally { [void][OrbCheckNative]::DeleteObject($region) }
}
finally {
    if ($process) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    if ($null -eq $original) { Remove-Item -LiteralPath $settings -ErrorAction SilentlyContinue }
    else { [IO.File]::WriteAllBytes($settings, $original) }
}
