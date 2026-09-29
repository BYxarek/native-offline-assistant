Add-Type -AssemblyName UIAutomationClient
if (Get-Process OfflineAssistant -ErrorAction SilentlyContinue) { throw 'Close the running assistant before this check' }
$settings = Join-Path $env:LOCALAPPDATA 'Sphere\settings.json'
$original = if (Test-Path $settings) { [IO.File]::ReadAllBytes($settings) } else { $null }
$exe = Join-Path $PSScriptRoot '..\OfflineAssistant\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\OfflineAssistant.exe'
$process = $null
try {
    New-Item (Split-Path $settings) -ItemType Directory -Force | Out-Null
    [IO.File]::WriteAllText($settings, '{"Mode":0,"WakeWord":"проверка","MicrophoneDevice":-1,"WaitSeconds":30}')
    $process = Start-Process -FilePath $exe -ArgumentList '--check-wake-timeout' -WorkingDirectory (Split-Path $exe) -PassThru
    $window = $null
    for ($i = 0; $i -lt 100 -and $null -eq $window; $i++) {
        Start-Sleep -Milliseconds 100
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)))
    }
    if ($null -eq $window) { throw 'Main window not found' }
    $chat = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '☰   Чат')))
    if ($null -eq $chat) { throw 'Chat button not found' }
    $chat.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $findStatus = {
        param($name)
        $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, $name)))
    }
    $active = $null
    for ($i = 0; $i -lt 50 -and $null -eq $active; $i++) {
        Start-Sleep -Milliseconds 100
        $active = & $findStatus 'Слушаю вас…'
    }
    if ($null -eq $active) { throw 'Wake word did not activate listening' }
    Start-Sleep -Seconds 4
    if (-not (& $findStatus 'Слушаю вас…')) { throw 'Listening stopped before five seconds' }
    $inactive = $null
    for ($i = 0; $i -lt 30 -and $null -eq $inactive; $i++) {
        Start-Sleep -Milliseconds 100
        $inactive = & $findStatus 'Жду слово «проверка»'
    }
    if ($null -eq $inactive) { throw 'Listening did not stop after five seconds' }
    'Wake word without a command times out after five seconds'
}
finally {
    if ($process) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    if ($null -eq $original) { Remove-Item -LiteralPath $settings -ErrorAction SilentlyContinue }
    else { [IO.File]::WriteAllBytes($settings, $original) }
}
