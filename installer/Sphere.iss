#define AppName "Сфера"
#ifndef PublishDir
  #define PublishDir "..\OfflineAssistant\bin\Release\net10.0-windows10.0.26100.0\win-x64\publish"
#endif

[Setup]
AppId={{B8FE0DAA-255D-4D33-A556-64B172A9A0A8}
AppName={#AppName}
AppVersion={#DisplayVersion}
VersionInfoVersion={#FileVersion}
VersionInfoProductVersion={#DisplayVersion}
DefaultDirName={localappdata}\Programs\Sphere
DefaultGroupName={#AppName}
OutputDir=output
OutputBaseFilename=Sphere-Setup-{#DisplayVersion}-{#BuildNumber}-x64
Compression=lzma2
SolidCompression=yes
PrivilegesRequired=lowest
WizardStyle=modern
SetupIconFile=..\OfflineAssistant\Assets\AppIcon.ico
UninstallDisplayIcon={app}\OfflineAssistant.exe
CloseApplications=yes

[Languages]
Name: "russian"; MessagesFile: "compiler:Languages\Russian.isl"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs ignoreversion

[Icons]
Name: "{group}\Сфера"; Filename: "{app}\OfflineAssistant.exe"
Name: "{autodesktop}\Сфера"; Filename: "{app}\OfflineAssistant.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Создать значок на рабочем столе"; GroupDescription: "Дополнительные значки:"; Flags: unchecked

[Registry]
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Sphere"; ValueData: """{app}\OfflineAssistant.exe"" --background"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\OfflineAssistant.exe"; Description: "Запустить Сферу"; Flags: nowait postinstall skipifsilent
