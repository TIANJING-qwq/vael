using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Services;

namespace SBtools.Views;

public partial class UpdateView : UserControl
{
    public UpdateView()
    {
        InitializeComponent();
        RefreshInfo();
        Loaded += (_, _) => RefreshInfo();
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

    /// <summary>各版本更新说明（新增版本往这里加一行）</summary>
    public static string ChangelogFor(string _) =>
        """
        0.5.3「曦月」
        • 新增：实验室 → 更新 选项卡（版本号 / 代号 / 检查更新 / 强制测试更新）
        • 新增：CCTV 定时播放支持按周几重复（全不选 = 每天）
        • 修复：已是最新版本仍触发下载的问题（版本守卫）
        • 安全：Windows 更新只走安装包静默升级，下载位置改 %TEMP% 固定文件名

        0.5.2
        • Windows 更新改用 Inno Setup 安装包静默升级
        • CI 新增安装包产物；程序集元数据补全

        0.5.1
        • 关于页隐藏彩蛋（连点 Logo）
        """;

    private async void CheckUpdateButton_Click(object? sender, RoutedEventArgs e)
        => await RunUpdateAsync(false);

    private async void ForceUpdateButton_Click(object? sender, RoutedEventArgs e)
        => await RunUpdateAsync(true);

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
