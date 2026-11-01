using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Media;
using SBtools.Services;

namespace SBtools.Views;

public partial class UpdateView : UserControl
{
    public UpdateView()
    {
        InitializeComponent();
        RefreshInfo();
        Loaded += (_, _) => { RefreshInfo(); _ = LoadChangelogAsync(); };
    }

    private void RefreshInfo()
    {
        try
        {
            var ver = Assembly.GetExecutingAssembly().GetName().Version;
            VerRun.Text = $"{ver?.Major ?? 0}.{ver?.Minor ?? 0}.{ver?.Build ?? 0}";
            CodenameRun.Text = VersionCodename;
        }
        catch (Exception ex)
        {
            LogService.Log($"更新页加载失败: {ex.Message}", "更新");
        }
    }

    /// <summary>版本代号（全球地名 / 小米机型代号体系）</summary>
    public static string VersionCodename => "Umi";

    // ============================================================
    // 版本日志：跟随 GitHub Releases，离线回退内置
    // ============================================================

    private bool _changelogLoaded;

    private async System.Threading.Tasks.Task LoadChangelogAsync()
    {
        if (_changelogLoaded) return;
        _changelogLoaded = true;
        RenderChangelog(FallbackChangelog, online: false);
        try
        {
            var online = await UpdateService.Instance.GetOnlineChangelogAsync();
            if (online.Count > 0)
                RenderChangelog(online, online: true);
        }
        catch { /* 保持内置 */ }
    }

    /// <summary>渲染时间线（首条蓝点，其余灰点）</summary>
    private void RenderChangelog(List<(string Tag, string Body)> entries, bool online)
    {
        ChangelogPanel.Children.Clear();
        for (int i = 0; i < entries.Count; i++)
        {
            var (tag, body) = entries[i];
            var header = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 10 };
            header.Children.Add(new Ellipse
            {
                Width = 8, Height = 8, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Fill = new SolidColorBrush(i == 0
                    ? Avalonia.Media.Color.Parse("#0078D4")
                    : Avalonia.Media.Color.Parse("#999999")),
            });
            header.Children.Add(new TextBlock
            {
                Text = "v" + tag, FontSize = 15, FontWeight = FontWeight.SemiBold,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            });
            if (i == 0)
                header.Children.Add(new TextBlock
                {
                    Text = online ? "GitHub" : "内置",
                    FontSize = 11, Opacity = 0.5,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                });

            var item = new StackPanel { Spacing = 4 };
            item.Children.Add(header);
            if (!string.IsNullOrWhiteSpace(body))
                item.Children.Add(new TextBlock
                {
                    Text = body, FontSize = 13, Opacity = 0.75,
                    Margin = new Avalonia.Thickness(18, 0, 0, 0),
                    TextWrapping = TextWrapping.Wrap,
                });
            ChangelogPanel.Children.Add(item);
        }
    }

    /// <summary>离线回退：内置版本日志（在线获取失败时显示）</summary>
    private static List<(string Tag, string Body)> FallbackChangelog { get; } = new()
    {
        ("0.5.7", """
            • 强制测试更新重做：无视版本比较直拉最新安装包，完整走升级流程
            • 修复启动崩溃与示例插件页重复注册
            • 更新页徽章改为测试版
            """),
        ("0.5.6", """
            • 插件系统全面开放：可创建侧边栏选项卡、注册小组件卡片、弹独立窗口
            • 插件页一键启用/禁用（立即生效并持久化，UI 同步增删）
            • 内置示例插件演示全部 API；插件文档 2.0 重写
            • 全新开屏动画
            """),
        ("0.5.5", """
            • 小组件全面进化：统一窗体 + 会话记忆（位置/置顶/参数）
            • 抛硬币上抛旋转动画、点名/抽号老虎机减速动画
            • 开关窗非线性淡入淡出；涩图历史回退（中键/按钮）
            • 云母/亚克力材质与主界面同源，下拉实时切换
            • 内存 60 秒曲线 · 网速自动换单位 · 按键式计算器
            """),
        ("0.5.4", """
            • 小组件管理页：16 项自实现桌面小组件
            • 点击开启 / 再次点击关闭 / 右键详细配置
            • 常见问题 115 条 · 优化大全 114 项
            """),
        ("0.5.2", """
            • Windows 更新改用安装包静默升级
            • CI 产出安装包；程序集元数据补全
            """),
        ("0.5.1", """
            • 关于页隐藏彩蛋
            • 下拉框非线性动画
            """),
    };

    private async void CheckUpdateButton_Click(object? sender, RoutedEventArgs e)
        => await RunUpdateAsync(false);

    private async void ForceUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        // ★ 强制测试更新：无视版本比较，直接下载最新 Release 安装包并静默安装
        StatusText.Text = "强制测试更新：获取最新版本…";
        CheckUpdateButton.IsEnabled = ForceUpdateButton.IsEnabled = false;
        void OnProgress(int p) => StatusText.Text = $"强制下载最新版… {p}%";
        UpdateService.Instance.DownloadProgressChanged += OnProgress;
        try
        {
            var ok = await UpdateService.Instance.ForceLatestSetupAsync();
            if (!ok)
                StatusText.Text = $"强制更新失败：{UpdateService.Instance.LastError ?? "未知错误"}";
            else
                StatusText.Text = "安装包已启动，程序即将退出完成升级…";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"强制更新失败: {ex.Message}";
            LogService.Log($"强制测试更新异常: {ex.Message}", "更新");
        }
        finally
        {
            UpdateService.Instance.DownloadProgressChanged -= OnProgress;
            CheckUpdateButton.IsEnabled = ForceUpdateButton.IsEnabled = true;
        }
    }

    private async System.Threading.Tasks.Task RunUpdateAsync(bool force)
    {
        try
        {
            StatusText.Text = force ? "强制测试更新：检查中…" : "检查更新中…";
            CheckUpdateButton.IsEnabled = ForceUpdateButton.IsEnabled = false;

            var has = await UpdateService.Instance.CheckForUpdatesAsync(force);
            if (!has)
            {
                StatusText.Text = force
                    ? "强制测试：远端 Release 可用但无更新动作，或无资产"
                    : "当前已是最新版本";
                return;
            }

            StatusText.Text = $"发现新版本 {UpdateService.Instance.LatestVersion}，开始下载…";
            MainWindow.InstallWithProgressAsync();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"更新失败: {ex.Message}";
            LogService.Log($"更新页更新失败: {ex.Message}", "更新");
        }
        finally
        {
            CheckUpdateButton.IsEnabled = ForceUpdateButton.IsEnabled = true;
        }
    }
}
