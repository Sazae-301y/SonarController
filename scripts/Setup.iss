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
  #define MyRunValue "SonarController.Trial"
#elif MyEdition == "Full"
  #define MyAppName "SonarController"
  #define MyAppId "{{B1C1B6C1-6F5D-4E9A-9C3B-2A6E7F9D1A20}"
  #define MyRunValue "SonarController"
#else
  #error The Store edition is distributed as MSIX (scripts\BuildStore.ps1), not with Inno Setup.
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

[Registry]
; アンインストール後に自動起動の登録が残らないようにする
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "{#MyRunValue}"; ValueType: none; Flags: dontcreatekey uninsdeletevalue

[UninstallRun]
; Terminate the resident process before uninstalling
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden skipifdoesntexist; RunOnceId: "TerminateSonarController"

[UninstallDelete]
; 一時ファイル・ログは常に削除する（設定は下のCodeで確認する）
Type: filesandordirs; Name: "{localappdata}\SonarController\logs"

[Code]
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep <> usPostUninstall then Exit;
  if UninstallSilent then Exit;

  { 体験期間の記録(trial.dat)は、再インストールで体験期間をやり直せないよう削除しない }
  if MsgBox('設定ファイル（ホットキー・表示先・言語など）も削除しますか？', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
  begin
    DataDir := ExpandConstant('{localappdata}\SonarController');
    DeleteFile(DataDir + '\settings.json');
    DeleteFile(DataDir + '\language-settings.json');
    DeleteFile(DataDir + '\display-settings.json');
    DeleteFile(DataDir + '\interaction-settings.json');
  end;
end;
