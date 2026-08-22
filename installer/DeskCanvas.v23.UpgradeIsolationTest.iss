; This installer exists only for the automated isolation upgrade trial. It has
; its own AppId, Run value and mock startup shortcut below the supplied {app}.
#define AppName "DeskCanvas Upgrade Isolation Test"
#define AppVersion "1.2.0-beta.2"
#define AppExeName "DeskCanvas.exe"
#define PayloadPath "..\artifacts\beta\DeskCanvas-1.2.0-beta.2-win-x64\DeskCanvas.exe"

[Setup]
AppId={{6CDDD3AC-E205-46DE-A0A5-88B47830A485}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={tmp}\DeskCanvas-upgrade-isolation-default
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer-tests
OutputBaseFilename=DeskCanvas-UpgradeIsolationTest-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
CloseApplications=no
RestartApplications=no
Uninstallable=yes

[InstallDelete]
Type: files; Name: "{app}\DeskCanvas.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.pdb"
Type: files; Name: "{app}\DeskCanvas.runtimeconfig.json"
Type: files; Name: "{app}\DeskCanvas.deps.json"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\legacy"
Type: filesandordirs; Name: "{app}\obsolete"
Type: filesandordirs; Name: "{app}\plugins"

[Files]
Source: "{#PayloadPath}"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Code]
const
  StartupKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupValue = 'DeskCanvas.UpgradeIsolationTest';
  MockStartupShortcut = '{app}\legacy-startup\DeskCanvas.UpgradeIsolationTest.lnk';

var
  HadStartupRegistration: Boolean;

procedure RemoveLegacyStartup;
begin
  RegDeleteValue(HKCU, StartupKey, StartupValue);
  DeleteFile(ExpandConstant(MockStartupShortcut));
end;

procedure InitializeWizard;
begin
  HadStartupRegistration := RegValueExists(HKCU, StartupKey, StartupValue) or
    FileExists(ExpandConstant(MockStartupShortcut));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    RemoveLegacyStartup;

  if CurStep = ssPostInstall then
  begin
    RemoveLegacyStartup;
    if HadStartupRegistration then
      RegWriteStringValue(HKCU, StartupKey, StartupValue,
        '"' + ExpandConstant('{app}\{#AppExeName}') + '"');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveLegacyStartup;
end;
