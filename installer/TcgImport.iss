; Inno Setup script for TCG Import. Build it with build-installer.ps1, which publishes the app first
; and passes AppVersion, PublishDir and RepoUrl.
;
; The wizard is kept short: install folder → desktop shortcut choice → Install → Finish (launch).
; It installs per user (no admin prompt), by default into %LOCALAPPDATA%\Programs\TCG Import.
; Upgrades reuse the earlier folder and skip the folder page.

#ifndef AppVersion
  #define AppVersion "0.0.0"
#endif
#ifndef PublishDir
  #define PublishDir "..\artifacts\publish"
#endif
#ifndef RepoUrl
  #define RepoUrl "https://github.com/Svenning123/tcg-import"
#endif

#define AppName "TCG Import"
#define AppExe "TcgImport.exe"

[Setup]
; Keep AppId the same across versions so installing a new version upgrades the old one.
AppId={{6F0B8E5C-3E7A-4C55-9E86-2B7A4C1D9F31}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=TCG Import
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
PrivilegesRequired=lowest
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableWelcomePage=yes
DisableDirPage=auto
DisableProgramGroupPage=yes
DisableReadyPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
WizardStyle=modern
SetupIconFile=..\src\TcgImport.App\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
UninstallDisplayName={#AppName}
; Closes a running copy of the app before upgrading it.
CloseApplications=yes
OutputDir=..\artifacts
OutputBaseFilename=TcgImport-Setup-{#AppVersion}
Compression=lzma2/max
SolidCompression=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall skipifsilent
