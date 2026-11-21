using SBtools.Models;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Updatum;

namespace SBtools.Services;

/// <summary>
/// 自动更新服务（单例）。检查用 Updatum，下载走自定义 HttpClient，
/// 更新方式是 ZIP 自替换（不调用 Inno 安装程序）。
/// </summary>
public sealed class UpdateService
{
    private static readonly Lazy<UpdateService> _lazy = new(() => new UpdateService());
    public static UpdateService Instance => _lazy.Value;

    private const string GithubOwner = "TIANJING-qwq";
    private const string GithubRepo  = "Vael";

    private readonly UpdatumManager _updater;
    private bool _initialized;

    private CancellationTokenSource? _downloadCts;
    private System.Timers.Timer? _autoCheckTimer;

    /// <summary>下载完成后暂存的更新目录（持久化，供「手动安装」使用）。</summary>
    private static readonly string PendingDir =
        Path.Combine(ConfigManager.AppDataDir, "pending_update");

    public event Action<int>? DownloadProgressChanged;
    public event Action<string>? DownloadSpeedChanged;
    public event Action<bool>? DownloadCompleted;
    public event Action<bool>? InstallCompleted;
    public event Action? DownloadCancelled;

    /// <summary>定时自动检查完成（是否有新版本），供主窗口后台自动更新使用。</summary>
    public event Action<bool>? AutoCheckCompleted;

    public string CurrentVersion => _updater.CurrentVersion.ToString();
    public string? LatestVersion  => _updater.LatestRelease?.TagName?.TrimStart('v');

    /// <summary>最近一次操作失败的原因（空 = 无错误）。</summary>
    public string? LastError { get; private set; }

    /// <summary>是否存在已下载、待安装的更新。</summary>
    public bool HasPendingUpdate =>
        Directory.Exists(PendingDir) && File.Exists(PendingExePath);

    /// <summary>待安装更新的主程序路径（Windows 为 SBtools.exe，其它平台无扩展名）。</summary>
    private static string PendingExeName => OperatingSystem.IsWindows() ? "SBtools.exe" : "SBtools";

    private static string PendingExePath => Path.Combine(PendingDir, PendingExeName);

    /// <summary>当前运行时标识，用来挑选对应平台的更新包。</summary>
    public static string CurrentRuntimeId()
    {
        var arch = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
        if (OperatingSystem.IsWindows()) return arch == System.Runtime.InteropServices.Architecture.Arm64 ? "win-arm64" : "win-x64";
        if (OperatingSystem.IsLinux())   return arch == System.Runtime.InteropServices.Architecture.Arm64 ? "linux-arm64" : "linux-x64";
        if (OperatingSystem.IsMacOS())   return arch == System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64";
        return "win-x64";
    }

    private UpdateService()
    {
        _updater = new UpdatumManager(GithubOwner, GithubRepo)
        {
            // ★ 只匹配 ZIP（忽略 Setup.exe）
            // ★ 按当前平台过滤；DownloadAsync 里还会再精确匹配一次 RID
            AssetRegexPattern = @$"Vael.*{CurrentRuntimeId()}.*\.zip$",
            DownloadProgressUpdateFrequencySeconds = 0.3,
        };
    }

    public void Initialize()
    {
        if (_initialized) return;
        _initialized = true;
        LogService.Log($"更新服务已初始化，当前版本 {CurrentVersion}", "更新");
    }

    public void CheckQuietly()
    {
        try
        {
            LogService.Log("启动自动检查更新...", "更新");
            _ = CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            LogService.Log($"自动检查失败: {ex.Message}", "更新");
        }
    }

