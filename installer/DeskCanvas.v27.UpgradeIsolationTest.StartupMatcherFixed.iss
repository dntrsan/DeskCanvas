; Isolation-only test. Registry is restricted to HKCU\Software\DeskCanvas\InstallerIsolationV27.
#define AppName "DeskCanvas Upgrade Isolation Test"
#define AppVersion "1.2.0-beta.3"
#define AppExeName "DeskCanvas.exe"
#define PayloadPath "..\artifacts\beta\DeskCanvas-1.2.0-beta.3-win-x64\DeskCanvas.exe"

[Setup]
AppId={{FCB5E335-61A1-45D4-89A5-26BD3D39E6DB}
AppName={#AppName}
AppVersion={#AppVersion}
DefaultDirName={tmp}\DeskCanvas-upgrade-isolation-default
DisableDirPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\artifacts\installer-tests
OutputBaseFilename=DeskCanvas-UpgradeIsolationStartupMatcherFixed-{#AppVersion}
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
Type: files; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\legacy"

[Files]
Source: "{#PayloadPath}"; DestDir: "{app}"; DestName: "{#AppExeName}"; Flags: ignoreversion

[Code]
const
  StartupKey = 'Software\DeskCanvas\InstallerIsolationV27\Run';
  StartupRunOnceKey = 'Software\DeskCanvas\InstallerIsolationV27\RunOnce';
  StartupApprovedRunKey = 'Software\DeskCanvas\InstallerIsolationV27\StartupApproved\Run';
  StartupApprovedRunOnceKey = 'Software\DeskCanvas\InstallerIsolationV27\StartupApproved\RunOnce';
  StartupValue = 'DeskCanvas.UpgradeIsolationTest';
  MockStartupPattern = '{app}\legacy-startup\DeskCanvas*.lnk';

var HadStartupRegistration: Boolean; HadLegacyPayload: Boolean;

function IsCommandStart(const Text: String; const Index: Integer): Boolean;
var Previous: String;
begin
  if Index = 1 then begin Result := True; exit; end;
  Previous := Copy(Text, Index - 1, 1);
  Result := (Previous = '\') or (Previous = '/') or (Previous = '"') or (Previous = '''');
end;

function IsCommandEnd(const Text: String; const Index: Integer): Boolean;
var Following: String;
begin
  if Index > Length(Text) then begin Result := True; exit; end;
  Following := Copy(Text, Index, 1);
  Result := (Following = ' ') or (Following = '"') or (Following = '''') or (Following = #9);
end;

function IsDeskCanvasCommand(const Command: String): Boolean;
var LowerCommand: String; SearchFrom, Candidate, VersionStart, ExeOffset, ExeEnd: Integer;
begin
  Result := False; LowerCommand := Lowercase(Command); SearchFrom := 1;
  while SearchFrom <= Length(LowerCommand) do
  begin
    Candidate := Pos('deskcanvas', Copy(LowerCommand, SearchFrom, MaxInt));
    if Candidate = 0 then exit;
    Candidate := Candidate + SearchFrom - 1;
    if IsCommandStart(LowerCommand, Candidate) then
    begin
      if Copy(LowerCommand, Candidate, 14) = 'deskcanvas.exe' then
      begin if IsCommandEnd(LowerCommand, Candidate + 14) then begin Result := True; exit; end; end
      else if Copy(LowerCommand, Candidate, 11) = 'deskcanvas-' then
      begin
        VersionStart := Candidate + 11; ExeOffset := Pos('.exe', Copy(LowerCommand, VersionStart, MaxInt));
        if ExeOffset > 1 then begin ExeEnd := VersionStart + ExeOffset + 3; if IsCommandEnd(LowerCommand, ExeEnd) then begin Result := True; exit; end; end;
      end;
    end;
    SearchFrom := Candidate + 10;
  end;
end;

function HasDeskCanvasRunValue(const Subkey: String): Boolean;
var Names: TArrayOfString; Index: Integer; Command: String;
begin
  Result := RegValueExists(HKCU, Subkey, StartupValue); if Result then exit;
  if not RegGetValueNames(HKCU, Subkey, Names) then exit;
  for Index := 0 to GetArrayLength(Names) - 1 do if RegQueryStringValue(HKCU, Subkey, Names[Index], Command) and IsDeskCanvasCommand(Command) then begin Result := True; exit; end;
end;

function HasPathMatch(const Pattern: String): Boolean;
var FindRec: TFindRec;
begin Result := FindFirst(ExpandConstant(Pattern), FindRec); if Result then FindClose(FindRec); end;

procedure RemoveDeskCanvasRunValues(const Subkey, ApprovedSubkey: String);
var Names: TArrayOfString; Index: Integer; Command: String;
begin
  if not RegGetValueNames(HKCU, Subkey, Names) then exit;
  for Index := 0 to GetArrayLength(Names) - 1 do if (CompareText(Names[Index], StartupValue) = 0) or (RegQueryStringValue(HKCU, Subkey, Names[Index], Command) and IsDeskCanvasCommand(Command)) then begin RegDeleteValue(HKCU, Subkey, Names[Index]); RegDeleteValue(HKCU, ApprovedSubkey, Names[Index]); end;
end;

procedure RemoveMockStartupShortcuts;
var FindRec: TFindRec;
begin if FindFirst(ExpandConstant(MockStartupPattern), FindRec) then begin try repeat DeleteFile(ExpandConstant('{app}\legacy-startup\' + FindRec.Name)); until not FindNext(FindRec); finally FindClose(FindRec); end; end; end;

procedure RemoveLegacyStartup;
begin RemoveDeskCanvasRunValues(StartupKey, StartupApprovedRunKey); RemoveDeskCanvasRunValues(StartupRunOnceKey, StartupApprovedRunOnceKey); RemoveMockStartupShortcuts; end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin HadStartupRegistration := HasDeskCanvasRunValue(StartupKey) or HasDeskCanvasRunValue(StartupRunOnceKey) or HasPathMatch(MockStartupPattern); HadLegacyPayload := HasPathMatch('{app}\DeskCanvas*.exe'); RemoveLegacyStartup; end;
  if CurStep = ssPostInstall then begin RemoveLegacyStartup; if HadStartupRegistration or HadLegacyPayload then RegWriteStringValue(HKCU, StartupKey, StartupValue, '"' + ExpandConstant('{app}\{#AppExeName}') + '"'); end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin if CurUninstallStep = usUninstall then RemoveLegacyStartup; end;
