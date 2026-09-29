Add-Type -AssemblyName UIAutomationClient
if (Get-Process OfflineAssistant -ErrorAction SilentlyContinue) { throw 'Close the running assistant before this check' }
$settings = Join-Path $env:LOCALAPPDATA 'Sphere\settings.json'
$original = if (Test-Path $settings) { [IO.File]::ReadAllBytes($settings) } else { $null }
$exe = Join-Path $PSScriptRoot '..\OfflineAssistant\bin\x64\Debug\net10.0-windows10.0.26100.0\win-x64\OfflineAssistant.exe'
$process = $null
try {
    New-Item (Split-Path $settings) -ItemType Directory -Force | Out-Null
    [IO.File]::WriteAllText($settings, '{"Mode":0,"WakeWord":"проверка","MicrophoneDevice":-1,"WaitSeconds":5}')
    $process = Start-Process -FilePath $exe -WorkingDirectory (Split-Path $exe) -WindowStyle Hidden -PassThru
    $window = $null
    for ($i = 0; $i -lt 50 -and $null -eq $window; $i++) {
        Start-Sleep -Milliseconds 200
        $window = [System.Windows.Automation.AutomationElement]::RootElement.FindFirst(
            [System.Windows.Automation.TreeScope]::Children,
            (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::ProcessIdProperty, $process.Id)))
    }
    if ($null -eq $window) { throw 'Main window not found' }
    $chat = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '☰   Чат')))
    if ($null -eq $chat) { throw 'Chat button not found' }
    $chat.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
    $prompt = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Скажите «проверка», чтобы начать запись речи.')))
    if ($null -eq $prompt) { throw 'Chat does not show the configured wake word' }
    'Chat shows the configured wake word'
    $settingsButton = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, '⚙   Настройки')))
    if ($null -eq $settingsButton) { throw 'Settings button not found' }
    $settingsButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    Start-Sleep -Milliseconds 400
    $microphoneSection = $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Микрофон'))) |
        Where-Object { $_.TryGetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern, [ref]$null) } |
        Select-Object -First 1
    if ($null -eq $microphoneSection) { throw 'Microphone section not found' }
    $microphoneSection.GetCurrentPattern([System.Windows.Automation.ExpandCollapsePattern]::Pattern).Expand()
    Start-Sleep -Milliseconds 300
    $microphone = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Микрофон')))
    $meter = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Уровень микрофона')))
    if ($null -eq $microphone -or $null -eq $meter) { throw 'Microphone selection or level meter not found' }
    'Settings show microphone selection and level meter'
    $testButton = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
        (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Проверить микрофон')))
    if ($null -eq $testButton) { throw 'Microphone test button not found' }
    $testButton.GetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern).Invoke()
    $testStatus = $null
    for ($i = 0; $i -lt 30 -and $null -eq $testStatus; $i++) {
        Start-Sleep -Milliseconds 500
        $testStatus = $window.FindFirst([System.Windows.Automation.TreeScope]::Descendants,
            (New-Object System.Windows.Automation.OrCondition(
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Говорите: индикатор показывает уровень звука')),
                (New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::NameProperty, 'Микрофон недоступен. Проверьте доступ для классических приложений в настройках Windows.')))))
    }
    if ($null -eq $testStatus) {
        $window.FindAll([System.Windows.Automation.TreeScope]::Descendants,
            [System.Windows.Automation.Condition]::TrueCondition) |
            ForEach-Object { $_.Current.Name } | Where-Object { $_ -match 'микрофон|индикатор|доступ|проверку' }
        throw 'Microphone test gave no result'
    }
    "Microphone test: $($testStatus.Current.Name)"
}
finally {
    if ($process) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    if ($null -eq $original) { Remove-Item -LiteralPath $settings -ErrorAction SilentlyContinue }
    else { [IO.File]::WriteAllBytes($settings, $original) }
}
