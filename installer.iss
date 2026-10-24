[Setup]
AppName=Vael
AppVersion=0.4.8
AppPublisher=Vael
DefaultDirName={autopf}\Vael
DefaultGroupName=Vael
UninstallDisplayIcon={app}\SBtools.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
OutputDir=.\publish\installer
OutputBaseFilename=Vael_Setup_v0.4.8
SetupIconFile=Assets\app.ico
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "鍒涘缓妗岄潰蹇嵎鏂瑰紡"; GroupDescription: "闄勫姞鍥炬爣:"

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\Vael"; Filename: "{app}\SBtools.exe"
Name: "{group}\鍗歌浇Vael"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Vael"; Filename: "{app}\SBtools.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SBtools.exe"; Description: "绔嬪嵆鍚姩"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"