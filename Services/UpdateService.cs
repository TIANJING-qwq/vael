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

    public async Task<bool> CheckForUpdatesAsync()
    {
        try
        {
            var hasUpdate = await _updater.CheckForUpdatesAsync();

            // ★ 版本守卫：不信库内部比较，自行比较一次（杜绝"已是最新还更新"）
            if (hasUpdate &&
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

    // ============================================================
    // ★ 下载：ZIP → 解压 → 展平 → 暂存到 pending_update
    // ============================================================
    public async Task<bool> DownloadAsync()
    {
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
                    var setupPath = Path.Combine(ConfigManager.AppDataDir,
                        $"Vael_setup_{Guid.NewGuid():N}.exe");

                    await DownloadFileAsync(
                        (ConfigManager.Instance.UpdateProxy ?? "") + setup.BrowserDownloadUrl,
                        setupPath, token);
                    LogService.Log($"安装包下载完成: {setupPath}，启动静默升级", "更新");

                    try { DownloadCompleted?.Invoke(true); } catch { }
                    Process.Start(new ProcessStartInfo(setupPath,
                        "/VERYSILENT /NORESTART /SUPPRESSMSGBOXES")
                    { UseShellExecute = true });
                    await Task.Delay(1000);
                    Environment.Exit(0);
                    return true;
                }
                // ★ Windows 不再回退 ZIP 更新：解压 dll 到 AppData 正是误报根源
                LastError = "Release 缺少 Windows 安装包（Vael_Setup），请到发布页手动下载";
                LogService.Log(LastError, "更新");
                DownloadCompleted?.Invoke(false);
                return false;
            }

            // ★ 按平台挑选对应资产（不能再取第一个 zip，否则会拿到别的平台包）
            var rid = CurrentRuntimeId();
            var asset = release.Assets.FirstOrDefault(a =>
                            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                            a.Name.Contains(rid, StringComparison.OrdinalIgnoreCase))
                        ?? release.Assets.FirstOrDefault(a =>
                            a.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) &&
                            a.Name.Contains(PlatformInfo.Name, StringComparison.OrdinalIgnoreCase));

            if (asset == null)
            {
                LastError = $"Release 里没有匹配当前平台（{rid}）的 .zip 资产";
                LogService.Log(LastError, "更新");
                DownloadCompleted?.Invoke(false);
                return false;
            }

            LogService.Log($"已选择资产: {asset.Name}（当前平台 {rid}）", "更新");

            var originalUrl = asset.BrowserDownloadUrl;
            var proxy = ConfigManager.Instance.UpdateProxy ?? "";
            var downloadUrl = string.IsNullOrWhiteSpace(proxy)
                ? originalUrl
                : proxy + originalUrl;

            LogService.Log($"下载地址: {downloadUrl}", "更新");

            // 1. 下载 ZIP 到数据目录（受限环境自动回退到可写位置）
            var zipPath = Path.Combine(
                ConfigManager.AppDataDir,
                $"Vael_update_{Guid.NewGuid():N}.zip");

            await DownloadFileAsync(downloadUrl, zipPath, token);
            LogService.Log($"下载完成: {zipPath}", "更新");

            // 2. 解压到 AppData 下的临时目录（与 pending_update 同盘，便于 rename）
            Directory.CreateDirectory(ConfigManager.AppDataDir);
            var extractDir = Path.Combine(
                ConfigManager.AppDataDir,
                $".update_tmp_{Guid.NewGuid():N}");

            try
            {
                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                ZipFile.ExtractToDirectory(zipPath, extractDir);
                LogService.Log($"解压完成: {extractDir}", "更新");
                try { File.Delete(zipPath); } catch { }
            }
            catch (Exception ex)
            {
                LastError = $"解压失败: {ex.Message}";
                LogService.Log(LastError, "更新");
                DownloadCompleted?.Invoke(false);
                return false;
            }

            // 3. 找到新的主程序（Windows 为 SBtools.exe；其它平台是无扩展名的可执行文件）
            var exeName = OperatingSystem.IsWindows() ? "SBtools.exe" : "SBtools";
            var newExe = Path.Combine(extractDir, exeName);
            if (!File.Exists(newExe))
            {
                newExe = Directory.GetFiles(extractDir, exeName, SearchOption.AllDirectories)
                    .FirstOrDefault() ?? "";
            }

            if (string.IsNullOrEmpty(newExe) || !File.Exists(newExe))
            {
                LastError = $"ZIP 里没有主程序 {exeName}（平台不匹配？当前 {CurrentRuntimeId()}）";
                LogService.Log(LastError, "更新");

                // 清理这次解压出来的垃圾，避免残留占空间
                try { Directory.Delete(extractDir, true); } catch { }

                DownloadCompleted?.Invoke(false);
                return false;
            }

            // 若 exe 在子目录，把子目录内容整体上提到顶层
            var newExeDir = Path.GetDirectoryName(newExe)!;
            if (!newExeDir.Equals(extractDir, StringComparison.OrdinalIgnoreCase))
            {
                LogService.Log($"exe 位于子目录 {newExeDir}，正在展平...", "更新");

                foreach (var file in Directory.GetFiles(newExeDir, "*", SearchOption.AllDirectories))
                {
                    var rel = Path.GetRelativePath(newExeDir, file);
                    var dst = Path.Combine(extractDir, rel);
                    Directory.CreateDirectory(Path.GetDirectoryName(dst)!);
                    File.Move(file, dst, true);
                }

                try { Directory.Delete(newExeDir, true); } catch { }
            }

            // 4. 暂存到 pending_update（原子 rename，避免临时目录被清理）
            try
            {
                if (Directory.Exists(PendingDir)) Directory.Delete(PendingDir, true);
                Directory.Move(extractDir, PendingDir);
                LogService.Log($"更新已暂存到: {PendingDir}", "更新");
            }
            catch (Exception ex)
            {
                LastError = $"暂存更新失败: {ex.Message}";
                LogService.Log(LastError, "更新");
                DownloadCompleted?.Invoke(false);
                return false;
            }

            DownloadCompleted?.Invoke(true);
            return true;
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
            _downloadCts?.Dispose();
            _downloadCts = null;
        }
    }

    // ============================================================
    // ★ 安装：从 pending_update 生成替换脚本 → 退出主程序
    // ============================================================
    public async Task<bool> InstallAsync(IProgress<double>? progress = null)
    {
        try
        {
            LogService.Log("开始安装更新...", "更新");
            progress?.Report(0.10);

            if (!HasPendingUpdate)
            {
                LogService.Log("没有可安装的更新", "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }
            progress?.Report(0.25);

            // 当前程序目录
            var currentExe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(currentExe))
                currentExe = Path.Combine(AppContext.BaseDirectory, PendingExeName);

            var targetDir = Path.GetDirectoryName(currentExe)!;
            LogService.Log($"更新目标目录: {targetDir}", "更新");

            // ★ 非 Windows 没有 cmd/bat 替换脚本，只能提示手动更新
            if (!OperatingSystem.IsWindows())
            {
                LastError = $"当前平台（{CurrentRuntimeId()}）不支持自动安装更新，请手动下载新版本覆盖";
                LogService.Log(LastError, "更新");
                InstallCompleted?.Invoke(false);
                return false;
            }

            var batPath = Path.Combine(Path.GetTempPath(),
                $"Vael_update_{Guid.NewGuid():N}.bat");

            var batContent = BuildUpdateScript(
                PendingDir, targetDir, currentExe, "");
            File.WriteAllText(batPath, batContent, Encoding.ASCII);
            progress?.Report(0.55);

            LogService.Log($"替换脚本已生成: {batPath}", "更新");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c \"\"{batPath}\"\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            Process.Start(psi);
            progress?.Report(0.85);
            LogService.Log("更新脚本已启动，准备退出主程序", "更新");

            InstallCompleted?.Invoke(true);

            await Task.Delay(700);
            progress?.Report(1.0);
            await Task.Delay(400);
            Environment.Exit(0);

            return true;
        }
        catch (Exception ex)
        {
            LogService.Log($"安装失败: {ex.Message}", "更新");
            InstallCompleted?.Invoke(false);
            return false;
        }
    }

    /// <summary>下载后立即安装（一步到位）。</summary>
    public async Task<bool> DownloadAndInstallAsync()
    {
        if (!await DownloadAsync()) return false;
        return await InstallAsync();
    }

    // ============================================================
    // 生成替换脚本
    // ============================================================
    private static string BuildUpdateScript(
        string sourceDir, string targetDir, string exePath, string zipPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("@echo off");
        sb.AppendLine("chcp 65001 >nul 2>&1");
        sb.AppendLine("setlocal");
        sb.AppendLine("title Vael Update");
        sb.AppendLine("");
        sb.AppendLine("echo Waiting for SBtools.exe to exit...");
        sb.AppendLine("");
        sb.AppendLine(":wait_loop");
        sb.AppendLine("tasklist /FI \"IMAGENAME eq SBtools.exe\" 2>nul | find /I \"SBtools.exe\" >nul");
        sb.AppendLine("if not errorlevel 1 (");
        sb.AppendLine("    taskkill /F /IM SBtools.exe >nul 2>&1");
        sb.AppendLine("    timeout /t 1 /nobreak >nul");
        sb.AppendLine("    goto wait_loop");
        sb.AppendLine(")");
        sb.AppendLine("");
        sb.AppendLine("timeout /t 1 /nobreak >nul");
        sb.AppendLine("");
        sb.AppendLine($"echo Copying files from: \"{sourceDir}\"");
        sb.AppendLine($"echo To: \"{targetDir}\"");
        sb.AppendLine("");
        sb.AppendLine($"xcopy /Y /E /Q /I \"{sourceDir}\\*\" \"{targetDir}\\\" >nul");
        sb.AppendLine("");
        sb.AppendLine("echo Starting new version...");
        sb.AppendLine($"start \"\" \"{exePath}\"");
        sb.AppendLine("");
        sb.AppendLine("timeout /t 2 /nobreak >nul");
        sb.AppendLine("");
        sb.AppendLine("echo Cleaning up...");
        sb.AppendLine($"rmdir /S /Q \"{sourceDir}\" >nul 2>&1");
        if (!string.IsNullOrEmpty(zipPath))
            sb.AppendLine($"del /F /Q \"{zipPath}\" >nul 2>&1");
        sb.AppendLine("");
        sb.AppendLine("del /F /Q \"%~f0\"");
        sb.AppendLine("");

        return sb.ToString();
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