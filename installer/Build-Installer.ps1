$ErrorActionPreference = 'Stop'
$project = Join-Path $PSScriptRoot '..\OfflineAssistant\OfflineAssistant.csproj'
$metadata = [xml](Get-Content -LiteralPath $project -Raw)
$version = [string]$metadata.Project.PropertyGroup[0].Version
$build = [string]$metadata.Project.PropertyGroup[0].BuildNumber
# ponytail: Windows file versions cap each component at 65535; split the build across components if releases reach that limit.
if ($version -notmatch '^\d+\.\d+\.\d+$' -or $build -notmatch '^\d{7}$' -or [int]$build -gt 65535) {
    throw 'Expected Version x.y.z and BuildNumber 0000001–0065535 in the project file.'
}
$fileVersion = "$version.$([int]$build)"
$compiler = 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
if (-not (Test-Path $compiler)) { throw 'Install Inno Setup 6.' }
$publishDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\OfflineAssistant\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish'))
$projectDir = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\OfflineAssistant'))
if (-not $publishDir.StartsWith($projectDir + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Unsafe publish directory.' }
if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }
dotnet publish $project -c Release -p:Platform=x64 -r win-x64 --self-contained true -p:PublishTrimmed=false -p:PublishReadyToRun=false -p:SatelliteResourceLanguages=ru -p:PublishSingleFile=true -p:IncludeAllContentForSelfExtract=true -p:EnableCompressionInSingleFile=true
if ($LASTEXITCODE -ne 0) { throw 'Publishing failed.' }
$ttsBuild = Join-Path $PSScriptRoot 'tts-build'
$python = Join-Path $ttsBuild 'venv\Scripts\python.exe'
$ttsScript = Join-Path $PSScriptRoot 'silero-tts.py'
$ttsExe = Join-Path $ttsBuild 'dist\silero-tts\silero-tts.exe'
if (-not (Test-Path -LiteralPath $ttsExe) -or (Get-Item -LiteralPath $ttsScript).LastWriteTimeUtc -gt (Get-Item -LiteralPath $ttsExe).LastWriteTimeUtc) {
    if (-not (Test-Path -LiteralPath $python)) {
        uv venv (Join-Path $ttsBuild 'venv') --python 3.12
        if ($LASTEXITCODE -ne 0) { throw 'Creating Python environment failed. Install uv.' }
        uv pip install --python $python 'torch==2.14.0+cpu' --index-url https://download.pytorch.org/whl/cpu
        if ($LASTEXITCODE -ne 0) { throw 'Installing PyTorch failed.' }
        uv pip install --python $python 'pyinstaller==6.22.3'
        if ($LASTEXITCODE -ne 0) { throw 'Installing PyInstaller failed.' }
    }
    & $python -m PyInstaller --noconfirm --onedir --name silero-tts --distpath (Join-Path $ttsBuild 'dist') --workpath (Join-Path $ttsBuild 'work') --specpath $ttsBuild $ttsScript
    if ($LASTEXITCODE -ne 0) { throw 'Building Silero helper failed.' }
}
$ttsModel = Join-Path $ttsBuild 'cache\v5_5_ru.pt'
if (-not (Test-Path -LiteralPath $ttsModel)) {
    New-Item -ItemType Directory -Force (Split-Path -Parent $ttsModel) | Out-Null
    Invoke-WebRequest -Uri 'https://models.silero.ai/models/tts/ru/v5_5_ru.pt' -OutFile $ttsModel
}
if ((Get-FileHash -LiteralPath $ttsModel -Algorithm SHA256).Hash -ne '50081637B602126EE06CB3BC8A744D25651D2DA149EE8864B9A379BFDD934437') { throw 'Silero model checksum mismatch.' }
$ttsPublish = Join-Path $publishDir 'Tts'
New-Item -ItemType Directory -Force $ttsPublish | Out-Null
Copy-Item -LiteralPath (Split-Path -Parent $ttsExe) -Destination $ttsPublish -Recurse -Force
Copy-Item -LiteralPath $ttsModel -Destination $ttsPublish
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SILERO-LICENSE.txt') -Destination $ttsPublish
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SILERO-NOTICE.txt') -Destination $ttsPublish
$probe = Join-Path $ttsBuild 'cache\build-check.wav'
try {
    'Это проверка озвучивания.' | & (Join-Path $ttsPublish 'silero-tts\silero-tts.exe') (Join-Path $ttsPublish 'v5_5_ru.pt') $probe 'xenia'
    if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $probe) -or (Get-Item -LiteralPath $probe).Length -lt 1000) { throw 'Silero voice check failed.' }
}
finally { if (Test-Path -LiteralPath $probe) { Remove-Item -LiteralPath $probe -Force } }
$drive = 'Z:'
if (Get-PSDrive -Name 'Z' -ErrorAction SilentlyContinue) { throw 'Drive Z: is required for the short installer source path.' }
try {
    & subst.exe $drive $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'Creating short installer source path failed.' }
    & $compiler "/DDisplayVersion=$version" "/DBuildNumber=$build" "/DFileVersion=$fileVersion" "/DPublishDir=$drive" (Join-Path $PSScriptRoot 'Sphere.iss')
    if ($LASTEXITCODE -ne 0) { throw 'Installer build failed.' }
}
finally { & subst.exe $drive /D }
