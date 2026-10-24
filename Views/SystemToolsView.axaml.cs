using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class SystemToolsView : UserControl
{
    public SystemToolsView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Log("系统工具已就绪");
    }

    // ============================================================
    // 通用
    // ============================================================
    private static bool IsAdmin()
    {
        try
        {
            using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
            var principal = new System.Security.Principal.WindowsPrincipal(identity);
            return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
        }
        catch { return false; }
    }

    /// <summary>运行外部命令并读取输出（续体不回到 UI 线程，避免死锁）。</summary>
    private static async Task<(int code, string output)> RunAsync(string fileName, string arguments)
    {
        var psi = new ProcessStartInfo(fileName, arguments)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        try
        {
            using var p = Process.Start(psi);
            if (p == null) return (-1, "");
            var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await p.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await p.WaitForExitAsync().ConfigureAwait(false);
            return (p.ExitCode, output + error);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private void Log(string msg)
    {
        LogService.Log(msg, "系统工具");
        Dispatcher.UIThread.Post(() =>
        {
            if (LogPanel == null) return;
            LogPanel.Children.Add(new TextBlock
            {
                Text = $"[{DateTime.Now:HH:mm:ss}] {msg}",
                FontSize = 11,
                Foreground = Avalonia.Media.Brushes.LightGray,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
            });
            while (LogPanel.Children.Count > 100)
                LogPanel.Children.RemoveAt(0);
        });
    }

    /// <summary>先找已安装路径；找不到则自动下载主程序并运行；再失败就打开官方下载页。</summary>
    private async void LaunchToolAsync(string name, string[] exePaths, string downloadUrl, string pageUrl, string fileName)
    {
        foreach (var path in exePaths)
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    Log($"已启动 {name}");
                    return;
                }
            }
            catch { }
        }

        if (await DownloadAndRunAsync(name, downloadUrl, fileName))
            return;

        try
        {
            Process.Start(new ProcessStartInfo(pageUrl) { UseShellExecute = true });
            Log($"{name} 自动下载失败，已打开官方下载页");
        }
        catch (Exception ex)
        {
            Log($"打开下载页失败: {ex.Message}");
        }
    }

    private async Task<bool> DownloadAndRunAsync(string name, string url, string fileName)
    {
        try
        {
            var dir = Path.Combine(ConfigManager.AppDataDir, "tools");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, fileName);

            Log($"正在自动下载 {name} …");
            using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("Vael/1.0");
            var bytes = await http.GetByteArrayAsync(url).ConfigureAwait(false);
            if (bytes.Length < 1024) throw new Exception("下载内容过小，可能不是有效程序");

            // ★ 检测是否为 HTML（下载页/反爬页面），避免把网页当 ZIP 解压
            var head = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 512)).TrimStart();
            if (head.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase) ||
                head.StartsWith("<html", StringComparison.OrdinalIgnoreCase) ||
                head.StartsWith("<", StringComparison.Ordinal) && head.Contains("<script", StringComparison.OrdinalIgnoreCase))
                throw new Exception("下载到的是网页而非文件（下载页/反爬），请用官方下载页");

            await File.WriteAllBytesAsync(path, bytes).ConfigureAwait(false);
            Log($"{name} 下载完成：{path}");

            // ★ 若为 ZIP：先校验 ZIP 魔数（PK），再解压
            string launchPath = path;
            if (path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                if (bytes.Length < 4 || bytes[0] != 0x50 || bytes[1] != 0x4B)
                    throw new Exception("下载内容不是有效的 ZIP 文件");

                var extractDir = Path.Combine(dir, Path.GetFileNameWithoutExtension(fileName));
                try
                {
                    if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                }
                catch { }
                Directory.CreateDirectory(extractDir);

                Log($"正在解压 {fileName} …");
                ZipFile.ExtractToDirectory(path, extractDir, true);

                var exe = Directory.GetFiles(extractDir, "*.exe", SearchOption.AllDirectories)
                    .FirstOrDefault();
                if (exe == null) throw new Exception("解压后未找到可执行文件");
                launchPath = exe;
                Log($"已解压到 {extractDir}，准备运行 {Path.GetFileName(exe)}");
            }

            Process.Start(new ProcessStartInfo(launchPath) { UseShellExecute = true });
            return true;
        }
        catch (Exception ex)
        {
            Log($"{name} 自动下载失败: {ex.Message}");
            return false;
        }
    }

    // ============================================================
    // 系统修复
    // ============================================================
    private void Launch360_Click(object? sender, RoutedEventArgs e)
        => LaunchToolAsync(
            "360 系统急救箱",
            new[]
            {
                @"C:\Program Files (x86)\360\360Safe\safemon\360SysRepair.exe",
                @"C:\Program Files\360\360Safe\safemon\360SysRepair.exe",
            },
            "https://down.360safe.com/360SysRepairSetup.exe",
            "https://weishi.360.cn/jijiuxiang/index.html",
            "360SysRepairSetup.exe");

    private void LaunchHuorong_Click(object? sender, RoutedEventArgs e)
        => LaunchToolAsync(
            "火绒安全",
            new[]
            {
                @"C:\Program Files\Huorong\Sysdiag\bin\HipsMain.exe",
                @"C:\Program Files (x86)\Huorong\Sysdiag\bin\HipsMain.exe",
            },
            "https://down5.huorong.cn/hrswcksingle.exe",
            "https://www.huorong.cn/person5.html",
            "FireHuorongSetup.exe");

    private async void LaunchEasyRC_Click(object? sender, RoutedEventArgs e)
    {
        Log("正在解析 EasyRC 蓝奏云分享链接…");
        var directUrl = await ResolveLanzouAsync("https://wwasd.lanzout.com/i39Gx4ae9brg");
        if (string.IsNullOrEmpty(directUrl))
        {
            Log("蓝奏云链接解析失败，打开分享页手动下载");
            try
            {
                Process.Start(new ProcessStartInfo("https://wwasd.lanzout.com/i39Gx4ae9brg")
                {
                    UseShellExecute = true,
                });
            }
            catch { }
            return;
        }

        if (await DownloadAndRunAsync("EasyRC", directUrl, "EasyRC.zip"))
            Log("EasyRC 已下载并启动");
    }

    private async void LaunchLazyEnv_Click(object? sender, RoutedEventArgs e)
    {
        // GitHub Release：0.2.1 / Release.zip
        const string url = "https://github.com/ListerOvO1211/LazyEnv/releases/download/0.2.1/Release.zip";
        if (await DownloadAndRunAsync("LazyEnv", url, "LazyEnv.zip"))
            return;

        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/ListerOvO1211/LazyEnv")
            {
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private void LaunchObr_Click(object? sender, RoutedEventArgs e)
    {
        // OBR 无稳定直链：已装则启动，否则打开下载页
        foreach (var path in new[]
                 {
                     @"C:\Program Files\OBR\OBR.exe",
                     @"C:\OBR\OBR.exe",
                     @"C:\Program Files (x86)\OBR\OBR.exe",
                 })
        {
            try
            {
                if (File.Exists(path))
                {
                    Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
                    Log("已启动 OBR 系统备份");
                    return;
                }
            }
            catch { }
        }

        Log("OBR 无稳定直链，已打开下载页（可手动下载后把固定地址告诉我，改成自动）");
        try
        {
            Process.Start(new ProcessStartInfo("https://www.onlinedown.net/soft/20269930.htm")
            {
                UseShellExecute = true,
            });
        }
        catch { }
    }

    /// <summary>解析蓝奏云分享页，返回真实下载直链（失败返回 null）。</summary>
    private static async Task<string?> ResolveLanzouAsync(string pageUrl)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
            http.DefaultRequestHeaders.Referrer = new Uri(pageUrl);

            var html = await http.GetStringAsync(pageUrl).ConfigureAwait(false);

            // 蓝奏云页面里的 sign 字段（多版本兼容）
            string sign = "";
            foreach (var pattern in new[]
                     {
                         @"data\s*:\s*'([^']+)'",
                         @"'sign'\s*:\s*'([0-9a-zA-Z]+)'",
                         @"ajaxdata\s*=\s*'([^']+)'",
                         @"sign\s*:\s*'([0-9a-zA-Z]+)'",
                     })
            {
                var m = Regex.Match(html, pattern);
                if (m.Success) { sign = m.Groups[1].Value; break; }
            }
            if (string.IsNullOrEmpty(sign)) return null;

            var uri = new Uri(pageUrl);
            var ajaxUrl = $"{uri.Scheme}://{uri.Host}/ajaxm.php";

            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["action"] = "downprocess",
                ["sign"] = sign,
                ["kd"] = "1",
                ["ves"] = "1",
            });

            var resp = await http.PostAsync(ajaxUrl, form).ConfigureAwait(false);
            var json = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);

            var dom = Regex.Match(json, @"""dom""\s*:\s*""([^""]+)""").Groups[1].Value;
            var url = Regex.Match(json, @"""url""\s*:\s*""([^""]+)""").Groups[1].Value;
            if (string.IsNullOrEmpty(dom) || string.IsNullOrEmpty(url)) return null;

            return dom + url;
        }
        catch
        {
            return null;
        }
    }

    private void WindowsRepair_Click(object? sender, RoutedEventArgs e)
    {
        if (!PlatformInfo.CanUseSystemRepair)
        {
            Log("该功能仅支持 Windows");
            return;
        }

        if (!IsAdmin())
        {
            Log("Windows 修复需要管理员权限，请右键程序 → 以管理员身份运行");
            return;
        }

        Log("启动 Windows 修复：sfc /scannow 然后 DISM /RestoreHealth（新窗口执行）");
        try
        {
            Process.Start(new ProcessStartInfo(
                "cmd.exe",
                "/k echo ===== SFC 系统文件检查 ===== & sfc /scannow & echo. & echo ===== DISM 组件修复 ===== & DISM /Online /Cleanup-Image /RestoreHealth & echo. & echo 全部完成，按任意键关闭 & pause")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log($"启动修复失败: {ex.Message}");
        }
    }

    // ============================================================
    // Windows 更新管理
    // ============================================================
    private async void RefreshUpdates_Click(object? sender, RoutedEventArgs e)
    {
        if (!PlatformInfo.CanUseSystemRepair)
        {
            UpdateStatusText.Text = "该功能仅支持 Windows";
            return;
        }

        UpdateStatusText.Text = "正在读取已安装更新...";
        var (_, output) = await RunAsync(
            "powershell",
            "-NoProfile -Command \"Get-HotFix | Sort-Object InstalledOn -Descending | ForEach-Object { $_.HotFixID + ' | ' + $_.Description + ' | ' + $_.InstalledOn }\"");

        UpdateListBox.Items.Clear();
        int n = 0;
        foreach (var line in output.Split('\n'))
        {
            var t = line.Trim();
            if (t.Length == 0 || t.StartsWith("HotFixID", StringComparison.OrdinalIgnoreCase)) continue;
            UpdateListBox.Items.Add(t);
            n++;
        }
        UpdateStatusText.Text = n > 0 ? $"共 {n} 个已安装更新，选中后点「卸载选中更新」" : "未读取到更新（可能需要管理员权限）";
        Log($"已加载 {n} 个 Windows 更新");
    }

    private void UninstallSelectedUpdate_Click(object? sender, RoutedEventArgs e)
    {
        if (UpdateListBox.SelectedItem is not string selected)
        {
            UpdateStatusText.Text = "请先在列表里选中一个更新";
            return;
        }

        var m = Regex.Match(selected, @"KB\d+", RegexOptions.IgnoreCase);
        if (!m.Success)
        {
            UpdateStatusText.Text = "无法从该项解析出 KB 编号";
            return;
        }

        var kb = m.Value.ToUpperInvariant().Replace("KB", "");
        if (!IsAdmin())
        {
            UpdateStatusText.Text = "卸载更新需要管理员权限";
            Log("卸载更新需要管理员权限");
            return;
        }

        Log($"正在卸载 {m.Value}（新窗口执行）");
        try
        {
            Process.Start(new ProcessStartInfo(
                "cmd.exe",
                $"/k wusa /uninstall /kb:{kb} /quiet /norestart & echo. & echo 卸载命令已执行，按任意键关闭 & pause")
            {
                UseShellExecute = true,
            });
            UpdateStatusText.Text = $"已发起卸载 {m.Value}（可能需要重启）";
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"卸载失败: {ex.Message}";
        }
    }
}
