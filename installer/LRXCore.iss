#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif

[Setup]
AppId={{8F4C2B7E-6D31-4E5B-9A1C-4E7D8B2A91F3}
AppName=LRX Core
AppVersion={#MyAppVersion}
AppVerName=LRX Core v{#MyAppVersion}
AppPublisher=Mericsdc
AppPublisherURL=https://github.com/Mericsdc/LRXCore
AppSupportURL=https://github.com/Mericsdc/LRXCore/issues
AppUpdatesURL=https://github.com/Mericsdc/LRXCore/releases
DefaultDirName={autopf}\LRX Core
DefaultGroupName=LRX Core
DisableProgramGroupPage=yes
PrivilegesRequired=admin
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
OutputDir=..\dist
OutputBaseFilename=LRXCore-Setup-{#MyAppVersion}
SetupIconFile=..\Assets\app_icon.ico
UninstallDisplayIcon={app}\LRXCore.exe
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern
ChangesAssociations=yes
AppMutex=Local\LRXCore_SingleInstance_Mutex_v1
CloseApplications=force
CloseApplicationsFilter=*.exe

[Languages]
Name: "turkish"; MessagesFile: "compiler:Languages\Turkish.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LRX Core"; Filename: "{app}\LRXCore.exe"; IconFilename: "{app}\LRXCore.exe"
Name: "{group}\LRX Core Kaldır"; Filename: "{uninstallexe}"
Name: "{autodesktop}\LRX Core"; Filename: "{app}\LRXCore.exe"; IconFilename: "{app}\LRXCore.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\LRXCore.exe"; Description: "{cm:LaunchProgram,LRX Core}"; Flags: nowait postinstall skipifsilent
Filename: "{app}\LRXCore.exe"; Flags: nowait skipifnotsilent
