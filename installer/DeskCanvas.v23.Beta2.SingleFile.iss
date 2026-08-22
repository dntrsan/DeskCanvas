#define AppName "DeskCanvas"
#define AppVersion "1.2.0-beta.2"
#define AppFileVersion "1.2.0.2"
#define AppExeName "DeskCanvas.exe"
#define PayloadPath "..\artifacts\beta\DeskCanvas-1.2.0-beta.2-win-x64\DeskCanvas.exe"

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
; Only the known program payload in {app} is removed. User layouts, media and
; backups live under %LOCALAPPDATA%\DeskCanvas and are deliberately untouched.
Type: files; Name: "{app}\DeskCanvas.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.pdb"
Type: files; Name: "{app}\DeskCanvas.runtimeconfig.json"
Type: files; Name: "{app}\DeskCanvas.deps.json"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\cs"
Type: filesandordirs; Name: "{app}\de"
Type: filesandordirs; Name: "{app}\en"
Type: filesandordirs; Name: "{app}\en-US"
Type: filesandordirs; Name: "{app}\es"
Type: filesandordirs; Name: "{app}\fr"
Type: filesandordirs; Name: "{app}\it"
Type: filesandordirs; Name: "{app}\ja"
Type: filesandordirs; Name: "{app}\ja-JP"
Type: filesandordirs; Name: "{app}\ko"
Type: filesandordirs; Name: "{app}\pl"
Type: filesandordirs; Name: "{app}\pt-BR"
Type: filesandordirs; Name: "{app}\ru"
Type: filesandordirs; Name: "{app}\tr"
Type: filesandordirs; Name: "{app}\zh-Hans"
Type: filesandordirs; Name: "{app}\zh-Hant"
Type: filesandordirs; Name: "{app}\obsolete"
Type: filesandordirs; Name: "{app}\legacy"
Type: filesandordirs; Name: "{app}\plugins"

[Files]
Source: "{#PayloadPath}"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\DeskCanvas"; Filename: "{app}\{#AppExeName}"; WorkingDir: "{app}"

[Run]
Filename: "{app}\{#AppExeName}"; Description: "DeskCanvasを起動する"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: dirifempty; Name: "{app}"

[Code]
const
  StartupKey = 'Software\Microsoft\Windows\CurrentVersion\Run';
  StartupValue = 'DeskCanvas';
  LegacyStartupShortcut = '{userstartup}\DeskCanvas.lnk';

var
  HadStartupRegistration: Boolean;

procedure InitializeWizard;
begin
  { Preserve enabled startup regardless of whether the older build used Run or
    a startup shortcut. Both legacy forms are collapsed to one Run value. }
  HadStartupRegistration := RegValueExists(HKCU, StartupKey, StartupValue) or
    FileExists(ExpandConstant(LegacyStartupShortcut));
end;

procedure RemoveLegacyStartup;
begin
  RegDeleteValue(HKCU, StartupKey, StartupValue);
  DeleteFile(ExpandConstant(LegacyStartupShortcut));
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    RemoveLegacyStartup;

  if CurStep = ssPostInstall then
  begin
    RemoveLegacyStartup;
    if HadStartupRegistration or WizardIsTaskSelected('startup') then
      RegWriteStringValue(HKCU, StartupKey, StartupValue,
        '"' + ExpandConstant('{app}\{#AppExeName}') + '"');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveLegacyStartup;
end;
