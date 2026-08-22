#define AppName "DeskCanvas"
#define AppVersion "1.2.0-beta.3"
#define AppFileVersion "1.2.0.3"
#define AppExeName "DeskCanvas.exe"
#define PayloadPath "..\artifacts\beta\DeskCanvas-1.2.0-beta.3-win-x64\DeskCanvas.exe"

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
CloseApplicationsFilter=DeskCanvas*.exe
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
Name: "startup"; Description: "Windowsへのサインイン時にDeskCanvasを起動する"; Flags: checkedonce

[InstallDelete]
; Program cleanup stays inside the confirmed install location. User layouts,
; media and backups in %LOCALAPPDATA%\DeskCanvas are not installer payload.
Type: files; Name: "{app}\DeskCanvas.exe"
Type: files; Name: "{app}\DeskCanvas-*.exe"
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
  StartupRunOnceKey = 'Software\Microsoft\Windows\CurrentVersion\RunOnce';
  StartupApprovedRunKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run';
  StartupApprovedRunOnceKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\RunOnce';
  StartupValue = 'DeskCanvas';

var
  HadStartupRegistration: Boolean;
  HadLegacyPayload: Boolean;

function IsDeskCanvasCommand(const Command: String): Boolean;
begin
  Result := Pos('deskcanvas.exe', Lowercase(Command)) > 0;
end;

function HasDeskCanvasRunValue(const Subkey: String): Boolean;
var
  ValueNames: TArrayOfString;
  Index: Integer;
  Command: String;
begin
  Result := RegValueExists(HKCU, Subkey, StartupValue);
  if Result then
    exit;
  if not RegGetValueNames(HKCU, Subkey, ValueNames) then
    exit;
  for Index := 0 to GetArrayLength(ValueNames) - 1 do
  begin
    if RegQueryStringValue(HKCU, Subkey, ValueNames[Index], Command) and
       IsDeskCanvasCommand(Command) then
    begin
      Result := True;
      exit;
    end;
  end;
end;

function HasDeskCanvasStartupShortcut(const Pattern: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant(Pattern), FindRec);
  if Result then
    FindClose(FindRec);
end;

function HasLegacyDeskCanvasPayload: Boolean;
begin
  Result := HasDeskCanvasStartupShortcut('{app}\DeskCanvas*.exe');
end;

procedure RemoveDeskCanvasRunValues(const Subkey, StartupApprovedSubkey: String);
var
  ValueNames: TArrayOfString;
  Index: Integer;
  Command: String;
begin
  if not RegGetValueNames(HKCU, Subkey, ValueNames) then
    exit;
  for Index := 0 to GetArrayLength(ValueNames) - 1 do
  begin
    if (CompareText(ValueNames[Index], StartupValue) = 0) or
       (RegQueryStringValue(HKCU, Subkey, ValueNames[Index], Command) and
        IsDeskCanvasCommand(Command)) then
    begin
      RegDeleteValue(HKCU, Subkey, ValueNames[Index]);
      RegDeleteValue(HKCU, StartupApprovedSubkey, ValueNames[Index]);
    end;
  end;
end;

procedure RemoveDeskCanvasStartupShortcuts;
var
  FindRec: TFindRec;
  ShortcutPath: String;
begin
  ShortcutPath := ExpandConstant('{userstartup}\DeskCanvas*.lnk');
  if FindFirst(ShortcutPath, FindRec) then
  begin
    try
      repeat
        DeleteFile(ExpandConstant('{userstartup}\' + FindRec.Name));
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
end;

procedure RemoveLegacyStartup;
begin
  RemoveDeskCanvasRunValues(StartupKey, StartupApprovedRunKey);
  RemoveDeskCanvasRunValues(StartupRunOnceKey, StartupApprovedRunOnceKey);
  RemoveDeskCanvasStartupShortcuts;
end;

procedure InitializeWizard;
begin
  HadStartupRegistration := HasDeskCanvasRunValue(StartupKey) or
    HasDeskCanvasRunValue(StartupRunOnceKey) or
    HasDeskCanvasStartupShortcut('{userstartup}\DeskCanvas*.lnk');
  HadLegacyPayload := HasLegacyDeskCanvasPayload;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then
    RemoveLegacyStartup;

  if CurStep = ssPostInstall then
  begin
    RemoveLegacyStartup;
    if HadStartupRegistration or HadLegacyPayload or WizardIsTaskSelected('startup') then
      RegWriteStringValue(HKCU, StartupKey, StartupValue,
        '"' + ExpandConstant('{app}\{#AppExeName}') + '"');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveLegacyStartup;
end;
