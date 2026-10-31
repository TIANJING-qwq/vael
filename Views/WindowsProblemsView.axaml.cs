using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace SBtools.Views;

public partial class WindowsProblemsView : UserControl
{
    private readonly ObservableCollection<WindowsProblem> _filtered = new();
    private readonly List<Button> _catCards = new();
    private string? _selectedCat;

    public WindowsProblemsView()
    {
        InitializeComponent();

        BuildCategoryCards();
        ProblemList.ItemsSource = _filtered;
        ApplyFilter();
    }

    // ============================================================
    // 分类方块卡片
    // ============================================================
    private void BuildCategoryCards()
    {
        _catCards.Clear();
        CategoryCardsHost.Children.Clear();

        AddCategoryCard(null, "全部", "📋");
        foreach (var c in WindowsProblemCatalog.Categories)
        {
            var count = WindowsProblemCatalog.All.Count(p => p.Category == c);
            AddCategoryCard(c, $"{c}  {count}", null);
        }
        UpdateCardStyles();
    }

    private void AddCategoryCard(string? cat, string label, string? icon)
    {
        var text = icon == null ? label : $"{icon}  {label}";
        var card = new Button
        {
            Content = new TextBlock { Text = text, FontSize = 12.5 },
            Tag = cat,
            Padding = new Avalonia.Thickness(14, 8),
            Margin = new Avalonia.Thickness(0, 0, 10, 10),
            MinWidth = 110,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            CornerRadius = new Avalonia.CornerRadius(8),
            Background = Avalonia.Media.Brush.Parse("#22FFFFFF"),
            BorderThickness = new Avalonia.Thickness(1),
            BorderBrush = Avalonia.Media.Brush.Parse("#33FFFFFF"),
            Foreground = Avalonia.Media.Brushes.White,
        };
        card.Click += CategoryCard_Click;
        _catCards.Add(card);
        CategoryCardsHost.Children.Add(card);
    }

    private void CategoryCard_Click(object? sender, RoutedEventArgs e)
    {
        _selectedCat = (sender as Button)?.Tag as string;
        UpdateCardStyles();
        ApplyFilter();
    }

    private void UpdateCardStyles()
    {
        // 按当前主题取中性底色（浅色下用半透明黑，深色下用半透明白），两种模式都可见
        bool dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        foreach (var card in _catCards)
        {
            bool selected = (card.Tag as string) == _selectedCat;
            card.Background = selected
                ? Avalonia.Media.Brush.Parse("#0078D4")
                : Avalonia.Media.Brush.Parse(dark ? "#22FFFFFF" : "#14000000");
            card.BorderBrush = selected
                ? Avalonia.Media.Brush.Parse("#0078D4")
                : Avalonia.Media.Brush.Parse(dark ? "#33FFFFFF" : "#29000000");
        }
    }

    // ============================================================
    // 过滤 / 搜索
    // ============================================================
    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var kw = (SearchBox.Text ?? "").Trim();

        var list = WindowsProblemCatalog.All.Where(p =>
            (_selectedCat == null || p.Category == _selectedCat) &&
            (kw.Length == 0 ||
             p.Title.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
             p.Symptom.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
             p.Manual.Contains(kw, StringComparison.OrdinalIgnoreCase)));

        _filtered.Clear();
        foreach (var p in list)
            _filtered.Add(p);