    public async Task<bool> CheckForUpdatesAsync(bool force = false)
    {
        try
        {
            var hasUpdate = await _updater.CheckForUpdatesAsync();

            // ★ 下载/安装进行中：直接返回，避免并发触发第二条更新流程
            if (Busy)
            {
                LogService.Log("更新流程进行中，跳过本次检查", "更新");
                return false;
            }

            // 强制测试模式：忽略库内比较，视为有更新
            if (force && !hasUpdate && !string.IsNullOrEmpty(LatestVersion))
            {
                LogService.Log($"强制测试更新：忽略版本比较（远端 {LatestVersion}）", "更新");
                hasUpdate = true;
            }

            // ★ 版本守卫：不信库内部比较，自行比较一次（杜绝"已是最新还更新"）
            if (hasUpdate && !force &&
                Version.TryParse((LatestVersion ?? "").TrimStart('v', 'V'), out var latest) &&
                Version.TryParse(CurrentVersion, out var cur) &&
                latest <= cur)
            {
                LogService.Log($"忽略非更新版本: {LatestVersion} <= {CurrentVersion}", "更新");
                return false;
            }

            if (hasUpdate)
                LogService.Log($"发现新版本: {LatestVersion}", "更新");
            else
                LogService.Log("当前已是最新版本", "更新");

            return hasUpdate;
        }
        catch (Exception ex)
        {
            LogService.Log($"检查更新失败: {ex.Message}", "更新");
            return false;
        }
    }

