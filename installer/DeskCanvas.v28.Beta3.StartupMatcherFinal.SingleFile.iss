#define AppName "DeskCanvas"
#define AppVersion "1.2.0-beta.3"
#define AppFileVersion "1.2.0.3"
#define AppExeName "DeskCanvas.exe"
#define PayloadPath "..\artifacts\beta\DeskCanvas-1.2.0-beta.3-win-x64\DeskCanvas.exe"

[Setup]
AppId={{960134F4-96AD-4F8E-87C2-7AA30CE18E27}
AppName={#AppName}
AppMutex=Local\DeskCanvas.SingleInstance
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
Type: files; Name: "{app}\DeskCanvas.exe"
Type: files; Name: "{app}\DeskCanvas-*.exe"
Type: files; Name: "{app}\*.dll"
Type: files; Name: "{app}\*.pdb"
Type: files; Name: "{app}\*.json"
Type: filesandordirs; Name: "{app}\runtimes"
Type: filesandordirs; Name: "{app}\cs"
Type: filesandordirs; Name: "{app}\de"
Type: filesandordirs; Name: "{app}\en"
Type: filesandordirs; Name: "{app}\es"
Type: filesandordirs; Name: "{app}\fr"
Type: filesandordirs; Name: "{app}\it"
Type: filesandordirs; Name: "{app}\ja"
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

function IsVersionToken(const Text: String): Boolean;
var Index: Integer; Character: String;
begin
  Result := Length(Text) > 0;
  for Index := 1 to Length(Text) do
  begin
    Character := Copy(Text, Index, 1);
    if (Character = '\') or (Character = '/') or (Character = ' ') or
       (Character = '"') or (Character = '''') or (Character = #9) then
    begin Result := False; exit; end;
  end;
end;

function IsDeskCanvasCommand(const Command: String): Boolean;
var LowerCommand, VersionToken: String; SearchFrom, Candidate, VersionStart, ExeOffset, ExeEnd: Integer;
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
        if ExeOffset > 1 then
        begin
          VersionToken := Copy(LowerCommand, VersionStart, ExeOffset - 1);
          ExeEnd := VersionStart + ExeOffset + 3;
          if IsVersionToken(VersionToken) and IsCommandEnd(LowerCommand, ExeEnd) then begin Result := True; exit; end;
        end;
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

procedure RemoveDeskCanvasStartupShortcuts;
var FindRec: TFindRec;
begin if FindFirst(ExpandConstant('{userstartup}\DeskCanvas*.lnk'), FindRec) then begin try repeat DeleteFile(ExpandConstant('{userstartup}\' + FindRec.Name)); until not FindNext(FindRec); finally FindClose(FindRec); end; end; end;

procedure RemoveLegacyStartup;
begin RemoveDeskCanvasRunValues(StartupKey, StartupApprovedRunKey); RemoveDeskCanvasRunValues(StartupRunOnceKey, StartupApprovedRunOnceKey); RemoveDeskCanvasStartupShortcuts; end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssInstall then begin HadStartupRegistration := HasDeskCanvasRunValue(StartupKey) or HasDeskCanvasRunValue(StartupRunOnceKey) or HasPathMatch('{userstartup}\DeskCanvas*.lnk'); HadLegacyPayload := HasPathMatch('{app}\DeskCanvas*.exe'); RemoveLegacyStartup; end;
  if CurStep = ssPostInstall then begin RemoveLegacyStartup; if HadStartupRegistration or HadLegacyPayload or WizardIsTaskSelected('startup') then RegWriteStringValue(HKCU, StartupKey, StartupValue, '"' + ExpandConstant('{app}\{#AppExeName}') + '"'); end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin if CurUninstallStep = usUninstall then RemoveLegacyStartup; end;
