#define MyAppName "OpenTuner"
#define MyAppDisplayName "Open Tuner (ZR6TG)"
#define MyAppVersion "1.0.1"
#define MyAppDisplayVersion "M0CKE 0.C.v1.0.1"
#define MyAppPublisher "ZR6TG / M0CKE"
#define MyAppExeName "opentuner.exe"

[Setup]
AppId={{D97138B4-F2BA-584D-827A-A40489C26C66}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppDisplayName} Version {#MyAppDisplayVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/makidoja/open_tuner
AppSupportURL=https://github.com/makidoja/open_tuner/issues
AppUpdatesURL=https://github.com/makidoja/open_tuner
DefaultDirName={localappdata}\Programs\OpenTuner
DefaultGroupName=Open Tuner (ZR6TG)
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\installer-output
OutputBaseFilename=OpenTuner-M0CKE-0.C.v1.0.1-Setup
SetupIconFile=..\OpenTunerModern.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
UninstallDisplayName={#MyAppDisplayName} Version {#MyAppDisplayVersion}
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no
VersionInfoVersion=1.0.1.0
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppDisplayName} Version {#MyAppDisplayVersion}
VersionInfoProductName={#MyAppDisplayName}
VersionInfoProductVersion=1.0.1.0

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\OpenTunerModern.ico"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Open Tuner (ZR6TG)"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\OpenTunerModern.ico"
Name: "{group}\Uninstall OpenTuner"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Open Tuner (ZR6TG)"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; IconFilename: "{app}\OpenTunerModern.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OpenTuner"; Flags: nowait postinstall skipifsilent
