; Inno Setup 6 script for Desktop Dashboard.
; Build with installer\build.ps1, which publishes the app first and passes AppVersion/PublishDir.

#ifndef AppVersion
  #define AppVersion "0.1.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish\win-x64"
#endif

#define AppName "Desktop Dashboard"
#define AppExe "DesktopDashboard.exe"
#define RunKey "Software\Microsoft\Windows\CurrentVersion\Run"

[Setup]
AppId={{8F5B0C3E-6A4D-4C1B-9E2F-3D7A1B5C9E44}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=gaybullayevumid
VersionInfoVersion={#AppVersion}
; Per-user install: no admin prompt, matches the per-user startup entry the app manages.
PrivilegesRequired=lowest
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir=..\artifacts\installer
OutputBaseFilename=DesktopDashboard-{#AppVersion}-win-x64-setup
SetupIconFile=..\src\DesktopDashboard\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExe}
WizardStyle=modern
Compression=lzma2/ultra64
SolidCompression=yes
; Close a running dashboard before replacing its files.
CloseApplications=force
RestartApplications=no

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startup"; Description: "Start {#AppName} when I sign in to Windows"; GroupDescription: "Startup:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Registry]
; Same value name and command format as RunKeyStartupRegistration, so the in-app toggle sees it.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: string; ValueName: "DesktopDashboard"; \
    ValueData: """{app}\{#AppExe}"" --startup"; Tasks: startup
; Remove the startup entry on uninstall even if it was turned on from inside the app.
Root: HKCU; Subkey: "{#RunKey}"; ValueType: none; ValueName: "DesktopDashboard"; Flags: uninsdeletevalue

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im {#AppExe} /f"; Flags: runhidden; RunOnceId: "StopDashboard"
