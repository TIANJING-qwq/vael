using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class BackupView : UserControl
{
    public BackupView()
    {
        InitializeComponent();
        PopulateDrives();
    }

    // ============================================================
    // 盘符
    // ============================================================
    private void PopulateDrives()
    {
        try
        {
            if (TargetDriveCombo == null) return;
            TargetDriveCombo.Items.Clear();

            var systemRoot = Path.GetPathRoot(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)) ?? "";

            var list = new List<string>();
            foreach (var d in DriveInfo.GetDrives())
            {
                try
                {
                    // 先判断 DriveType（快），避免对不可用网络盘访问 IsReady 卡住
                    if (d.DriveType != DriveType.Fixed) continue;
                    if (!d.IsReady) continue;
                    if (d.RootDirectory.FullName.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
                        continue; // 跳过系统盘
                    list.Add(d.RootDirectory.FullName);
                }
                catch { }
            }

            foreach (var p in list) TargetDriveCombo.Items.Add(p);
            if (TargetDriveCombo.Items.Count > 0)
                TargetDriveCombo.SelectedIndex = 0;
        }
        catch { }
    }

    private void RefreshDrives_Click(object? sender, RoutedEventArgs e)
    {
        PopulateDrives();
        Log("已刷新盘符列表");
    }

    // ============================================================
    // 备份
    // ============================================================
    private async void StartBackup_Click(object? sender, RoutedEventArgs e)
    {
        var drive = TargetDriveCombo?.SelectedItem as string;
        if (string.IsNullOrEmpty(drive))
        {
            StatusText.Text = "请先选择备份目标盘符";
            return;
        }

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var folders = new List<(string Name, string Path)>();
        if (DesktopCheck.IsChecked == true) folders.Add(("桌面", Path.Combine(userProfile, "Desktop")));
        if (DocumentsCheck.IsChecked == true) folders.Add(("文档", Path.Combine(userProfile, "Documents")));
        if (DownloadsCheck.IsChecked == true) folders.Add(("下载", Path.Combine(userProfile, "Downloads")));
        if (PicturesCheck.IsChecked == true) folders.Add(("图片", Path.Combine(userProfile, "Pictures")));
        if (VideosCheck.IsChecked == true) folders.Add(("视频", Path.Combine(userProfile, "Videos")));
        if (MusicCheck.IsChecked == true) folders.Add(("音乐", Path.Combine(userProfile, "Music")));

        if (folders.Count == 0)
        {
            StatusText.Text = "请至少勾选一个要备份的文件夹";
            return;
        }

        var targetRoot = Path.Combine(drive, "VaelBackup");
        StatusText.Text = $"正在备份到 {targetRoot} …";
        Log($"开始备份，共 {folders.Count} 个文件夹 → {targetRoot}");

        int ok = 0, fail = 0;
        foreach (var (name, src) in folders)
        {
            if (!Directory.Exists(src))
            {
                Log($"跳过（不存在）：{name} ({src})");
                continue;
            }

            var dst = Path.Combine(targetRoot, name);
            Log($"正在备份 {name} …");
            try
            {
                var (code, output) = await CopyFolderAsync(src, dst);
                // robocopy: 0-7 为成功，>=8 为失败；托管复制成功返回 0
                if (code < 8) { ok++; Log($"{name} 完成（退出码 {code}）"); }
                else { fail++; Log($"{name} 失败（退出码 {code}）:{Truncate(output)}"); }
            }
            catch (Exception ex)
            {
                fail++;
                Log($"{name} 异常: {ex.Message}");
            }
        }

        StatusText.Text = $"备份完成：成功 {ok} 个，失败 {fail} 个 → {targetRoot}";
        Log(StatusText.Text);
    }

    /// <summary>跨平台复制：Windows 用 robocopy，其它平台用托管递归复制。</summary>
    private static async Task<(int code, string output)> CopyFolderAsync(string src, string dst)
    {
        if (OperatingSystem.IsWindows())
        {
            var psi = new ProcessStartInfo(
                "robocopy",
                $"\"{src}\" \"{dst}\" /E /R:1 /W:1 /NFL /NDL /NP /NJH")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var p = Process.Start(psi);
            if (p == null) return (-1, "");
            var output = await p.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            var error = await p.StandardError.ReadToEndAsync().ConfigureAwait(false);
            await p.WaitForExitAsync().ConfigureAwait(false);
            return (p.ExitCode, output + error);
        }

        try
        {
            await Task.Run(() => CopyDirectory(src, dst)).ConfigureAwait(false);
            return (0, "");
        }
        catch (Exception ex)
        {
            return (8, ex.Message);
        }
    }

    private static void CopyDirectory(string src, string dst)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(src, file);
            var target = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private void OpenBackupDir_Click(object? sender, RoutedEventArgs e)
    {
        var drive = TargetDriveCombo?.SelectedItem as string;
        if (string.IsNullOrEmpty(drive))
        {
            StatusText.Text = "请先选择目标盘符";
            return;
        }

        var target = Path.Combine(drive, "VaelBackup");
        try
        {
            if (!Directory.Exists(target))
            {
                StatusText.Text = "备份目录还不存在，请先执行一次备份";
                return;
            }
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log($"打开备份目录失败: {ex.Message}");
        }
    }

    // ============================================================
    // 日志
    // ============================================================
    private static string Truncate(string s, int max = 200)
        => string.IsNullOrEmpty(s) ? "" : (s.Length <= max ? s : s[..max] + "…");

    private void Log(string msg)
    {
        LogService.Log(msg, "备份");
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
}
