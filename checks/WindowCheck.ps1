$source = @'
using System;
using System.Runtime.InteropServices;
public static class WindowCheckNative {
    public delegate bool EnumProc(IntPtr hwnd, IntPtr data);
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc callback, IntPtr data);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint processId);
    [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] public static extern IntPtr GetWindowLongPtr(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern bool GetLayeredWindowAttributes(IntPtr hwnd, out uint key, out byte alpha, out uint flags);
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
Add-Type $source
$exe = Join-Path $PSScriptRoot '..\OfflineAssistant\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\OfflineAssistant.exe'
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
try {
    Start-Sleep -Seconds 5
    $script:window = [IntPtr]::Zero
    $script:largestArea = 0
    $callback = [WindowCheckNative+EnumProc]{ param($hwnd, $data)
        [uint32]$owner = 0
        [void][WindowCheckNative]::GetWindowThreadProcessId($hwnd, [ref]$owner)
        if ($owner -eq $process.Id) {
            [WindowCheckNative+Rect]$candidate = New-Object WindowCheckNative+Rect
            [void][WindowCheckNative]::GetWindowRect($hwnd, [ref]$candidate)
            $area = ($candidate.Right - $candidate.Left) * ($candidate.Bottom - $candidate.Top)
            if ($area -gt $script:largestArea) { $script:largestArea = $area; $script:window = $hwnd }
        }
        return $true
    }
    [void][WindowCheckNative]::EnumWindows($callback, [IntPtr]::Zero)
    if ($script:window -eq [IntPtr]::Zero) { throw 'Main window not found' }
    $style = [WindowCheckNative]::GetWindowLongPtr($script:window, -20).ToInt64()
    [WindowCheckNative+Rect]$rect = New-Object WindowCheckNative+Rect
    [void][WindowCheckNative]::GetWindowRect($script:window, [ref]$rect)
    $screenWidth = [WindowCheckNative]::GetSystemMetrics(0)
    $screenHeight = [WindowCheckNative]::GetSystemMetrics(1)
    "opaque=$((($style -band 0x80000) -eq 0)) window=$($rect.Right - $rect.Left)x$($rect.Bottom - $rect.Top) screen=${screenWidth}x${screenHeight}"
    if (($style -band 0x80000) -ne 0) { throw 'Window still uses layered transparency' }
    if (($rect.Right - $rect.Left) -lt $screenWidth -or ($rect.Bottom - $rect.Top) -lt $screenHeight) { throw 'Window is not fullscreen' }
}
finally { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
