; Compile through tools/package-native.ps1 so every payload is allowlisted first.
#ifndef PayloadDir
  #error PayloadDir is required
#endif
#ifndef PackageOutput
  #error PackageOutput is required
#endif
#ifndef PackageVersion
  #define PackageVersion "0.22.4"
#endif
#ifndef PackageBaseName
  #define PackageBaseName "Dicebound-0.22.4-beta.1-Windows-x64-Setup"
#endif

[Setup]
AppId={{36827C07-2C10-4292-81A3-BB472DA782BA}
AppName=Dicebound · 烬海天阙
AppVersion={#PackageVersion} Beta 1
AppVerName=Dicebound · 烬海天阙 {#PackageVersion} Beta 1
AppPublisher=Dicebound contributors
AppPublisherURL=https://github.com/chenzhiyong1994/cindersea
AppSupportURL=https://github.com/chenzhiyong1994/cindersea/issues
AppUpdatesURL=https://github.com/chenzhiyong1994/cindersea/releases
DefaultDirName={localappdata}\Programs\Dicebound
DefaultGroupName=Dicebound
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
OutputDir={#PackageOutput}
OutputBaseFilename={#PackageBaseName}
Compression=lzma2/normal
LZMADictionarySize=32768
LZMANumBlockThreads=2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\Dicebound.exe
CloseApplications=no
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#PackageVersion}.0
VersionInfoDescription=Dicebound Beta 1 Windows installer
InfoBeforeFile={#PayloadDir}\START_HERE.txt

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut / 创建桌面快捷方式"; GroupDescription: "Shortcuts / 快捷方式"; Flags: unchecked

[Files]
Source: "{#PayloadDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Dicebound"; Filename: "{app}\Dicebound.exe"; WorkingDir: "{app}"
Name: "{group}\Credits and licenses"; Filename: "{app}\Credits"
Name: "{autodesktop}\Dicebound"; Filename: "{app}\Dicebound.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\Dicebound.exe"; WorkingDir: "{app}"; Description: "Launch Dicebound / 启动游戏"; Flags: nowait postinstall skipifsilent unchecked

; Deliberately no UninstallDelete entries: only installer-owned files are removed.
; Unity's LocalLow player saves are outside {app} and are never touched.
