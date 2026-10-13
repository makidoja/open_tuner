#define MyAppName "OpenTuner"
#define MyAppDisplayName "Open Tuner (ZR6TG)"
#define MyAppVersion "M0CKE 0.C.21026"
#define MyAppPublisher "ZR6TG / M0CKE"
#define MyAppExeName "opentuner.exe"

[Setup]
AppId={{D97138B4-F2BA-584D-827A-A40489C26C66}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppDisplayName} Version {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL=https://github.com/makidoja/open_tuner
AppSupportURL=https://github.com/makidoja/open_tuner/issues
AppUpdatesURL=https://github.com/makidoja/open_tuner
DefaultDirName={localappdata}\Programs\OpenTuner
DefaultGroupName=OpenTuner
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\installer-output
OutputBaseFilename=OpenTuner-M0CKE-0.C.21026-Setup
SetupIconFile=..\tv.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
LicenseFile=..\LICENSE
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
ChangesAssociations=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked

[Files]
Source: "..\bin\Release\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\OpenTuner"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"
Name: "{group}\Uninstall OpenTuner"; Filename: "{uninstallexe}"
Name: "{autodesktop}\OpenTuner"; Filename: "{app}\{#MyAppExeName}"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch OpenTuner"; Flags: nowait postinstall skipifsilent
