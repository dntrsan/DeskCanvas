; Isolation-only upgrade test. Its AppId, registry values and shortcut mock are
; all distinct from the production DeskCanvas installation.
#define AppName "DeskCanvas Upgrade Isolation Test"
#define AppVersion "1.2.0-beta.3"
#define AppExeName "DeskCanvas.exe"
#define PayloadPath "..\artifacts\beta\DeskCanvas-1.2.0-beta.3-win-x64\DeskCanvas.exe"

[Setup]
AppId={{7D5B9DFB-E777-4FEB-A866-EA43A03EE9C2}
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
Type: files; Name: "{app}\DeskCanvas-*.exe"
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
  StartupRunOnceKey = 'Software\Microsoft\Windows\CurrentVersion\RunOnce';
  StartupApprovedRunKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run';
  StartupApprovedRunOnceKey = 'Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\RunOnce';
  StartupValue = 'DeskCanvas.UpgradeIsolationTest';
  MockStartupPattern = '{app}\legacy-startup\DeskCanvas*.lnk';

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
  if Result then exit;
  if not RegGetValueNames(HKCU, Subkey, ValueNames) then exit;
  for Index := 0 to GetArrayLength(ValueNames) - 1 do
    if RegQueryStringValue(HKCU, Subkey, ValueNames[Index], Command) and IsDeskCanvasCommand(Command) then
    begin
      Result := True;
      exit;
    end;
end;

function HasPathMatch(const Pattern: String): Boolean;
var
  FindRec: TFindRec;
begin
  Result := FindFirst(ExpandConstant(Pattern), FindRec);
  if Result then FindClose(FindRec);
end;

procedure RemoveDeskCanvasRunValues(const Subkey, StartupApprovedSubkey: String);
var
  ValueNames: TArrayOfString;
  Index: Integer;
  Command: String;
begin
  if not RegGetValueNames(HKCU, Subkey, ValueNames) then exit;
  for Index := 0 to GetArrayLength(ValueNames) - 1 do
    if (CompareText(ValueNames[Index], StartupValue) = 0) or
       (RegQueryStringValue(HKCU, Subkey, ValueNames[Index], Command) and IsDeskCanvasCommand(Command)) then
    begin
      RegDeleteValue(HKCU, Subkey, ValueNames[Index]);
      RegDeleteValue(HKCU, StartupApprovedSubkey, ValueNames[Index]);
    end;
end;

procedure RemoveMockStartupShortcuts;
var
  FindRec: TFindRec;
begin
  if FindFirst(ExpandConstant(MockStartupPattern), FindRec) then
  begin
    try
      repeat
        DeleteFile(ExpandConstant('{app}\legacy-startup\' + FindRec.Name));
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
  RemoveMockStartupShortcuts;
end;

procedure InitializeWizard;
begin
  HadStartupRegistration := HasDeskCanvasRunValue(StartupKey) or HasDeskCanvasRunValue(StartupRunOnceKey) or
    HasPathMatch(MockStartupPattern);
  HadLegacyPayload := HasPathMatch('{app}\DeskCanvas*.exe');
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then RemoveLegacyStartup;
  if CurStep = ssPostInstall then
  begin
    RemoveLegacyStartup;
    if HadStartupRegistration or HadLegacyPayload then
      RegWriteStringValue(HKCU, StartupKey, StartupValue, '"' + ExpandConstant('{app}\{#AppExeName}') + '"');
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then RemoveLegacyStartup;
end;
