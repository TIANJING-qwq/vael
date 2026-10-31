using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools.Services;

public enum RiskLevel
{
    Safe,
    Medium,
    Advanced
}

public class OptimizationItem
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public string Name { get; init; } = "";
    public string Description { get; init; } = "";
    public RiskLevel Risk { get; init; } = RiskLevel.Safe;

    /// <summary>内联应用脚本（优先于 switch 映射，供目录条目使用）。</summary>
    public string? Apply { get; init; }
    public string? Revert { get; init; }
    public string? Check { get; init; }
    /// <summary>是否需要重启生效。</summary>
    public bool Restart { get; init; }
    public bool HasRevert => Revert != null;
    public string RiskText => Risk switch
    {
        RiskLevel.Safe => "安全", RiskLevel.Medium => "中等", _ => "高级"
    };
    public Avalonia.Media.IBrush RiskBrush => Risk switch
    {
        RiskLevel.Safe => Avalonia.Media.Brush.Parse("#22AA22"),
        RiskLevel.Medium => Avalonia.Media.Brush.Parse("#FFAA00"),
        _ => Avalonia.Media.Brush.Parse("#FF5555")
    };
}

public static class SystemOptimizer
{
    public static List<OptimizationItem> AllItems { get; } = new()
    {
        // ============ 服务优化 ============
        new() { Id = "svc_diagtrack", Category = "服务优化",
                Name = "禁用遥测服务 (DiagTrack)",
                Description = "禁用 Connected User Experiences and Telemetry，减少后台数据上传",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_dmwappush", Category = "服务优化",
                Name = "禁用设备管理 WAP 推送 (dmwappushservice)",
                Description = "禁用设备管理推送服务，微软遥测的一部分",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_retaildemo", Category = "服务优化",
                Name = "禁用零售演示服务 (RetailDemo)",
                Description = "仅用于商店展示机的演示模式，个人电脑可禁用",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_mapsbroker", Category = "服务优化",
                Name = "禁用地图服务 (MapsBroker)",
                Description = "如果不用 Windows 内置地图，可禁用",
                Risk = RiskLevel.Safe },

        new() { Id = "svc_sysmain", Category = "服务优化",
                Name = "禁用 SysMain (SuperFetch)",
                Description = "SSD 用户可禁用，机械硬盘建议保留",
                Risk = RiskLevel.Medium },

        new() { Id = "svc_wsearch", Category = "服务优化",
                Name = "禁用 Windows Search",
                Description = "禁用索引服务，会失去开始菜单搜索。SSD 用户可禁",
                Risk = RiskLevel.Advanced },

        new() { Id = "svc_print", Category = "服务优化",
                Name = "禁用打印后台服务 (Spooler)",
                Description = "没有打印机时可禁用，同时降低 PrintNightmare 漏洞风险",
                Risk = RiskLevel.Medium },

        // ============ AI 组件 ============
        new() { Id = "ai_copilot", Category = "AI 组件",
                Name = "卸载 Windows Copilot",
                Description = "卸载系统内置的 Copilot 应用",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_recall", Category = "AI 组件",
                Name = "禁用 Recall（AI 截图历史）",
                Description = "Windows 11 24H2 的 AI 屏幕记录功能",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_webexp", Category = "AI 组件",
                Name = "卸载 Windows Web Experience Pack",
                Description = "移除「新闻和兴趣」小组件，减少后台占用",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_bing", Category = "AI 组件",
                Name = "禁用搜索中的 Bing 结果",
                Description = "让开始菜单搜索只返回本地结果",
                Risk = RiskLevel.Safe },

        new() { Id = "ai_paint", Category = "AI 组件",
                Name = "卸载 Paint Cocreator AI",
                Description = "移除画图应用的 AI 生成图片功能",
                Risk = RiskLevel.Safe },

        // ============ 隐私 ============
        new() { Id = "priv_telemetry", Category = "隐私",
                Name = "关闭诊断数据上传",
                Description = "将遥测级别设为 0（最低），减少诊断数据收集",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_adid", Category = "隐私",
                Name = "关闭广告 ID",
                Description = "禁用个性化广告追踪",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_activity", Category = "隐私",
                Name = "关闭活动历史记录",
                Description = "禁用跨设备的活动记录同步",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_location", Category = "隐私",
                Name = "禁用位置跟踪",
                Description = "关闭系统级定位服务",
                Risk = RiskLevel.Medium },

        new() { Id = "priv_cortana", Category = "隐私",
                Name = "禁用 Cortana",
                Description = "关闭 Cortana 语音助手",
                Risk = RiskLevel.Safe },

        new() { Id = "priv_consumer", Category = "隐私",
                Name = "关闭建议和推广内容",
                Description = "禁用开始菜单和锁屏的微软建议应用/广告",
                Risk = RiskLevel.Safe },

        // ★ 新增
        new() { Id = "priv_widgets", Category = "隐私",
                Name = "关闭 Win11 小组件",
                Description = "隐藏任务栏小组件按钮，并禁用小组件新闻推送，降低后台资源占用",
                Risk = RiskLevel.Safe },

        // ============ 性能 ============
        new() { Id = "perf_visual", Category = "性能",
                Name = "关闭视觉动画效果",
                Description = "禁用窗口动画、阴影等效果，提升响应速度",
                Risk = RiskLevel.Safe },

        new() { Id = "perf_power", Category = "性能",
                Name = "启用高性能电源计划",
                Description = "优先性能，笔记本续航会下降",
                Risk = RiskLevel.Medium },

        new() { Id = "perf_startup_delay", Category = "性能",
                Name = "禁用启动延迟",
                Description = "关闭登录后 10 秒的启动程序延迟",
                Risk = RiskLevel.Safe },

        new() { Id = "perf_game_mode", Category = "性能",
                Name = "开启游戏模式",
                Description = "玩游戏时暂停后台任务，提升帧率",
                Risk = RiskLevel.Safe },

        new() { Id = "perf_transparency", Category = "性能",
                Name = "关闭透明效果",
                Description = "禁用亚克力/云母透明效果，减少 GPU 占用",
                Risk = RiskLevel.Safe },

        // ============ 清理 ============
        new() { Id = "clean_temp", Category = "清理",
                Name = "清理用户临时文件",
                Description = "删除 %TEMP% 目录下的临时文件",
                Risk = RiskLevel.Safe },

        new() { Id = "clean_update", Category = "清理",
                Name = "清理 Windows Update 缓存",
                Description = "删除已下载的更新包，释放几 GB 空间",
                Risk = RiskLevel.Safe },

        new() { Id = "clean_thumb", Category = "清理",
                Name = "清理缩略图缓存",
                Description = "删除资源管理器的缩略图缓存",
                Risk = RiskLevel.Safe },

        new() { Id = "clean_old", Category = "清理",
                Name = "清理 Windows.old（升级后残留）",
                Description = "升级后 10 天内的旧系统备份，删除后无法回退",
                Risk = RiskLevel.Advanced },

        new() { Id = "clean_delivery", Category = "清理",
                Name = "清理交付优化缓存",
                Description = "删除 Windows 更新分发的临时文件",
                Risk = RiskLevel.Safe },

        // ============ 界面与体验（12）============
        new() { Id = "ui_menu_delay", Category = "界面与体验",
                Name = "菜单弹出零延迟", Description = "右键/下拉菜单立即出现（默认 400ms 延迟）",
                Apply = WrapCommand("reg add \"HKCU\\Control Panel\\Desktop\" /v MenuShowDelay /t REG_SZ /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Control Panel\\Desktop\" /v MenuShowDelay /t REG_SZ /d 400 /f"),
                Check = WrapCheckRegistryEquals(@"HKCU:\Control Panel\Desktop", "MenuShowDelay", 0) },
        new() { Id = "ui_min_anim", Category = "界面与体验",
                Name = "关闭窗口最小化动画", Description = "去掉最小化/还原的缩放动画，响应更快",
                Apply = WrapCommand("reg add \"HKCU\\Control Panel\\Desktop\\WindowMetrics\" /v MinAnimate /t REG_SZ /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Control Panel\\Desktop\\WindowMetrics\" /v MinAnimate /t REG_SZ /d 1 /f") },
        new() { Id = "ui_taskbar_left", Category = "界面与体验",
                Name = "任务栏图标居左（Win11）", Description = "开始按钮和图标对齐屏幕左侧，回到 Win10 布局",
                Restart = true,
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAl /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarAl /t REG_DWORD /d 1 /f") },
        new() { Id = "ui_taskbar_search", Category = "界面与体验",
                Name = "任务栏搜索改为图标", Description = "大搜索框缩成图标，释放任务栏空间",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search\" /v SearchboxTaskbarMode /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Search\" /v SearchboxTaskbarMode /t REG_DWORD /d 3 /f") },
        new() { Id = "ui_taskbar_taskview", Category = "界面与体验",
                Name = "隐藏任务视图按钮", Description = "去掉任务栏上的「任务视图」图标",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowTaskViewButton /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v ShowTaskViewButton /t REG_DWORD /d 1 /f") },
        new() { Id = "ui_taskbar_chat", Category = "界面与体验",
                Name = "隐藏任务栏聊天图标", Description = "去掉任务栏的 Teams/聊天按钮",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarMn /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v TaskbarMn /t REG_DWORD /d 1 /f") },
        new() { Id = "ui_classic_menu", Category = "界面与体验",
                Name = "恢复 Win10 经典右键菜单（Win11）", Description = "右键直接显示完整菜单，不用点「显示更多选项」",
                Restart = true, Risk = RiskLevel.Medium,
                Apply = WrapCommand("reg add \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\\InprocServer32\" /ve /f"),
                Revert = WrapCommand("reg delete \"HKCU\\Software\\Classes\\CLSID\\{86ca1aa0-34aa-4e8b-a509-50c905bae2a2}\" /f") },
        new() { Id = "ui_lockscreen_focus", Category = "界面与体验",
                Name = "关闭锁屏 Windows 聚焦广告", Description = "锁屏不再推送必应推广内容",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v RotatingLockScreenEnabled /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\ContentDeliveryManager\" /v RotatingLockScreenEnabled /t REG_DWORD /d 1 /f") },
        new() { Id = "ui_sticky_keys", Category = "界面与体验",
                Name = "关闭粘滞键连按触发", Description = "连按 5 次 Shift 不再弹出粘滞键提示",
                Apply = WrapCommand("reg add \"HKCU\\Control Panel\\Accessibility\\StickyKeys\" /v Flags /t REG_SZ /d 506 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Control Panel\\Accessibility\\StickyKeys\" /v Flags /t REG_SZ /d 510 /f") },
        new() { Id = "ui_show_ext", Category = "界面与体验",
                Name = "显示文件扩展名", Description = "资源管理器显示 .txt/.exe 等后缀，防伪装病毒",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v HideFileExt /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v HideFileExt /t REG_DWORD /d 1 /f"),
                Check = WrapCheckRegistryEquals(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "HideFileExt", 0) },
        new() { Id = "ui_show_hidden", Category = "界面与体验",
                Name = "显示隐藏文件", Description = "显示带隐藏属性的文件（谨慎使用）",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v Hidden /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v Hidden /t REG_DWORD /d 2 /f") },
        new() { Id = "ui_explorer_thispc", Category = "界面与体验",
                Name = "资源管理器默认打开「此电脑」", Description = "打开资源管理器直接进此电脑而不是主页",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v LaunchTo /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced\" /v LaunchTo /t REG_DWORD /d 2 /f") },

        // ============ 网络优化（12）============
        new() { Id = "net_dns_aliyun", Category = "网络优化",
                Name = "DNS 改为阿里+腾讯", Description = "223.5.5.5 / 119.29.29.29，国内解析更快更稳",
                Risk = RiskLevel.Medium,
                Apply = WrapCommand("Get-NetAdapter | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ServerAddresses '223.5.5.5','119.29.29.29'"),
                Revert = WrapCommand("Get-NetAdapter | Where-Object Status -eq 'Up' | Set-DnsClientServerAddress -ResetServerAddresses") },
        new() { Id = "net_do_off", Category = "网络优化",
                Name = "关闭更新传递优化（P2P 上传）", Description = "不再给陌生设备上传 Windows 更新，节省上行带宽",
                Apply = WrapCommand("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DODownloadMode /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DeliveryOptimization\" /v DODownloadMode /f") },
        new() { Id = "net_qos_limit0", Category = "网络优化",
                Name = "解除 QoS 保留带宽限制", Description = "系统默认保留 20% 带宽，改为 0",
                Apply = WrapCommand("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Psched\" /v NonBestEffortLimit /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg delete \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\Psched\" /v NonBestEffortLimit /f") },
        new() { Id = "net_ipv6_off", Category = "网络优化",
                Name = "关闭网卡 IPv6（仅家用网络）", Description = "解决部分路由器 IPv6 异常导致的网页卡顿",
                Restart = true, Risk = RiskLevel.Medium,
                Apply = WrapCommand("Get-NetAdapter | Disable-NetAdapterBinding -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue"),
                Revert = WrapCommand("Get-NetAdapter | Enable-NetAdapterBinding -ComponentID ms_tcpip6 -ErrorAction SilentlyContinue") },
        new() { Id = "net_nagle", Category = "网络优化",
                Name = "禁用 Nagle 算法（降低游戏延迟）", Description = "减少小包合并等待，游戏/远程响应更快",
                Risk = RiskLevel.Medium,
                Apply = WrapCommand("Get-NetAdapter | ForEach-Object { $k=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\\"+$_.InterfaceGuid; Set-ItemProperty $k TcpAckFrequency 1 -Type DWord -Force -EA SilentlyContinue; Set-ItemProperty $k TCPNoDelay 1 -Type DWord -Force -EA SilentlyContinue }"),
                Revert = WrapCommand("Get-NetAdapter | ForEach-Object { $k=\"HKLM:\\SYSTEM\\CurrentControlSet\\Services\\Tcpip\\Parameters\\Interfaces\\\"+$_.InterfaceGuid; Remove-ItemProperty $k TcpAckFrequency -EA SilentlyContinue; Remove-ItemProperty $k TCPNoDelay -EA SilentlyContinue }") },
        new() { Id = "net_flushdns", Category = "网络优化",
                Name = "刷新 DNS 解析缓存", Description = "清掉错误的域名缓存，解决「部分网站打不开」",
                Apply = WrapCommand("Clear-DnsClientCache") },
        new() { Id = "net_winsock", Category = "网络优化",
                Name = "重置 Winsock 网络栈", Description = "修复代理/加速器残留导致的全网上不了",
                Restart = true, Risk = RiskLevel.Medium,
                Apply = WrapCommand("netsh winsock reset") },
        new() { Id = "net_ip_reset", Category = "网络优化",
                Name = "重置 TCP/IP 协议栈", Description = "解决 IP 配置损坏、未识别网络",
                Restart = true, Risk = RiskLevel.Medium,
                Apply = WrapCommand("netsh int ip reset") },
        new() { Id = "net_adapter_power", Category = "网络优化",
                Name = "关闭网卡节能省电", Description = "避免网卡休眠导致掉线/延迟抖动",
                Apply = WrapCommand("Get-NetAdapter | Disable-NetAdapterPowerManagement -NoRestart -ErrorAction SilentlyContinue"),
                Revert = WrapCommand("Get-NetAdapter | Enable-NetAdapterPowerManagement -NoRestart -ErrorAction SilentlyContinue") },
        new() { Id = "net_proxy_clear", Category = "网络优化",
                Name = "清空系统代理设置", Description = "清掉加速器/梯子残留的代理，恢复直连",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings\" /v ProxyEnable /t REG_DWORD /d 0 /f; netsh winhttp reset proxy") },
        new() { Id = "net_time_ntp", Category = "网络优化",
                Name = "时间同步改阿里云 NTP", Description = "国内 NTP 服务器，校时更快更准",
                Apply = WrapCommand("w32tm /config /syncfromflags:manual /manualpeerlist:ntp.aliyun.com /update; w32tm /resync") },
        new() { Id = "net_rss", Category = "网络优化",
                Name = "开启网卡 RSS 接收缩放", Description = "多核分摊网络中断，大流量下降低 CPU 占用",
                Apply = WrapCommand("Get-NetAdapterRss -ErrorAction SilentlyContinue | Enable-NetAdapterRss -NoRestart -ErrorAction SilentlyContinue"),
                Revert = WrapCommand("Get-NetAdapterRss -ErrorAction SilentlyContinue | Disable-NetAdapterRss -NoRestart -ErrorAction SilentlyContinue") },

        // ============ 电源与续航（10）============
        new() { Id = "pw_monitor_never", Category = "电源与续航",
                Name = "插电时屏幕永不熄灭", Description = "接电源时关闭自动息屏（演示/挂机场景）",
                Apply = WrapCommand("powercfg /change monitor-timeout-ac 0"),
                Revert = WrapCommand("powercfg /change monitor-timeout-ac 15") },
        new() { Id = "pw_sleep_never", Category = "电源与续航",
                Name = "插电时永不睡眠", Description = "接电源时不自动睡眠，下载/挂机不断",
                Apply = WrapCommand("powercfg /change standby-timeout-ac 0"),
                Revert = WrapCommand("powercfg /change standby-timeout-ac 30") },
        new() { Id = "pw_usb_suspend_off", Category = "电源与续航",
                Name = "关闭 USB 选择性暂停", Description = "USB 设备不掉线，键鼠/声卡不瞬间卡顿",
                Apply = WrapCommand("powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0; powercfg /setactive SCHEME_CURRENT"),
                Revert = WrapCommand("powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 2; powercfg /setactive SCHEME_CURRENT") },
        new() { Id = "pw_hibernate_off", Category = "电源与续航",
                Name = "关闭休眠（释放 C 盘空间）", Description = "删除 hiberfil.sys，约等于内存大小的空间",
                Risk = RiskLevel.Medium,
                Apply = WrapCommand("powercfg /h off"),
                Revert = WrapCommand("powercfg /h on") },
        new() { Id = "pw_hibernate_reduced", Category = "电源与续航",
                Name = "休眠文件压缩为 40%", Description = "保留休眠但 hiberfil.sys 只占 40% 内存",
                Apply = WrapCommand("powercfg /h /type reduced"),
                Revert = WrapCommand("powercfg /h /type full") },
        new() { Id = "pw_faststartup_off", Category = "电源与续航",
                Name = "关闭快速启动", Description = "解决关机变重启/唤醒异常/双系统时间错乱",
                Apply = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HiberbootEnabled /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HiberbootEnabled /t REG_DWORD /d 1 /f") },
        new() { Id = "pw_cpu_min", Category = "电源与续航",
                Name = "CPU 最低状态 100%（插电）", Description = "避免降频卡顿，台式机/游戏本适用",
                Risk = RiskLevel.Medium,
                Apply = WrapCommand("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 100; powercfg /setactive SCHEME_CURRENT"),
                Revert = WrapCommand("powercfg /setacvalueindex SCHEME_CURRENT SUB_PROCESSOR PROCTHROTTLEMIN 5; powercfg /setactive SCHEME_CURRENT") },
        new() { Id = "pw_disk_never", Category = "电源与续航",
                Name = "硬盘永不休眠（插电）", Description = "机械盘不停转，避免唤醒等待",
                Apply = WrapCommand("powercfg /change disk-timeout-ac 0"),
                Revert = WrapCommand("powercfg /change disk-timeout-ac 20") },
        new() { Id = "pw_plan_balanced", Category = "电源与续航",
                Name = "切回平衡电源计划", Description = "从高性能/节能切回平衡（日常推荐）",
                Apply = WrapCommand("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e"),
                Revert = WrapCommand("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") },
        new() { Id = "pw_plan_saver", Category = "电源与续航",
                Name = "切换节能电源计划", Description = "笔记本外出临时续航救急",
                Apply = WrapCommand("powercfg /setactive a1841308-3541-4fab-bc81-f71556f20b4a"),
                Revert = WrapCommand("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e") },

        // ============ 游戏性能（9）============
        new() { Id = "game_dvr_off", Category = "游戏性能",
                Name = "关闭 GameDVR 录制", Description = "去掉后台录制开销，游戏帧数更稳",
                Apply = WrapCommand("reg add \"HKCU\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\System\\GameConfigStore\" /v GameDVR_Enabled /t REG_DWORD /d 1 /f") },
        new() { Id = "game_bgcap", Category = "游戏性能",
                Name = "关闭后台录像捕获", Description = "Win+G 后台录制彻底关闭",
                Apply = WrapCommand("reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v AppCaptureEnabled /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\GameDVR\" /v AppCaptureEnabled /t REG_DWORD /d 1 /f") },
        new() { Id = "game_hags", Category = "游戏性能",
                Name = "开启硬件加速 GPU 调度（HAGS）", Description = "降低延迟提升帧率，需显卡支持",
                Restart = true, Risk = RiskLevel.Medium,
                Apply = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\" /v HwSchMode /t REG_DWORD /d 2 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\GraphicsDrivers\" /v HwSchMode /t REG_DWORD /d 1 /f") },
        new() { Id = "game_mmcss", Category = "游戏性能",
                Name = "多媒体调度偏向前台（10%）", Description = "CPU 只留 10% 给后台多媒体服务",
                Apply = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v SystemResponsiveness /t REG_DWORD /d 10 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v SystemResponsiveness /t REG_DWORD /d 20 /f") },
        new() { Id = "game_tasks", Category = "游戏性能",
                Name = "游戏任务优先级调高", Description = "MMCSS Games 任务优先级 6 / GPU 优先级 8",
                Apply = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games\" /v Priority /t REG_DWORD /d 6 /f; reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games\" /v \"GPU Priority\" /t REG_DWORD /d 8 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games\" /v Priority /t REG_DWORD /d 2 /f; reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\\Tasks\\Games\" /v \"GPU Priority\" /t REG_DWORD /d 8 /f") },
        new() { Id = "game_fse", Category = "游戏性能",
                Name = "禁用全屏优化", Description = "独占全屏，降低输入延迟（个别游戏更稳）",
                Apply = WrapCommand("reg add \"HKCU\\System\\GameConfigStore\" /v GameDVR_FSEBehaviorMode /t REG_DWORD /d 2 /f"),
                Revert = WrapCommand("reg add \"HKCU\\System\\GameConfigStore\" /v GameDVR_FSEBehaviorMode /t REG_DWORD /d 0 /f") },
        new() { Id = "game_net_throttle", Category = "游戏性能",
                Name = "解除网络节流（游戏延迟）", Description = "关闭 MMCSS 每毫秒 10 包的网络限流",
                Risk = RiskLevel.Medium,
                Apply = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v NetworkThrottlingIndex /t REG_DWORD /d 4294967295 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows NT\\CurrentVersion\\Multimedia\\SystemProfile\" /v NetworkThrottlingIndex /t REG_DWORD /d 10 /f") },
        new() { Id = "game_win32_priority", Category = "游戏性能",
                Name = "前台程序优先级最高", Description = "Win32PrioritySeparation=38，前台获得更多 CPU 时间",
                Risk = RiskLevel.Medium,
                Apply = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\" /v Win32PrioritySeparation /t REG_DWORD /d 38 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\PriorityControl\" /v Win32PrioritySeparation /t REG_DWORD /d 2 /f") },
        new() { Id = "game_mouse_accel", Category = "游戏性能",
                Name = "关闭鼠标加速", Description = "指针移动与手移 1:1，FPS 玩家必备",
                Apply = WrapCommand("reg add \"HKCU\\Control Panel\\Mouse\" /v MouseSpeed /t REG_SZ /d 0 /f; reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold1 /t REG_SZ /d 0 /f; reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold2 /t REG_SZ /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Control Panel\\Mouse\" /v MouseSpeed /t REG_SZ /d 1 /f; reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold1 /t REG_SZ /d 6 /f; reg add \"HKCU\\Control Panel\\Mouse\" /v MouseThreshold2 /t REG_SZ /d 10 /f") },

        // ============ 安全加固（10）============
        new() { Id = "sec_autoreboot", Category = "安全加固",
                Name = "关闭蓝屏自动重启", Description = "蓝屏时停住显示错误代码，便于排查",
                Apply = WrapCommand("wmic recoveros set AutoReboot=False") },
        new() { Id = "sec_firewall_on", Category = "安全加固",
                Name = "确保防火墙全开", Description = "三个配置文件全部启用防火墙",
                Apply = WrapCommand("netsh advfirewall set allprofiles state on"),
                Revert = WrapCommand("netsh advfirewall set allprofiles state off") },
        new() { Id = "sec_remote_registry", Category = "安全加固",
                Name = "禁用远程注册表服务", Description = "阻止他人远程改你的注册表",
                Apply = WrapServiceDisable("RemoteRegistry"),
                Revert = WrapServiceEnable("RemoteRegistry", "Manual") },
        new() { Id = "sec_admin_share", Category = "安全加固",
                Name = "关闭默认管理共享", Description = "关闭 C$/D$ 等默认共享，减少内网攻击面",
                Apply = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters\" /v AutoShareWks /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\LanmanServer\\Parameters\" /v AutoShareWks /t REG_DWORD /d 1 /f") },
        new() { Id = "sec_rdp_off", Category = "安全加固",
                Name = "关闭远程桌面服务端", Description = "不使用远程桌面时关闭，防爆破",
                Risk = RiskLevel.Medium,
                Apply = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\" /v fDenyTSConnections /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Terminal Server\" /v fDenyTSConnections /t REG_DWORD /d 0 /f") },
        new() { Id = "sec_lsass", Category = "安全加固",
                Name = "开启 LSASS 保护（RunAsPPL）", Description = "凭据进程受保护，防凭据转储",
                Restart = true, Risk = RiskLevel.Medium,
                Apply = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Lsa\" /v RunAsPPL /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Lsa\" /v RunAsPPL /t REG_DWORD /d 0 /f") },
        new() { Id = "sec_restore_point", Category = "安全加固",
                Name = "立即创建系统还原点", Description = "优化前先留一手，可回滚（在系统保护开启时有效）",
                Apply = WrapCommand("Checkpoint-Computer -Description 'Vael优化前备份' -RestorePointType MODIFY_SETTINGS -ErrorAction SilentlyContinue") },
        new() { Id = "sec_quick_scan", Category = "安全加固",
                Name = "Defender 快速查杀", Description = "立即用 Windows Defender 扫描活跃威胁",
                Apply = WrapCommand("Start-MpScan -ScanType QuickScan -ErrorAction SilentlyContinue") },
        new() { Id = "sec_time_sync", Category = "安全加固",
                Name = "强制时间重新同步", Description = "修复时间不对导致的 HTTPS/激活报错",
                Apply = WrapCommand("w32tm /resync /force") },
        new() { Id = "sec_uac_desktop", Category = "安全加固",
                Name = "UAC 切换到安全桌面", Description = "UAC 提示在安全桌面显示，防伪造点击",
                Apply = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v PromptOnSecureDesktop /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System\" /v PromptOnSecureDesktop /t REG_DWORD /d 0 /f") },

        // ============ 输入与设备（6）============
        new() { Id = "dev_numlock", Category = "输入与设备",
                Name = "开机默认开启小键盘", Description = "登录后 NumLock 自动亮",
                Apply = WrapCommand("reg add \"HKCU\\Control Panel\\Keyboard\" /v InitialKeyboardIndicators /t REG_SZ /d 2147483650 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Control Panel\\Keyboard\" /v InitialKeyboardIndicators /t REG_SZ /d 2147483648 /f") },
        new() { Id = "dev_keyboard_fast", Category = "输入与设备",
                Name = "键盘重复速度最快", Description = "按住按键的重复延迟最短、速度最快",
                Apply = WrapCommand("reg add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardDelay /t REG_SZ /d 0 /f; reg add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardSpeed /t REG_SZ /d 31 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardDelay /t REG_SZ /d 1 /f; reg add \"HKCU\\Control Panel\\Keyboard\" /v KeyboardSpeed /t REG_SZ /d 31 /f") },
        new() { Id = "dev_wheel_lines", Category = "输入与设备",
                Name = "滚轮一次滚 6 行", Description = "默认 3 行改 6 行，浏览更快",
                Apply = WrapCommand("reg add \"HKCU\\Control Panel\\Desktop\" /v WheelScrollLines /t REG_SZ /d 6 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Control Panel\\Desktop\" /v WheelScrollLines /t REG_SZ /d 3 /f") },
        new() { Id = "dev_dark_mode", Category = "输入与设备",
                Name = "系统深色模式", Description = "系统和应用界面改为深色主题",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v AppsUseLightTheme /t REG_DWORD /d 0 /f; reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v SystemUseLightTheme /t REG_DWORD /d 0 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v AppsUseLightTheme /t REG_DWORD /d 1 /f; reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\" /v SystemUseLightTheme /t REG_DWORD /d 1 /f") },
        new() { Id = "dev_long_path", Category = "输入与设备",
                Name = "解除 260 字符路径限制", Description = "支持长路径，告别「路径太长无法删除」",
                Restart = true,
                Apply = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem\" /v LongPathsEnabled /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\FileSystem\" /v LongPathsEnabled /t REG_DWORD /d 0 /f") },
        new() { Id = "dev_ime_switch", Category = "输入与设备",
                Name = "用 Ctrl+Shift 切换输入法", Description = "习惯旧版切换键的用户",
                Apply = WrapCommand("reg add \"HKCU\\Keyboard Layout\\Toggle\" /v Language Hotkey /t REG_SZ /d 2 /f") },

        // ============ 存储与维护（12）============
        new() { Id = "maint_temp_user", Category = "存储与维护",
                Name = "清理用户临时文件", Description = "删除 %TEMP% 下的临时文件",
                Apply = WrapCommand("Remove-Item \"$env:TEMP\\*\" -Recurse -Force -ErrorAction SilentlyContinue") },
        new() { Id = "maint_temp_win", Category = "存储与维护",
                Name = "清理系统临时文件", Description = "删除 C:\\Windows\\Temp 下的临时文件",
                Apply = WrapCommand("Remove-Item \"$env:SystemRoot\\Temp\\*\" -Recurse -Force -ErrorAction SilentlyContinue") },
        new() { Id = "maint_sd_download", Category = "存储与维护",
                Name = "清理更新下载缓存", Description = "删除 SoftwareDistribution\\Download 下的更新包",
                Apply = WrapCommand("Remove-Item \"$env:SystemRoot\\SoftwareDistribution\\Download\\*\" -Recurse -Force -ErrorAction SilentlyContinue") },
        new() { Id = "maint_thumbnail", Category = "存储与维护",
                Name = "清理缩略图缓存", Description = "删除 Explorer 缩略图数据库，修复缩略图错乱",
                Apply = WrapCommand($"{S.TKill} /f /im explorer.exe; Remove-Item \"$env:LocalAppData\\Microsoft\\Windows\\Explorer\\thumbcache_*\" -Force -ErrorAction SilentlyContinue; Start-Process explorer.exe") },
        new() { Id = "maint_icon", Category = "存储与维护",
                Name = "重建图标缓存", Description = "删除 IconCache.db 并重启资源管理器，修复白图标",
                Apply = WrapCommand($"{S.TKill} /f /im explorer.exe; Remove-Item \"$env:LocalAppData\\IconCache.db\" -Force -ErrorAction SilentlyContinue; Start-Process explorer.exe") },
        new() { Id = "maint_dns", Category = "存储与维护",
                Name = "清理 DNS 缓存", Description = "刷新本地域名解析缓存",
                Apply = WrapCommand("Clear-DnsClientCache") },
        new() { Id = "maint_recycle", Category = "存储与维护",
                Name = "清空回收站", Description = "立即清空所有盘的回收站",
                Apply = WrapCommand("Clear-RecycleBin -Force -ErrorAction SilentlyContinue") },
        new() { Id = "maint_storage_sense", Category = "存储与维护",
                Name = "开启存储感知自动清理", Description = "系统自动清理临时文件和回收站",
                Apply = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicy\" /v 01 /t REG_DWORD /d 1 /f"),
                Revert = WrapCommand("reg add \"HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\StorageSense\\Parameters\\StoragePolicy\" /v 01 /t REG_DWORD /d 0 /f") },
        new() { Id = "maint_do_cache", Category = "存储与维护",
                Name = "清理传递优化缓存", Description = "删除为其他设备缓存的更新文件",
                Apply = WrapCommand("Delete-DeliveryOptimizationCache -Force -ErrorAction SilentlyContinue") },
        new() { Id = "maint_prefetch", Category = "存储与维护",
                Name = "清理 Prefetch 预读取", Description = "删除预读取数据（仅排查启动问题时用）",
                Apply = WrapCommand("Remove-Item \"$env:SystemRoot\\Prefetch\\*\" -Force -ErrorAction SilentlyContinue") },
        new() { Id = "maint_sfc", Category = "存储与维护",
                Name = "SFC 系统文件校验修复", Description = "扫描并修复损坏的系统文件（约 10-20 分钟）",
                Apply = WrapCommand("sfc /scannow") },
        new() { Id = "maint_dism", Category = "存储与维护",
                Name = "DISM 组件存储修复", Description = "修复系统镜像，解决更新失败（约 10-30 分钟）",
                Apply = WrapCommand("DISM /Online /Cleanup-Image /RestoreHealth") },
        new() { Id = "ui_widgetsoff", Category = "界面与体验",
                Name = "禁用任务栏小组件（策略）", Description = "组策略注册表方式彻底禁用 Windows 11 小组件/新闻资讯（AllowNewsAndInterests）",
                Apply = WrapRegistrySet(@"HKLM:\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 0),
                Revert = WrapRegistrySet(@"HKLM:\SOFTWARE\Policies\Microsoft\Dsh", "AllowNewsAndInterests", 1) },
        new() { Id = "ui_taskbarda", Category = "界面与体验",
                Name = "隐藏任务栏小组件按钮", Description = "移除任务栏左下角小组件/天气入口（TaskbarDa），重启资源管理器后生效",
                Apply = WrapRegistrySet(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 0),
                Revert = WrapRegistrySet(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "TaskbarDa", 1) },
        new() { Id = "ui_cliphist", Category = "界面与体验",
                Name = "开启剪贴板历史 (Win+V)", Description = "云剪贴板，跨设备同步",
                Apply = WrapRegistrySet(@"HKCU:\Software\Microsoft\Clipboard", "EnableClipboardHistory", 1),
                Revert = WrapRegistrySet(@"HKCU:\Software\Microsoft\Clipboard", "EnableClipboardHistory", 0) },
        new() { Id = "ui_winkey", Category = "界面与体验",
                Name = "禁用 Win 键菜单（游戏防误触）", Description = "游戏中按 Win 不再弹出开始菜单",
                Apply = WrapRegistrySet(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisabledHotkeys", 0),
                Revert = WrapRegistrySet(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced", "DisabledHotkeys", 1) },
        new() { Id = "net_autotune", Category = "网络优化",
                Name = "恢复 TCP 自动调优（下载慢时）", Description = "某些'优化软件'会关掉它导致限速",
                Apply = WrapCommand("netsh interface tcp set global autotuninglevel=normal") },
        new() { Id = "net_throttle", Category = "网络优化",
                Name = "解除网络限流（多媒体类）", Description = "后台下载/网盘满速",
                Apply = WrapRegistrySet(@"HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", -1) },
        new() { Id = "net_rss", Category = "网络优化",
                Name = "开启 RSS 接收缩放", Description = "多核网卡分流，降低 CPU 占用",
                Apply = WrapCommand("netsh int tcp set global rss=enabled") },
        new() { Id = "pw_usboff", Category = "电源与续航",
                Name = "关闭 USB 选择性暂停", Description = "防外设掉线/鼠标卡顿",
                Apply = WrapCommand("powercfg /setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 & powercfg /setactive SCHEME_CURRENT") },
        new() { Id = "game_hags", Category = "游戏性能",
                Name = "开启硬件加速 GPU 调度", Description = "降延迟（需 RTX/Navi 新卡+新驱动）",
                Apply = WrapRegistrySet(@"HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2),
                Revert = WrapRegistrySet(@"HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 1) },
        new() { Id = "game_mpo", Category = "游戏性能",
                Name = "禁用 MPO（解决闪烁/黑屏）", Description = "部分驱动下 MPO 导致闪屏，禁用可修复",
                Apply = WrapRegistrySet(@"HKLM:\SOFTWARE\Microsoft\Windows\Dwm", "OverlayTestMode", 5),
                Revert = WrapCommand("Remove-ItemProperty 'HKLM:\\SOFTWARE\\Microsoft\\Windows\\Dwm' -Name OverlayTestMode -EA SilentlyContinue") },
        new() { Id = "sec_smb", Category = "安全加固",
                Name = "关闭 SMBv1（防勒索）", Description = "WannaCry 攻击面，老协议无现代用途",
                Apply = WrapCommand("Disable-WindowsOptionalFeature -Online -FeatureName SMB1Protocol -NoRestart") },
        new() { Id = "sec_lsass", Category = "安全加固",
                Name = "LSASS 防护加固", Description = "防凭据转储（RunAsPPL）",
                Apply = WrapRegistrySet(@"HKLM:\SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL", 1),
                Revert = WrapRegistrySet(@"HKLM:\SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL", 0) },
        new() { Id = "dev_touchpad", Category = "输入与设备",
                Name = "提高精准触控板灵敏度", Description = "掌托按压更跟手（最高档）",
                Apply = WrapRegistrySet(@"HKCU:\Software\Microsoft\Windows\CurrentVersion\PrecisionTouchPad", "AAPLoompressAndTapMaxIntensity", 3) },
        new() { Id = "maint_dnsflush", Category = "存储与维护",
                Name = "一键网络体检", Description = "刷新 DNS + 重置 Winsock + IP（修复多数玄学断网）",
                Apply = WrapCommand("ipconfig /flushdns; netsh winsock reset; ipconfig /release; ipconfig /renew"),
                Restart = true },
    };

    // ============================================================
    // 应用
    // ============================================================
    public static async Task<(bool Success, string Message)> ApplyAsync(string id)
    {
        var script = GetApplyScript(id);
        if (string.IsNullOrEmpty(script))
            return (false, $"未找到优化脚本: {id}");

        LogService.Log($"应用优化: {id}", "优化");
        return await RunPowerShellAsync(script);
    }

    // ============================================================
    // 还原
    // ============================================================
    public static async Task<(bool Success, string Message)> RevertAsync(string id)
    {
        var script = GetRevertScript(id);
        if (string.IsNullOrEmpty(script))
            return (false, $"未找到还原脚本: {id}");

        LogService.Log($"还原优化: {id}", "优化");
        return await RunPowerShellAsync(script);
    }

    // ============================================================
    // 检查状态
    // ============================================================
    public static async Task<bool> CheckStatusAsync(string id)
    {
        var script = GetCheckScript(id);
        if (string.IsNullOrEmpty(script)) return false;

        var result = await RunPowerShellRawAsync(script);
        if (!result.Success) return false;

        return result.StdOut.Contains("TRUE", StringComparison.OrdinalIgnoreCase);
    }

    // ============================================================
    // 应用脚本
    // ============================================================
    private static string GetApplyScript(string id) =>
        AllItems.FirstOrDefault(i => i.Id == id)?.Apply ?? id switch
    {
        "svc_diagtrack" => WrapServiceDisable("DiagTrack"),
        "svc_dmwappush" => WrapServiceDisable("dmwappushservice"),
        "svc_retaildemo" => WrapServiceDisable("RetailDemo"),
        "svc_mapsbroker" => WrapServiceDisable("MapsBroker"),
        "svc_sysmain" => WrapServiceDisable("SysMain"),
        "svc_wsearch" => WrapServiceDisable("WSearch"),
        "svc_print" => WrapServiceDisable("Spooler"),

        "ai_copilot" => WrapAppxRemove("Microsoft.Copilot"),
        "ai_webexp" => WrapAppxRemove("WindowsWebExperiencePack"),
        "ai_paint" => WrapAppxRemove("Microsoft.Paint"),
        "ai_recall" => WrapCommand(@"Disable-WindowsOptionalFeature -Online -FeatureName 'Recall' -NoRestart -ErrorAction SilentlyContinue"),
        "ai_bing" => WrapCommand(@"Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'BingSearchEnabled' -Value 0 -Type DWord -Force; Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Search' -Name 'CortanaConsent' -Value 0 -Type DWord -Force"),

        "priv_telemetry" => WrapRegistrySet(
            @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "AllowTelemetry", 0),
        "priv_adid" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
            "Enabled", 0),
        "priv_activity" => WrapCommand(@"
$p = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\System'
if (-not (Test-Path $p)) { New-Item -Path $p -Force | Out-Null }
Set-ItemProperty -Path $p -Name 'EnableActivityFeed' -Value 0 -Type DWord -Force
Set-ItemProperty -Path $p -Name 'PublishUserActivities' -Value 0 -Type DWord -Force
Set-ItemProperty -Path $p -Name 'UploadUserActivities' -Value 0 -Type DWord -Force
"),
        "priv_location" => WrapServiceDisable("lfsvc"),
        "priv_cortana" => WrapRegistrySet(
            @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            "AllowCortana", 0),
        "priv_consumer" => WrapCommand(@"
Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338388Enabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SubscribedContent-338389Enabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager' -Name 'SystemPaneSuggestionsEnabled' -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
"),

        // ★ 关闭 Win11 小组件
        "priv_widgets" => WrapWidgetsDisable(),

        "perf_visual" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting", 2),
        "perf_power" => WrapCommand("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"),
        "perf_startup_delay" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Serialize",
            "StartupDelayInMSec", 0),
        "perf_game_mode" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\GameBar",
            "AutoGameModeEnabled", 1),
        "perf_transparency" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "EnableTransparency", 0),

        "clean_temp" => WrapCommand(@"Remove-Item -Path ""$env:TEMP\*"" -Recurse -Force -ErrorAction SilentlyContinue"),
        "clean_update" => WrapCommand(@"
Stop-Service wuauserv -Force -ErrorAction SilentlyContinue
Remove-Item -Path ""$env:SystemRoot\SoftwareDistribution\Download\*"" -Recurse -Force -ErrorAction SilentlyContinue
Start-Service wuauserv -ErrorAction SilentlyContinue
"),
        "clean_thumb" => WrapCommand(@"Remove-Item -Path ""$env:LOCALAPPDATA\Microsoft\Windows\Explorer\thumbcache_*.db"" -Force -ErrorAction SilentlyContinue"),
        "clean_old" => WrapCommand(@"
if (Test-Path 'C:\Windows.old') {
    takeown /F 'C:\Windows.old' /R /D Y 2>&1 | Out-Null
    icacls 'C:\Windows.old' /grant Administrators:F /T 2>&1 | Out-Null
    Remove-Item 'C:\Windows.old' -Recurse -Force -ErrorAction SilentlyContinue
}
"),
        "clean_delivery" => WrapCommand(@"Remove-Item -Path ""$env:SystemRoot\SoftwareDistribution\DeliveryOptimization\*"" -Recurse -Force -ErrorAction SilentlyContinue"),

        _ => ""
    };

    // ============================================================
    // 还原脚本
    // ============================================================
    private static string GetRevertScript(string id) =>
        AllItems.FirstOrDefault(i => i.Id == id)?.Revert ?? id switch
    {
        "svc_diagtrack" => WrapServiceEnable("DiagTrack", "Automatic"),
        "svc_dmwappush" => WrapServiceEnable("dmwappushservice", "Manual"),
        "svc_retaildemo" => WrapServiceEnable("RetailDemo", "Manual"),
        "svc_mapsbroker" => WrapServiceEnable("MapsBroker", "Automatic"),
        "svc_sysmain" => WrapServiceEnable("SysMain", "Automatic"),
        "svc_wsearch" => WrapServiceEnable("WSearch", "Automatic"),
        "svc_print" => WrapServiceEnable("Spooler", "Automatic"),

        "ai_recall" => WrapCommand(@"Enable-WindowsOptionalFeature -Online -FeatureName 'Recall' -NoRestart -ErrorAction SilentlyContinue"),
        "ai_bing" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Search",
            "BingSearchEnabled", 1),

        "priv_telemetry" => WrapRegistrySet(
            @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "AllowTelemetry", 3),
        "priv_adid" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
            "Enabled", 1),
        "priv_location" => WrapServiceEnable("lfsvc", "Manual"),
        "priv_cortana" => WrapRegistrySet(
            @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            "AllowCortana", 1),

        // ★ 恢复 Win11 小组件
        "priv_widgets" => WrapWidgetsEnable(),

        "perf_visual" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting", 0),
        "perf_power" => WrapCommand("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e"),
        "perf_game_mode" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\GameBar",
            "AutoGameModeEnabled", 0),
        "perf_transparency" => WrapRegistrySet(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "EnableTransparency", 1),

        _ => ""
    };

    // ============================================================
    // 检查脚本
    // ============================================================
    private static string GetCheckScript(string id) =>
        AllItems.FirstOrDefault(i => i.Id == id)?.Check ?? id switch
    {
        "svc_diagtrack" => WrapCheckServiceDisabled("DiagTrack"),
        "svc_dmwappush" => WrapCheckServiceDisabled("dmwappushservice"),
        "svc_retaildemo" => WrapCheckServiceDisabled("RetailDemo"),
        "svc_mapsbroker" => WrapCheckServiceDisabled("MapsBroker"),
        "svc_sysmain" => WrapCheckServiceDisabled("SysMain"),
        "svc_wsearch" => WrapCheckServiceDisabled("WSearch"),
        "svc_print" => WrapCheckServiceDisabled("Spooler"),
        "priv_location" => WrapCheckServiceDisabled("lfsvc"),

        "priv_telemetry" => WrapCheckRegistryEquals(
            @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
            "AllowTelemetry", 0),
        "priv_adid" => WrapCheckRegistryEquals(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\AdvertisingInfo",
            "Enabled", 0),
        "priv_cortana" => WrapCheckRegistryEquals(
            @"HKLM:\SOFTWARE\Policies\Microsoft\Windows\Windows Search",
            "AllowCortana", 0),

        // ★ 检查 Win11 小组件状态
        "priv_widgets" => WrapWidgetsCheck(),

        "perf_visual" => WrapCheckRegistryEquals(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects",
            "VisualFXSetting", 2),
        "perf_transparency" => WrapCheckRegistryEquals(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "EnableTransparency", 0),
        "perf_game_mode" => WrapCheckRegistryEquals(
            @"HKCU:\Software\Microsoft\GameBar",
            "AutoGameModeEnabled", 1),
        "ai_bing" => WrapCheckRegistryEquals(
            @"HKCU:\Software\Microsoft\Windows\CurrentVersion\Search",
            "BingSearchEnabled", 0),

        _ => ""
    };

    // ============================================================
    // ★ Win11 小组件脚本片段
    // ============================================================
    private static string WrapWidgetsDisable() => @"
$ErrorActionPreference = 'SilentlyContinue'
try {
    # 1. 隐藏任务栏小组件按钮
    $adv = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    if (-not (Test-Path $adv)) { New-Item -Path $adv -Force | Out-Null }
    Set-ItemProperty -Path $adv -Name 'TaskbarDa' -Value 0 -Type DWord -Force -ErrorAction Stop

    # 2. 通过策略完全禁用（Win11 22000+）
    $dsh = 'HKCU:\Software\Policies\Microsoft\Dsh'
    if (-not (Test-Path $dsh)) { New-Item -Path $dsh -Force | Out-Null }
    Set-ItemProperty -Path $dsh -Name 'AllowNewsAndInterests' -Value 0 -Type DWord -Force -ErrorAction Stop

    Write-Output 'SUCCESS'
    exit 0
} catch {
    Write-Output ""FAILED: $($_.Exception.Message)""
    exit 1
}
";

    private static string WrapWidgetsEnable() => @"
$ErrorActionPreference = 'SilentlyContinue'
try {
    # 1. 恢复任务栏小组件按钮
    $adv = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    if (Test-Path $adv) {
        Set-ItemProperty -Path $adv -Name 'TaskbarDa' -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    }

    # 2. 删除策略值
    $dsh = 'HKCU:\Software\Policies\Microsoft\Dsh'
    if (Test-Path $dsh) {
        Remove-ItemProperty -Path $dsh -Name 'AllowNewsAndInterests' -Force -ErrorAction SilentlyContinue
    }

    Write-Output 'SUCCESS'
    exit 0
} catch {
    Write-Output ""FAILED: $($_.Exception.Message)""
    exit 1
}
";

    private static string WrapWidgetsCheck() => @"
$adv = (Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarDa' -ErrorAction SilentlyContinue).TaskbarDa
$dsh = (Get-ItemProperty -Path 'HKCU:\Software\Policies\Microsoft\Dsh' -Name 'AllowNewsAndInterests' -ErrorAction SilentlyContinue).AllowNewsAndInterests
if ($adv -eq 0 -or $dsh -eq 0) { Write-Output 'TRUE' } else { Write-Output 'FALSE' }
";

    // ============================================================
    // 脚本片段辅助
    // ============================================================
    private static string WrapCommand(string body) => $@"
$ErrorActionPreference = 'SilentlyContinue'
{body}
exit 0
";

    private static string WrapServiceDisable(string serviceName) => $@"
$ErrorActionPreference = 'SilentlyContinue'
$svc = Get-Service -Name '{serviceName}' -ErrorAction SilentlyContinue
if ($null -eq $svc) {{
    Write-Output 'SKIP: 服务不存在'
    exit 0
}}
try {{
    Set-Service -Name '{serviceName}' -StartupType Disabled -ErrorAction Stop
    Stop-Service -Name '{serviceName}' -Force -ErrorAction SilentlyContinue
    $after = Get-Service -Name '{serviceName}' -ErrorAction SilentlyContinue
    if ($after.StartType -eq 'Disabled') {{
        Write-Output 'SUCCESS'
        exit 0
    }} else {{
        Write-Output ""FAILED: 状态未改变 ($($after.StartType))""
        exit 1
    }}
}} catch {{
    Write-Output ""FAILED: $($_.Exception.Message)""
    exit 1
}}
";

    private static string WrapServiceEnable(string serviceName, string startupType) => $@"
$ErrorActionPreference = 'SilentlyContinue'
$svc = Get-Service -Name '{serviceName}' -ErrorAction SilentlyContinue
if ($null -eq $svc) {{
    Write-Output 'SKIP: 服务不存在'
    exit 0
}}
try {{
    Set-Service -Name '{serviceName}' -StartupType {startupType} -ErrorAction Stop
    Start-Service -Name '{serviceName}' -ErrorAction SilentlyContinue
    Write-Output 'SUCCESS'
    exit 0
}} catch {{
    Write-Output ""FAILED: $($_.Exception.Message)""
    exit 1
}}
";

    private static string WrapCheckServiceDisabled(string serviceName) => $@"
$svc = Get-Service -Name '{serviceName}' -ErrorAction SilentlyContinue
if ($null -eq $svc) {{
    Write-Output 'FALSE'
}} elseif ($svc.StartType -eq 'Disabled') {{
    Write-Output 'TRUE'
}} else {{
    Write-Output 'FALSE'
}}
";

    private static string WrapRegistrySet(string path, string name, int value) => $@"
$ErrorActionPreference = 'Stop'
try {{
    if (-not (Test-Path '{path}')) {{
        New-Item -Path '{path}' -Force | Out-Null
    }}
    Set-ItemProperty -Path '{path}' -Name '{name}' -Value {value} -Type DWord -Force -ErrorAction Stop
    Write-Output 'SUCCESS'
    exit 0
}} catch {{
    Write-Output ""FAILED: $($_.Exception.Message)""
    exit 1
}}
";

    private static string WrapCheckRegistryEquals(string path, string name, int expected) => $@"
$val = (Get-ItemProperty -Path '{path}' -Name '{name}' -ErrorAction SilentlyContinue).{name}
if ($val -eq {expected}) {{ Write-Output 'TRUE' }} else {{ Write-Output 'FALSE' }}
";

    private static string WrapAppxRemove(string packagePattern) => $@"
$ErrorActionPreference = 'SilentlyContinue'
$pkgs = Get-AppxPackage -AllUsers -Name '*{packagePattern}*' -ErrorAction SilentlyContinue
if ($null -eq $pkgs) {{
    Write-Output 'SKIP: 未安装该应用'
    exit 0
}}
foreach ($p in $pkgs) {{
    try {{
        Remove-AppxPackage -Package $p.PackageFullName -AllUsers -ErrorAction Stop
    }} catch {{
        Write-Output ""FAILED: $($_.Exception.Message)""
        exit 1
    }}
}}
Write-Output 'SUCCESS'
exit 0
";

    // ============================================================
    // 执行
    // ============================================================
    private static async Task<(bool Success, string Message)> RunPowerShellAsync(string script)
    {
        var result = await RunPowerShellRawAsync(script);

        var stdout = result.StdOut ?? "";
        var exitCode = result.ExitCode;

        var output = stdout.Trim();

        if (output.Contains("SUCCESS"))
            return (true, "成功");

        if (output.Contains("SKIP:"))
            return (true, output);

        if (output.Contains("FAILED:"))
            return (false, output);

        if (exitCode == 0)
            return (true, "成功");

        return (false, $"退出码 {exitCode}");
    }

    private static async Task<(bool Success, int ExitCode, string StdOut)> RunPowerShellRawAsync(string script)
    {
        try
        {
            // ★ %TEMP% 在受限/沙箱环境不可写，改用可写的 AppDataDir 子目录
            var optDir = System.IO.Path.Combine(Models.ConfigManager.AppDataDir, "tmp");
            System.IO.Directory.CreateDirectory(optDir);
            var tempFile = System.IO.Path.Combine(
                optDir,
                $"vael_opt_{Guid.NewGuid():N}.ps1");
            await System.IO.File.WriteAllTextAsync(tempFile, script, System.Text.Encoding.UTF8);

            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -File \"{tempFile}\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
                StandardErrorEncoding = System.Text.Encoding.UTF8
            };

            using var proc = Process.Start(psi);
            if (proc == null)
                return (false, -1, "");

            var stdout = await proc.StandardOutput.ReadToEndAsync();
            var stderr = await proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            try { System.IO.File.Delete(tempFile); } catch { }

            if (string.IsNullOrWhiteSpace(stdout) && !string.IsNullOrWhiteSpace(stderr))
                stdout = "FAILED: " + stderr;

            return (proc.ExitCode == 0, proc.ExitCode, stdout);
        }
        catch (Exception ex)
        {
            return (false, -1, $"FAILED: {ex.Message}");
        }
    }
}