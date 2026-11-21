; 版本号：CI 传 /DAppVersion=x.y.z；本地构建默认值
#ifndef AppVersion
  #define AppVersion "0.5.0"
#endif

[Setup]
AppName=Vael
AppVersion={#AppVersion}
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
OutputBaseFilename=Vael_Setup_v{#AppVersion}
SetupIconFile=Assets\app.ico
ArchitecturesInstallIn64BitMode=x64
; ★ 主程序单实例互斥体：升级时等旧进程真正退出再替换文件
AppMutex=Vael_SingleInstance_Mutex

[Languages]
; 注：CI 的 choco Inno 不带中文语言文件，用自带 Default.isl 保证可编译
; （静默升级 /VERYSILENT 下语言界面用户不可见；自定义中文字符串仍生效）
Name: "english"; MessagesFile: "compiler:Default.isl"

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

[Code]
function GetRelaunchArgs(): String;
var
  ArgFile: String;
  S: AnsiString;
begin
  Result := '';
  ArgFile := ExpandConstant('{userappdata}\SchoolBusytools\relaunch_args.txt');
  if FileExists(ArgFile) then
  begin
    if LoadStringFromFile(ArgFile, S) then
      Result := Trim(String(S));
    DeleteFile(ArgFile);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var
  ResultCode: Integer;
  RelaunchArgs: String;
begin
  // ★ 更新器流程：静默升级（/SILENT 进度条 或 /VERYSILENT 全后台）安装完成后自动重启程序，
  //   并沿用原启动模式（开机静默启动的实例更新后仍静默，不弹主窗口）
  if (CurStep = ssPostInstall) and WizardSilent() then
  begin
    RelaunchArgs := GetRelaunchArgs();
    Exec(ExpandConstant('{app}\SBtools.exe'), RelaunchArgs, '', SW_SHOW, ewNoWait, ResultCode);
  end;
end;

[UninstallDelete]
Type: filesandordirs; Name: "{app}"