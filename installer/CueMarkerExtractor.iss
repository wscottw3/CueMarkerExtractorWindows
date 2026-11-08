; Inno Setup script for Cue Marker Extractor.
; Built by the GitHub Actions release workflow (.github/workflows/release.yml):
;   iscc /DAppVersion=1.2.3 installer\CueMarkerExtractor.iss
; The published exe must already exist at publish\win-x64\CueMarkerExtractor.exe.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif

#define AppName "Cue Marker Extractor"
#define AppExeName "CueMarkerExtractor.exe"

[Setup]
; Never change AppId between releases — it's how upgrades find the old install.
AppId={{0F53A319-1BD2-42F5-8012-9AC8CB2D8F37}
AppName={#AppName}
AppVersion={#AppVersion}
VersionInfoVersion={#AppVersion}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
UninstallDisplayIcon={app}\{#AppExeName}
OutputDir=Output
OutputBaseFilename=CueMarkerExtractor-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
; Installs just for the current user by default (no admin prompt); the
; wizard offers "all users" as an option.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\publish\win-x64\{#AppExeName}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExeName}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExeName}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent
