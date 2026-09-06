#define MyAppVersion GetEnv("APP_VERSION")

[Setup]
AppId={{B7D6E3A1-0A6A-4CF6-9FE2-5E3A9D8F4C12}
AppName=Sortography
AppVersion={#MyAppVersion}
AppPublisher=Sortography
DefaultDirName={localappdata}\Programs\Sortography
DefaultGroupName=Sortography
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
LicenseFile=..\LICENSE
CloseApplications=yes
OutputDir=..\packages
OutputBaseFilename=Sortography-{#MyAppVersion}-Setup
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\Sortography.exe
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Sortography"; Filename: "{app}\Sortography.exe"
Name: "{autodesktop}\Sortography"; Filename: "{app}\Sortography.exe"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Run]
Filename: "{app}\Sortography.exe"; Description: "Open Sortography"; Flags: nowait postinstall skipifsilent
