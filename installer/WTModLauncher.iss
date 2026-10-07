; Inno Setup 6 script for WT Mod Launcher: per-user install, no admin rights needed.
;
; Build (from the repo root, after `dotnet publish src/WTModLauncher -c Release -o publish`):
;   iscc installer\WTModLauncher.iss /DAppVersion=1.0.0
; Output: publish\WTModLauncher-Setup-0.2.0.exe
;
; The launcher updates itself by running this setup with /SILENT /SUPPRESSMSGBOXES /NORESTART /RELAUNCH=1.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
; Windows file versions are numeric only: "0.3.0" for a "0.3.0-beta.1" tag.
#ifndef NumericVersion
  #define NumericVersion AppVersion
#endif
#ifndef SourceExe
  #define SourceExe "..\publish\WTModLauncher.exe"
#endif
#ifndef OutputDir
  #define OutputDir "..\publish"
#endif

#define AppName "WT Mod Launcher"
#define AppExe "WTModLauncher.exe"
#define RepoUrl "https://github.com/Robocnop/WarThunderModLauncher"

[Setup]
; Never change AppId: it is how Windows and later setups recognise an existing install.
AppId={{CAF350CC-B658-44C1-B674-96B8ECC9722E}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=Robocnop
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
VersionInfoVersion={#NumericVersion}
DefaultDirName={localappdata}\Programs\WTModLauncher
DisableDirPage=auto
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#OutputDir}
OutputBaseFilename=WTModLauncher-Setup-{#AppVersion}
SetupIconFile=..\src\WTModLauncher\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
; Closes a running launcher (e.g. during a self-update) instead of failing on a locked exe.
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"
Name: "fr"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#SourceExe}"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
; Self-update: start the new version once the silent setup is done.
Filename: "{app}\{#AppExe}"; Flags: nowait; Check: IsRelaunch

; Mods stay installed in the game and the launcher's data (%LOCALAPPDATA%\WTModLauncher) is kept on uninstall:
; reinstalling the launcher picks everything up again.

[Code]
function IsRelaunch: Boolean;
begin
  Result := ExpandConstant('{param:RELAUNCH|0}') = '1';
end;
