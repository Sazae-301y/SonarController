; SonarController2 installer definition (Inno Setup)
; Run scripts\Publish.ps1 first to generate the publish\ folder.
; Build with: ISCC.exe scripts\Setup.iss (Inno Setup Compiler)

#define MyAppName "SonarController2"
#define MyAppVersion "1.2.0"
#define MyAppExeName "SonarController2.exe"
#define MyPublishDir "..\publish"

[Setup]
AppId={{B1C1B6C1-6F5D-4E9A-9C3B-2A6E7F9D1A20}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
SetupIconFile=..\Assets\SonarController2.ico
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=..\artifacts
OutputBaseFilename=SonarController2-Setup-{#MyAppVersion}
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
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden skipifdoesntexist; RunOnceId: "TerminateSonarController2"
