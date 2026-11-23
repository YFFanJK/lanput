#ifndef AppVersion
  #define AppVersion "0.9.5"
#endif
#ifndef SourceDir
  #define SourceDir "..\dist\LANPUT-win-x64"
#endif

[Setup]
AppId={{CF92C636-8281-482D-B06C-966B5FE72AD9}
AppName=LANPUT
AppVersion={#AppVersion}
AppPublisher=LANPUT
DefaultDirName={localappdata}\Programs\LANPUT
DefaultGroupName=LANPUT
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
MinVersion=10.0
OutputDir=..\dist
OutputBaseFilename=LANPUT-{#AppVersion}-Setup-x64
Compression=lzma2/fast
SolidCompression=yes
WizardStyle=modern
SetupIconFile=..\src\LANPUT.Windows\Assets\LANPUT.ico
WizardImageFile=..\src\LANPUT.Windows\Assets\installer-banner.bmp
WizardSmallImageFile=..\src\LANPUT.Windows\Assets\installer-small.bmp
WizardImageStretch=no
UninstallDisplayIcon={app}\LANPUT.exe
AppMutex=Local\LANPUT.Instance
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
VersionInfoVersion={#AppVersion}.0

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\LANPUT"; Filename: "{app}\LANPUT.exe"; WorkingDir: "{app}"
Name: "{autodesktop}\LANPUT"; Filename: "{app}\LANPUT.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\LANPUT.exe"; Description: "启动 LANPUT"; Flags: nowait postinstall skipifsilent
