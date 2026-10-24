using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Diagnostics;
using System.IO;

namespace SBtools.Views;

public partial class WindowsProblemsView : UserControl
{
    public WindowsProblemsView()
    {
        InitializeComponent();
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

    private void Log(string msg)
    {
        LogService.Log(msg, "常见问题");
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

    private static void OpenUri(string uri)
    {
        try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); }
        catch { }
    }

    // ============================================================
    // ① 资源管理器无限重启
    // ============================================================
    private void OpenControlPanel_Click(object? sender, RoutedEventArgs e)
    {
        Log("打开控制面板（时钟和区域 → 更改时区 → 确定即可）");
        OpenUri("control.exe");
    }

    private void RestartExplorer_Click(object? sender, RoutedEventArgs e)
    {
        Log("正在重启 Windows 资源管理器…");
        try
        {
            Process.Start(new ProcessStartInfo(
                "cmd.exe",
                "/c taskkill /f /im explorer.exe & start explorer.exe")
            {
                UseShellExecute = true,
                CreateNoWindow = true,
            });
        }
        catch (Exception ex)
        {
            Log($"重启资源管理器失败: {ex.Message}");
        }
    }

    // ============================================================
    // ② 系统更新后闪屏
    // ============================================================
    private void ListUpdates_Click(object? sender, RoutedEventArgs e)
    {
        Log("正在列出已安装更新（供卸载参考）…");
        try
        {
            Process.Start(new ProcessStartInfo(
                "cmd.exe",
                "/k powershell -NoProfile -Command \"Get-HotFix | Sort-Object InstalledOn -Descending | Format-Table HotFixID,Description,InstalledOn -AutoSize & pause\"")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log($"列出更新失败: {ex.Message}");
        }
    }

    private void OpenUpdateHistory_Click(object? sender, RoutedEventArgs e)
    {
        Log("打开 Windows 更新历史");
        OpenUri("ms-settings:windowsupdate-history");
    }

    // ============================================================
    // ④ 网络重置
    // ============================================================
    private void ResetNetwork_Click(object? sender, RoutedEventArgs e)
    {
        if (!IsAdmin())
        {
            Log("网络重置需要管理员权限，请右键程序 → 以管理员身份运行");
            return;
        }

        Log("执行 netsh winsock reset（完成后需重启电脑）");
        try
        {
            Process.Start(new ProcessStartInfo("cmd.exe", "/k netsh winsock reset & pause")
            {
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Log($"网络重置失败: {ex.Message}");
        }
    }

    // ============================================================
    // ⑤ 图标缓存
    // ============================================================
    private void RebuildIconCache_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var iconCache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IconCache.db");

            if (File.Exists(iconCache))
            {
                File.Delete(iconCache);
                Log("已删除 IconCache.db");
            }
            else
            {
                Log("未找到 IconCache.db（可能已不存在）");
            }

            Process.Start(new ProcessStartInfo(
                "cmd.exe",
                "/c taskkill /f /im explorer.exe & start explorer.exe")
            {
                UseShellExecute = true,
                CreateNoWindow = true,
            });
            Log("正在重启资源管理器以重建图标缓存");
        }
        catch (Exception ex)
        {
            Log($"重建图标缓存失败: {ex.Message}");
        }
    }

    // ============================================================
    // ⑥ 系统文件修复
    // ============================================================
    private void RepairSystemFiles_Click(object? sender, RoutedEventArgs e)
    {
        if (!IsAdmin())
        {
            Log("系统文件修复需要管理员权限，请右键程序 → 以管理员身份运行");
            return;
        }

        Log("启动 SFC + DISM 修复（新窗口执行）");
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
}
