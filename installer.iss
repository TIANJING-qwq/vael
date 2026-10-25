[Setup]
AppName=Vael
AppVersion=0.5.0
AppPublisher=Vael
DefaultDirName={autopf}\Vael
DefaultGroupName=Vael
UninstallDisplayIcon={app}\SBtools.exe
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
LicenseFile=LICENSE.txt
PrivilegesRequired=lowest
OutputDir=.\publish\installer
OutputBaseFilename=Vael_Setup_v0.5.0
SetupIconFile=Assets\app.ico
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "chinesesimplified"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加图标:"

[Files]
Source: "publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "LICENSE.txt"; DestDir: "{app}"; Flags: ignoreversion
Source: "THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\Vael"; Filename: "{app}\SBtools.exe"
Name: "{group}\卸载Vael"; Filename: "{uninstallexe}"
Name: "{autodesktop}\Vael"; Filename: "{app}\SBtools.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SBtools.exe"; Description: "立即启动"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"