#define AppName "דעת אהרן"
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif
#define AppPublisher "דעת אהרן"
#define AppExeName "DaatAharon.exe"

[Setup]
AppId={{7D7E5A21-65D2-4A9B-9E8B-58D6F0A5B411}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\דעת אהרן
DefaultGroupName={#AppName}
OutputDir=..\artifacts\installer
OutputBaseFilename=DaatAharon-Setup-win-x64
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=admin
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#AppExeName}

[Languages]
Name: "hebrew"; MessagesFile: "compiler:Languages\Hebrew.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "יצירת קיצור דרך בשולחן העבודה"; GroupDescription: "קיצורי דרך:"; Flags: unchecked

[Files]
Source: "..\artifacts\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "הפעלת דעת אהרן"; Flags: postinstall nowait skipifsilent

[Code]
function IsWebView2RuntimeInstalled: Boolean;
var
  RuntimeVersion: String;
begin
  Result := False;
  RuntimeVersion := '';
  if RegQueryStringValue(HKLM64,
       'SOFTWARE\\WOW6432Node\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
       'pv', RuntimeVersion) and (RuntimeVersion <> '') and (RuntimeVersion <> '0.0.0.0') then
  begin
    Result := True;
    Exit;
  end;
  RuntimeVersion := '';
  if RegQueryStringValue(HKCU,
       'Software\\Microsoft\\EdgeUpdate\\Clients\\{F3017226-FE2A-4295-8BDF-00C3A9A7E4C5}',
       'pv', RuntimeVersion) and (RuntimeVersion <> '') and (RuntimeVersion <> '0.0.0.0') then
    Result := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  RuntimeInstaller: String;
begin
  if (CurStep <> ssPostInstall) or IsWebView2RuntimeInstalled then
    Exit;
  RuntimeInstaller := ExpandConstant('{tmp}\\MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  ExtractTemporaryFile('MicrosoftEdgeWebView2RuntimeInstallerX64.exe');
  if not Exec(RuntimeInstaller, '/silent /install', '', SW_HIDE, ewWaitUntilTerminated, ResultCode) then
  begin
    MsgBox('התקנת רכיב WebView2 נכשלה. ההתקנה תיעצר.', mbCriticalError, MB_OK);
    Abort;
  end;
  if (ResultCode <> 0) and (ResultCode <> 3010) then
  begin
    MsgBox('התקנת רכיב WebView2 נכשלה (קוד ' + IntToStr(ResultCode) + ').', mbCriticalError, MB_OK);
    Abort;
  end;
end;