        CountText.Text = _selectedCat == null
            ? $"共 {_filtered.Count} 个"
            : $"{_selectedCat} · {_filtered.Count} 个";
        EmptyHint.IsVisible = _filtered.Count == 0;
    }

    // ============================================================
    // 自动动作执行
    // ============================================================
    private void AutoActionButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ProblemAction a })
            Execute(a);
    }

    private void Execute(ProblemAction a)
    {
        Log($"执行：{a.Label}");
        try
        {
            bool adminOnly = a.Kind is ProblemActionKind.RunSfcDism
                or ProblemActionKind.NetshWinsockReset or ProblemActionKind.NetshIpReset
                or ProblemActionKind.CheckDisk or ProblemActionKind.ResetWmi
                or ProblemActionKind.PowerHibernationOff or ProblemActionKind.PowerFastStartupOff
                or ProblemActionKind.DisableAutoReboot or ProblemActionKind.RestartAudio;

            if (adminOnly && !IsAdmin())
            {
                Log("✗ 需要管理员权限，请以管理员身份运行 Vael 后重试");
                return;
            }

            switch (a.Kind)
            {
                case ProblemActionKind.RestartExplorer:
                    RunCmd($"/c {S.TKill} /f /im explorer.exe & start explorer.exe", visible: false);
                    Log("✓ 资源管理器已重启");
                    break;

                case ProblemActionKind.OpenControlPanel:
                    StartShell("control.exe");
                    break;

                case ProblemActionKind.OpenDateAndTime:
                    StartShell("control.exe", "/name Microsoft.DateAndTime");
                    break;

                case ProblemActionKind.RunSfcDism:
                    RunVisibleCmd(@"/k echo ===== SFC 扫描 ===== & sfc /scannow & echo ===== DISM 修复 ===== & DISM /Online /Cleanup-Image /RestoreHealth & echo ===== 完成，可关闭本窗口 ===== & pause");
                    Log("已打开修复窗口（耗时约 10-30 分钟，请勿中途关闭）");
                    break;

                case ProblemActionKind.NetshWinsockReset:
                    RunVisibleCmd("/k netsh winsock reset & echo 完成，请重启电脑生效 & pause");
                    break;

                case ProblemActionKind.NetshIpReset:
                    RunVisibleCmd("/k netsh int ip reset & echo 完成，请重启电脑生效 & pause");
                    break;

                case ProblemActionKind.FlushDns:
                    RunVisibleCmd("/k ipconfig /flushdns & pause");
                    break;

                case ProblemActionKind.RenewIp:
                    RunVisibleCmd("/k ipconfig /release & ipconfig /renew & pause");
                    break;

                case ProblemActionKind.RebuildIconCache:
                    RunCmd($"/c {S.TKill} /f /im explorer.exe & del /a /q /f \"%LocalAppData%\\IconCache.db\" & start explorer.exe", visible: false);
                    Log("✓ 图标缓存已重建（若图标仍异常请注销重登）");
                    break;

                case ProblemActionKind.ThumbnailCache:
                    RunCmd($"/c {S.TKill} /f /im explorer.exe & del /a /q /f \"%LocalAppData%\\Microsoft\\Windows\\Explorer\\thumbcache_*.db\" & start explorer.exe", visible: false);
                    Log("✓ 缩略图缓存已清理");
                    break;

                case ProblemActionKind.OpenMsSettings:
                    if (!string.IsNullOrEmpty(a.Arg)) StartShell("ms-settings:" + a.Arg);
                    break;

                case ProblemActionKind.RunCommand:
                    if (!string.IsNullOrEmpty(a.Arg)) RunVisibleCmd("/k " + a.Arg);
                    break;

                case ProblemActionKind.OpenUrl:
                    if (!string.IsNullOrEmpty(a.Arg)) StartShell(a.Arg);
                    break;

                case ProblemActionKind.OpenFolder:
                    if (!string.IsNullOrEmpty(a.Arg)) StartShell("explorer.exe", Expand(a.Arg));
                    break;

                case ProblemActionKind.ResetStoreCache:
                    StartShell("wsreset.exe");
                    Log("已重置应用商店缓存（稍后会自动打开商店）");
                    break;

                case ProblemActionKind.CheckDisk:
                    RunVisibleCmd("/k chkdsk C: /f & pause");
                    Log("已打开磁盘检查（提示时输入 Y 并回车，重启后执行）");
                    break;

                case ProblemActionKind.RestartAudio:
                    RunVisibleCmd("/k net stop audiosrv & net stop AudioEndpointBuilder & net start audiosrv & net start AudioEndpointBuilder & pause");
                    break;

                case ProblemActionKind.ResetWmi:
                    RunVisibleCmd("/k winmgmt /verifyrepository & winmgmt /salvagerepository & pause");
                    break;

                case ProblemActionKind.PowerHibernationOff:
                    RunVisibleCmd("/k powercfg /h off & echo 已关闭休眠 & pause");
                    break;

                case ProblemActionKind.PowerFastStartupOff:
                    RunVisibleCmd("/k reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Control\\Session Manager\\Power\" /v HiberbootEnabled /t REG_DWORD /d 0 /f & echo 已关闭快速启动 & pause");
                    break;

                case ProblemActionKind.OpenUpdateHistory:
                    StartShell("ms-settings:windowsupdate-history");
                    break;

                case ProblemActionKind.OpenTroubleshoot:
                    StartShell("ms-settings:troubleshoot");
                    break;

                case ProblemActionKind.DisableAutoReboot:
                    RunVisibleCmd("/k wmic recoveros set AutoReboot=False & echo 已关闭蓝屏自动重启 & pause");
                    break;
            }
        }
        catch (Exception ex)
        {
            Log("✗ 执行失败: " + ex.Message);
        }
    }

    // ============================================================
    // 进程 / 通用
    // ============================================================
    private static void RunVisibleCmd(string args) => RunCmd(args, visible: true);

    private static void RunCmd(string args, bool visible)
    {
        var psi = new ProcessStartInfo("cmd.exe", args)
        {
            UseShellExecute = true,
        };
        if (!visible)
        {
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.WindowStyle = ProcessWindowStyle.Hidden;
        }
        Process.Start(psi);
    }

    private static void StartShell(string file, string? args = null)
    {
        var psi = new ProcessStartInfo(file)
        {
            UseShellExecute = true,
        };
        if (!string.IsNullOrEmpty(args)) psi.Arguments = args;
        Process.Start(psi);
    }

    private static string Expand(string path) => Environment.ExpandEnvironmentVariables(path);

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
}
