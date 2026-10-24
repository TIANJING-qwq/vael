using System;
using System.Collections.Generic;
using System.Diagnostics;
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
    private static string GetApplyScript(string id) => id switch
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
    private static string GetRevertScript(string id) => id switch
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
    private static string GetCheckScript(string id) => id switch
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