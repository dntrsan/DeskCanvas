
#define AppName "DeskCanvas"
#define AppVersion "1.2.0-beta.1"
#define AppFileVersion "1.2.0.1"
#define AppExeName "DeskCanvas.exe"

[Setup]
AppId={{960134F4-96AD-4F8E-87C2-7AA30CE18E27}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=DeskCanvas
DefaultDirName={localappdata}\Programs\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.17763
OutputDir=..\artifacts\installer
OutputBaseFilename=DeskCanvas-Setup-{#AppVersion}
SetupIconFile=..\src\DeskCanvas.App\Assets\AppIcon.ico
UninstallDisplayIcon={app}\{#AppExeName}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
CloseApplicationsFilter={#AppExeName}
RestartApplications=no
UsePreviousAppDir=yes
UsePreviousTasks=yes
VersionInfoVersion={#AppFileVersion}
VersionInfoProductName={#AppName}
VersionInfoDescription=DeskCanvas installer
VersionInfoCompany=DeskCanvas

[Languages]
Name: "japanese"; MessagesFile: "compiler:Languages\Japanese.isl"

[Tasks]
Name: "startup"; Description: "Windowsへのサインイン時にDeskCanvasを起動する"; Flags: unchecked

[InstallDelete]
; Cleanup is confined to the known in-place {app}. Inno's unins*.exe/dat and
; %LOCALAPPDATA%\DeskCanvas user data are intentionally outside every pattern.
Type: files; Name: "{app}\DeskCanvas.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.pdb"
Type: files; Name: "{app}\DeskCanvas.runtimeconfig.json"
Type: files; Name: "{app}\DeskCanvas.deps.json"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\ja"
Type: filesandordirs; Name: "{app}\ja-JP"
Type: filesandordirs; Name: "{app}\en"
Type: filesandordirs; Name: "{app}\en-US"
Type: filesandordirs; Name: "{app}\obsolete"
Type: filesandordirs; Name: "{app}\legacy"
Type: filesandordirs; Name: "{app}\plugins"

[Files]
Source: "..\artifacts\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\DeskCanvas"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "DeskCanvasを起動する"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: dirifempty; Name: "{app}\ja"
Type: dirifempty; Name: "{app}"

[Code]
const
  StartupKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupValue = 'DeskCanvas';

var
  HadStartupRegistration: Boolean;

procedure InitializeWizard;
begin
  { Capture the runtime setting before ssInstall removes a stale executable path. }
  HadStartupRegistration := RegValueExists(HKCU, StartupKey, StartupValue);
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    RegDeleteValue(HKCU, StartupKey, StartupValue);

  if CurStep = ssPostInstall then
  begin
    { Preserve an enabled prior registration. A newly selected task can enable it too. }
    if HadStartupRegistration or WizardIsTaskSelected('startup') then
      RegWriteStringValue(HKCU, StartupKey, StartupValue,
        '"' + ExpandConstant('{app}\{#AppExeName}') + '"')
    else
      RegDeleteValue(HKCU, StartupKey, StartupValue);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RegDeleteValue(HKCU, StartupKey, StartupValue);
end;

#include "DeskCanvas.v20.UpgradeAccepted2.iss"

[InstallDelete]
; Satellite folders emitted by the current Windows publish payload.
Type: filesandordirs; Name: "{app}\cs"
Type: filesandordirs; Name: "{app}\de"
Type: filesandordirs; Name: "{app}\es"
Type: filesandordirs; Name: "{app}\fr"
Type: filesandordirs; Name: "{app}\it"
Type: filesandordirs; Name: "{app}\ko"
Type: filesandordirs; Name: "{app}\pl"
Type: filesandordirs; Name: "{app}\pt-BR"
Type: filesandordirs; Name: "{app}\ru"
Type: filesandordirs; Name: "{app}\tr"
Type: filesandordirs; Name: "{app}\zh-Hans"
Type: filesandordirs; Name: "{app}\zh-Hant"
