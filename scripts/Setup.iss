; SonarController installer definition (Inno Setup)
; Build with scripts\BuildFull.ps1 / BuildTrial.ps1 to use the project version consistently.

; MyEdition (Full / Trial) is passed from _Build.ps1
#ifndef MyEdition
  #define MyEdition "Full"
#endif

#define MyAppExeName "SonarController.exe"
#define MyPublishDir "..\publish\" + MyEdition

; Trial uses a separate AppId/name/folder so it never overwrites the Full install
#if MyEdition == "Trial"
  #define MyAppName "SonarController Trial"
  #define MyAppId "{{B1C1B6C1-6F5D-4E9A-9C3B-2A6E7F9D1A21}"
#else
  #define MyAppName "SonarController"
  #define MyAppId "{{B1C1B6C1-6F5D-4E9A-9C3B-2A6E7F9D1A20}"
#endif

[Setup]
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
SetupIconFile=..\Assets\SonarController-App.ico
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=SonarController-Setup-{#MyAppVersion}-{#MyEdition}
Compression=lzma2
SolidCompression=yes
ArchitecturesInstallIn64BitMode=x64compatible
; 起動中のEXEを検出したら自動でクローズしてからファイルを上書きする
CloseApplications=yes
CloseApplicationsFilter={#MyAppExeName}
RestartApplications=no

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional icons:"

[Files]
Source: "{#MyPublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
; Terminate the resident process before uninstalling
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden skipifdoesntexist; RunOnceId: "TerminateSonarController"
