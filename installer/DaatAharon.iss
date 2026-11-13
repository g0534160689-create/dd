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