    /// <summary>获取 GitHub Releases 在线版本日志（tag, 正文），失败返回空</summary>
    public async Task<System.Collections.Generic.List<(string Tag, string Body)>> GetOnlineChangelogAsync(int count = 10)
    {
        var result = new System.Collections.Generic.List<(string, string)>();
        try
        {
            string json;
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Vael-Updater");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                http.Timeout = TimeSpan.FromSeconds(20);
                json = await http.GetStringAsync(
                    $"https://api.github.com/repos/{GithubOwner}/{GithubRepo}/releases?per_page={count}");
            }
            foreach (var rel in Newtonsoft.Json.Linq.JArray.Parse(json))
            {
                var tag = (string?)rel["tag_name"] ?? "?";
                var body = ((string?)rel["body"] ?? "").Trim();
                if (tag.StartsWith("v", StringComparison.OrdinalIgnoreCase)) tag = tag[1..];
                result.Add((tag, body));
            }
        }
        catch (Exception ex) { LogService.Log($"在线版本日志获取失败: {ex.Message}", "更新"); }
        return result;
    }

    /// <summary>更新器重启参数文件（安装器 [Code] 读取后拉起程序）</summary>
    public static string RelaunchArgsFile => Path.Combine(ConfigManager.AppDataDir, "relaunch_args.txt");

    /// <summary>
    /// 记录"更新完成后用什么参数重启"：
    /// 本次是开机自启（--autostart）→ 重启后仍然静默，不再弹主窗口。
    /// </summary>
    private static void WriteRelaunchArgs()
    {
        try
        {
            var args = Environment.GetCommandLineArgs();
            var relaunch = args.Any(a => a.Equals("--autostart", StringComparison.OrdinalIgnoreCase))
                ? "--autostart"
                : "";
            Directory.CreateDirectory(ConfigManager.AppDataDir);
            File.WriteAllText(RelaunchArgsFile, relaunch);
            LogService.Log($"更新重启模式已记录: [{(string.IsNullOrEmpty(relaunch) ? "正常显示" : relaunch)}]", "更新");
        }
        catch { }
    }

    /// <summary>更新流程进行中（检查/下载/安装互斥，防自动检查与下载并发打架）</summary>
    public bool Busy { get; private set; }

    // ============================================================
    // ★ 强制测试更新：绕过 Updatum 的版本比较/资产过滤，直接拉最新 Release 的
    //   安装包下载并静默安装（本地已是最新版本时也能完整走一遍升级流程）
    // ============================================================
    public async Task<bool> ForceLatestSetupAsync()
    {
        if (Busy) { LogService.Log("更新流程进行中，跳过强制测试更新", "更新"); return false; }
        Busy = true;
        LastError = null;
        try
        {
            LogService.Log("强制测试更新：直接获取最新 Release", "更新");
            string json;
            using (var http = new HttpClient())
            {
                http.DefaultRequestHeaders.UserAgent.ParseAdd("Vael-Updater");
                http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
                http.Timeout = TimeSpan.FromSeconds(30);
                json = await http.GetStringAsync(
                    $"https://api.github.com/repos/{GithubOwner}/{GithubRepo}/releases/latest");
            }

            var rel = Newtonsoft.Json.Linq.JObject.Parse(json);
            var tagName = (string?)rel["tag_name"] ?? "?";
            var assets = (Newtonsoft.Json.Linq.JArray?)rel["assets"];
            string? dl = null, assetName = null;
            foreach (var a in assets ?? new Newtonsoft.Json.Linq.JArray())
            {
                var an = (string?)a["name"] ?? "";
                if (an.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    an.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                {
                    dl = (string?)a["browser_download_url"];
                    assetName = an;
                    break;
                }
            }
            if (dl == null)
            {
                LastError = $"最新 Release（{tagName}）没有 Windows 安装包（Vael_Setup）";
                LogService.Log(LastError, "更新");
                return false;
            }
            LogService.Log($"强制测试更新：{assetName}（{tagName}）", "更新");

            var setupPath = Path.Combine(ConfigManager.AppDataDir, "updates", assetName ?? "Vael_Setup.exe");
            try { Directory.CreateDirectory(Path.GetDirectoryName(setupPath)!); } catch { }
            try { File.Delete(setupPath); } catch { }
            await DownloadFileAsync((ConfigManager.Instance.UpdateProxy ?? "") + dl,
                setupPath, CancellationToken.None);

            var len = 0L;
            try { len = new FileInfo(setupPath).Length; } catch { }
            if (len < 5 * 1024 * 1024)
            {
                LastError = $"安装包下载不完整（{len / 1024} KB），已中止";
                LogService.Log(LastError, "更新");
                try { File.Delete(setupPath); } catch { }
                return false;
            }
            try { File.Delete(setupPath + ":Zone.Identifier"); } catch { }

            WriteRelaunchArgs(); // 保留当前启动模式（开机静默 → 更新后仍静默）
            LogService.Log($"安装包就绪: {setupPath}，启动更新器（带进度条，装完自动重启）", "更新");
            Process.Start(new ProcessStartInfo(setupPath, "/SILENT /NORESTART /SUPPRESSMSGBOXES")
            { UseShellExecute = true });
            await Task.Delay(1000);
            Environment.Exit(0);
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogService.Log($"强制测试更新失败: {ex.Message}", "更新");
            return false;
        }
        finally { Busy = false; }
    }

    // ============================================================
    // ★ 下载：Windows 直接拉安装包并静默升级（全后台可选）
    // ============================================================
    /// <param name="silentInstall">true=全后台（/VERYSILENT，无任何窗口）；false=显示安装进度条（/SILENT）</param>
    public async Task<bool> DownloadAsync(bool silentInstall = false)
    {
        if (Busy) return false;
        Busy = true;
        _downloadCts = new CancellationTokenSource();
        var token = _downloadCts.Token;
        LastError = null;

        try
        {
            LogService.Log("开始下载更新...", "更新");

            var release = _updater.LatestRelease;
            if (release?.Assets == null || release.Assets.Count == 0)
            {
                LastError = "Release 里没有资产";
                LogService.Log(LastError, "更新");
                DownloadCompleted?.Invoke(false);
                return false;
            }

            // ★ Windows 优先用安装包静默升级：
            //   不再"解压 dll 到 AppData + bat 换文件"（该模式易被启发式判为木马），
            //   改由 Inno 安装器正常替换文件。
            if (OperatingSystem.IsWindows())
            {
                var setup = release.Assets.FirstOrDefault(a =>
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase));
                if (setup != null)
                {
                    LogService.Log($"已选择安装包资产: {setup.Name}", "更新");
                    var setupPath = Path.Combine(ConfigManager.AppDataDir, "updates", setup.Name);
                    try { Directory.CreateDirectory(Path.GetDirectoryName(setupPath)!); } catch { }
                    try { File.Delete(setupPath); } catch { }

                    await DownloadFileAsync(
                        (ConfigManager.Instance.UpdateProxy ?? "") + setup.BrowserDownloadUrl,
                        setupPath, token);

                    // ★ 完整性校验：远端返回 HTML/0 字节时绝不能当安装包执行
                    var len = 0L;
                    try { len = new FileInfo(setupPath).Length; } catch { }
                    if (len < 5 * 1024 * 1024)
                    {
                        LastError = $"安装包下载不完整（{len / 1024} KB），已中止";
                        LogService.Log(LastError, "更新");
                        try { File.Delete(setupPath); } catch { }
                        DownloadCompleted?.Invoke(false);
                        return false;
                    }
                    // 去掉 MOTW 标记，避免"来自 Internet 的文件"二次拦截
                    try { File.Delete(setupPath + ":Zone.Identifier"); } catch { }
                    LogService.Log($"安装包下载完成: {setupPath}（{len / 1024 / 1024} MB）", "更新");

                    // ★ 记录重启模式：开机静默启动的实例，更新重启后仍保持静默（不弹主窗口）
                    WriteRelaunchArgs();

                    try { DownloadCompleted?.Invoke(true); } catch { }
                    // silentInstall=true：/VERYSILENT 全后台无窗口；否则 /SILENT 显示进度条
                    var flags = silentInstall
                        ? "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES"
                        : "/SILENT /NORESTART /SUPPRESSMSGBOXES";
                    LogService.Log($"启动安装器（{flags}）", "更新");
                    Process.Start(new ProcessStartInfo(setupPath, flags) { UseShellExecute = true });
                    await Task.Delay(1200);
                    Environment.Exit(0);
                    return true;
                }
                // ★ Windows 不再回退 ZIP 更新：解压 dll 到 AppData 正是误报根源
                LastError = "Release 缺少 Windows 安装包（Vael_Setup），请到发布页手动下载";
                LogService.Log(LastError, "更新");
                DownloadCompleted?.Invoke(false);
                return false;
            }

            // ★ 非 Windows：不做"解压 ZIP + 替换 DLL"（旧方案既误报又不可靠），
            //   直接提示到发布页手动下载。整段 ZIP/pending_update 死代码已删除。
            LastError = $"当前平台（{CurrentRuntimeId()}）不支持自动升级，请到 GitHub Releases 手动下载";
            LogService.Log(LastError, "更新");
            DownloadCompleted?.Invoke(false);
            return false;
        }
        catch (OperationCanceledException)
        {
            LastError = "用户取消了下载";
            LogService.Log(LastError, "更新");
            DownloadCancelled?.Invoke();
            DownloadCompleted?.Invoke(false);
            return false;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            LogService.Log($"下载失败: {ex.Message}", "更新");
            DownloadCompleted?.Invoke(false);
            return false;
        }
        finally
        {
            Busy = false; // 失败/取消后允许重试（安装包成功路径随 Exit 退出，复位无影响）
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    // ============================================================
    // ★ 安装：Windows 由安装包接管（DownloadAsync 里已启动并退出）；
    //   其余平台不支持自动替换，明确报错。旧版"bat + xcopy + taskkill"
    //   替换脚本已整体删除（死代码，且是杀软启发式误报的字符串特征）。
    // ============================================================
    public Task<bool> InstallAsync(IProgress<double>? progress = null)
    {
        try
        {
            progress?.Report(0.1);
            if (HasPendingUpdate)
            {
                LastError = "更新包已就绪，但当前平台不支持自动替换，请手动安装";
                LogService.Log(LastError, "更新");
            }
            else
            {
                LastError = OperatingSystem.IsWindows()
                    ? "Windows 更新由安装包直接完成，无需二次安装"
                    : "没有可安装的更新";
                LogService.Log(LastError, "更新");
            }
            InstallCompleted?.Invoke(false);
            return Task.FromResult(false);
        }
        catch (Exception ex)
        {
            LogService.Log($"安装失败: {ex.Message}", "更新");
            InstallCompleted?.Invoke(false);
            return Task.FromResult(false);
        }
    }

    /// <summary>下载后立即安装（一步到位）。</summary>
    public async Task<bool> DownloadAndInstallAsync()
    {
        if (!await DownloadAsync()) return false;
        return await InstallAsync();
    }

    public void CancelDownload()
    {
        try
        {
            _downloadCts?.Cancel();
            LogService.Log("已请求取消下载", "更新");
        }
        catch { }
    }

    public string GetChangelog()
    {
        try { return _updater.GetChangelog() ?? "暂无更新说明"; }
        catch { return "暂无更新说明"; }
    }

    public void StartAutoCheck(TimeSpan? interval = null)
    {
        try
        {
            var span = interval ?? TimeSpan.FromHours(1);
            StopAutoCheck();

            _autoCheckTimer = new System.Timers.Timer(span.TotalMilliseconds) { AutoReset = true };
            _autoCheckTimer.Elapsed += async (_, _) =>
            {
                try
                {
                    var hasUpdate = await CheckForUpdatesAsync();
                    AutoCheckCompleted?.Invoke(hasUpdate);
                }
                catch (Exception ex)
                {
                    LogService.Log($"定时检查异常: {ex.Message}", "更新");
                }
            };
            _autoCheckTimer.Start();

            LogService.Log($"定时自动检查已启动，间隔 {span}", "更新");
        }
        catch (Exception ex)
        {
            LogService.Log($"启动定时检查失败: {ex.Message}", "更新");
        }
    }

    public void StopAutoCheck()
    {
        try
        {
            _autoCheckTimer?.Stop();
            _autoCheckTimer?.Dispose();
            _autoCheckTimer = null;
        }
        catch { }
    }

    // ============================================================
    // 下载文件（带进度 + 速度）
    // ============================================================
    private async Task DownloadFileAsync(string url, string destPath, CancellationToken token)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(30);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("Vael/1.0");

        using var response = await http.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, token);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? 0;

        using var stream = await response.Content.ReadAsStreamAsync(token);
        using var fileStream = File.Create(destPath);

        var buffer = new byte[81920];
        long totalRead = 0;
        var lastReport = DateTime.Now;
        long lastBytes = 0;
        int lastPercent = -1;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            var read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), token);
            if (read == 0) break;

            await fileStream.WriteAsync(buffer.AsMemory(0, read), token);
            totalRead += read;

            var now = DateTime.Now;
            var elapsed = (now - lastReport).TotalSeconds;
            if (elapsed >= 0.3)
            {
                if (totalBytes > 0)
                {
                    var percent = (int)(totalRead * 100 / totalBytes);
                    if (percent != lastPercent)
                    {
                        lastPercent = percent;
                        DownloadProgressChanged?.Invoke(percent);
                    }
                }

                var bytesPerSec = (totalRead - lastBytes) / elapsed;
                DownloadSpeedChanged?.Invoke(FormatSpeed(bytesPerSec));

                lastReport = now;
                lastBytes = totalRead;
            }
        }

        DownloadProgressChanged?.Invoke(100);
        DownloadSpeedChanged?.Invoke("完成");
    }

    private static string FormatSpeed(double bps)
    {
        if (bps <= 0) return "0 MB/s";
        const double KB = 1024;
        const double MB = 1024 * 1024;
        if (bps >= MB) return $"{bps / MB:F2} MB/s";
        if (bps >= KB) return $"{bps / KB:F0} KB/s";
        return $"{bps:F0} B/s";
    }
}