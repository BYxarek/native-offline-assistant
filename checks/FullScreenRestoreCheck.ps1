Add-Type -AssemblyName UIAutomationClient
$native = @'
using System;
using System.Runtime.InteropServices;
public static class RestoreCheckNative {
    [StructLayout(LayoutKind.Sequential)] public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr window, out Rect rect);
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
Add-Type $native

if (Get-Process OfflineAssistant -ErrorAction SilentlyContinue) { throw 'Close the running assistant before this check' }
$exe = Join-Path $PSScriptRoot '..\OfflineAssistant\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\OfflineAssistant.exe'
$process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
try {
    $window = $null
    for ($i = 0; $i -lt 50 -and $null -eq $window; $i++) {
        Start-Sleep -Milliseconds 200
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)))
    }
    if ($null -eq $window) { throw 'Main window not found' }
    $button = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Свернуть')))
    if ($null -eq $button) {
        $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition) |
            ForEach-Object { $_.Current.Name } | Select-Object -First 30
        throw 'Minimize button not found'
    }
    $button.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
    $request = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
    [void]$request.WaitForExit(10000)
    Start-Sleep -Seconds 1
    $bounds = New-Object RestoreCheckNative+Rect
    [void][RestoreCheckNative]::GetWindowRect([IntPtr]$window.Current.NativeWindowHandle, [ref]$bounds)
    $width = $bounds.Right - $bounds.Left
    $height = $bounds.Bottom - $bounds.Top
    "restored=${width}x${height} screen=$([RestoreCheckNative]::GetSystemMetrics(0))x$([RestoreCheckNative]::GetSystemMetrics(1))"
    if ($width -lt [RestoreCheckNative]::GetSystemMetrics(0) -or $height -lt [RestoreCheckNative]::GetSystemMetrics(1)) {
        throw 'Fullscreen window restored too small'
    }
}
finally { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
