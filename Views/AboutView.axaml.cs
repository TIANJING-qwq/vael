using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Media.Transformation;
using Avalonia.Platform;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class AboutView : UserControl
{
    private const string GitHubUrl = "https://github.com/TIANJING-qwq/Vael";

    private readonly List<Control> _fadeTargets = new();

    private readonly Dictionary<string, double> _targetOpacity = new()
    {
        ["AppIcon"]           = 1.0,
        ["ChineseName"]       = 0.85,
        ["Divider"]           = 1.0,
        ["LineAuthor"]        = 1.0,
        ["LineTeam"]          = 0.6,
        ["LineBuild"]         = 0.7,
        ["LineVersion"]       = 0.5,
        ["LineDesc"]          = 0.6,
        ["CreditsSection"]    = 1.0,
        ["ButtonRow"]         = 1.0,
        ["UpdateStatusText"]  = 0.6,
    };

    private bool _played;
    private bool _isCheckingUpdate;

    private static string DisplayVersion
    {
        get
        {
            try
            {
                var v = typeof(AboutView).Assembly.GetName().Version;
                return v != null ? $"Dev{v.Major}.{v.Minor}.{v.Build}" : "Dev0.0.0";
            }
            catch { return "Dev0.0.0"; }
        }
    }

    public AboutView()
    {
        InitializeComponent();

        // ★ 加载制作人员头像（容错，失败不崩）
        LoadCreditImages();

        _fadeTargets.AddRange(new Control[]
        {
            AppIcon, ChineseName, Divider,
            LineAuthor, LineTeam, LineBuild, LineVersion, LineDesc,
            CreditsSection, ButtonRow
        });

        foreach (var t in _fadeTargets)
        {
            t.Opacity = 0;
            t.RenderTransform = TransformOperations.Parse("translateY(14px)");

            t.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(500),
                    Easing = new CubicEaseOut()
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(600),
                    Easing = new QuinticEaseOut()
                }
            };
        }

        // 标题字母：初始在下方且透明，随后从左往右依次上浮
        for (int i = 0; i < 4; i++)
        {
            var letter = this.FindControl<TextBlock>($"Letter{i}");
            if (letter == null) continue;
            letter.Opacity = 0;
            letter.RenderTransform = TransformOperations.Parse("translateY(30px)");
            letter.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(450),
                    Easing = new CubicEaseOut()
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(500),
                    Easing = new QuinticEaseOut()
                }
            };
        }

        LineVersion.Text = $"版本 {DisplayVersion}";

        AttachedToVisualTree += async (_, __) =>
        {
            if (_played) return;
            _played = true;
            await PlayAnimationAsync();
        };
    }

    // ============================================================
    // 加载头像图片（多重兜底）
    // ============================================================
    private void LoadCreditImages()
    {
        try
        {
            var tj = this.FindControl<Image>("TJImage");
            if (tj != null)
            {
                var bmp = TryLoadImage("TJ.jpg", "TJ.png", "tj.jpg");
                if (bmp != null) tj.Source = bmp;
            }

            var deep = this.FindControl<Image>("DeepImage");
            if (deep != null)
            {
                var bmp = TryLoadImage("deep.jpg", "deep.png", "Deep.jpg");
                if (bmp != null) deep.Source = bmp;
            }

            var shinecy = this.FindControl<Image>("ShinecyImage");
            if (shinecy != null)
            {
                var bmp = TryLoadImage("x.jpg", "x.png", "shinecy.jpg");
                if (bmp != null) shinecy.Source = bmp;
            }
        }
        catch (Exception ex)
        {
            LogService.Log($"加载头像失败: {ex.Message}", "关于");
        }
    }

    /// <summary>
    /// 尝试多种方式加载图片：AvaloniaResource → 输出目录 → exe 同级目录。
    /// </summary>
    private static Bitmap? TryLoadImage(params string[] candidates)
    {
        foreach (var fileName in candidates)
        {
            // 1) 从 Avalonia 资源加载
            try
            {
                var uri = new Uri($"avares://SBtools/Assets/{fileName}");
                if (AssetLoader.Exists(uri))
                {
                    using var stream = AssetLoader.Open(uri);
                    return new Bitmap(stream);
                }
            }
            catch { }

            // 2) 从 exe 输出目录的 Assets 文件夹加载
            try
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", fileName);
                if (File.Exists(path))
                    return new Bitmap(path);
            }
            catch { }

            // 3) 从项目源码目录加载（开发时）
            try
            {
                var path = Path.Combine(
                    AppContext.BaseDirectory,
                    "..", "..", "..", "Assets", fileName);
                path = Path.GetFullPath(path);
                if (File.Exists(path))
                    return new Bitmap(path);
            }
            catch { }
        }

        return null;
    }

    // ============================================================
    // 动画
    // ============================================================
    private async Task PlayAnimationAsync()
    {
        Fade(AppIcon);
        await Task.Delay(400);

        // 标题字母从左往右依次上浮
        for (int i = 0; i < 4; i++)
        {
            var letter = this.FindControl<TextBlock>($"Letter{i}");
            if (letter == null) continue;
            letter.Opacity = 1;
            letter.RenderTransform = TransformOperations.Parse("translateY(0px)");
            await Task.Delay(90);
        }

        Fade(ChineseName);
        await Task.Delay(200);

        foreach (var target in new Control[]
                 {
                     Divider,
                     LineAuthor,
                     LineTeam,
                     LineBuild,
                     LineVersion,
                     LineDesc,
                     CreditsSection,
                     ButtonRow,
                     UpdateStatusText
                 })
        {
            Fade(target);
            await Task.Delay(140);
        }
    }

    private void Fade(Control c)
    {
        c.Opacity = GetTarget(c);
        c.RenderTransform = TransformOperations.Parse("translateY(0px)");
    }

    private double GetTarget(Control c)
    {
        if (c.Name != null && _targetOpacity.TryGetValue(c.Name, out var v))
            return v;
        return 1.0;
    }

    // ============================================================
    // 彩蛋：头像交互
    // ============================================================
    private void TianjingAvatar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            Process.Start(new ProcessStartInfo("https://github.com/TIANJING-qwq")
            {
                UseShellExecute = true,
            });
        }
        catch { }
    }

    private bool _ricePlaying;

    private async void DeepseekAvatar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_ricePlaying) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _ricePlaying = true;
        try
        {
            var deepPos = DeepImage.TranslatePoint(new Point(0, 0), EggCanvas) ?? new Point(0, 0);
            var shinecyPos = ShinecyImage.TranslatePoint(new Point(0, 0), EggCanvas) ?? new Point(0, 0);

            // 气泡：放在 deepseek 头像正上方
            Canvas.SetLeft(RiceBubble, deepPos.X + 36 - 60);
            Canvas.SetTop(RiceBubble, deepPos.Y - 44);
            RiceBubble.Opacity = 1;

            // 白饭：从 shinecy 头像飞向 deepseek 头像
            double sx = shinecyPos.X + 36 - 15;
            double sy = shinecyPos.Y + 36 - 15;
            double dx = deepPos.X + 36 - 15;
            double dy = deepPos.Y + 36 - 15;

            Canvas.SetLeft(RiceBowl, sx);
            Canvas.SetTop(RiceBowl, sy);
            RiceBowl.RenderTransform = TransformOperations.Parse("translate(0px, 0px)");
            RiceBowl.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(650),
                    Easing = new CubicEaseOut(),
                },
            };
            RiceBowl.Opacity = 1;

            await Task.Delay(60);
            RiceBowl.RenderTransform =
                TransformOperations.Parse($"translate({dx - sx}px, {dy - sy}px)");

            await Task.Delay(720);
            RiceBowl.Opacity = 0;

            await Task.Delay(1500);
            RiceBubble.Opacity = 0;
        }
        catch { }
        finally
        {
            _ricePlaying = false;
        }
    }

    // ============================================================
    // GitHub
    // ============================================================
    private void GitHubButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(GitHubUrl) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", GitHubUrl);
            else
                Process.Start("xdg-open", GitHubUrl);
        }
        catch { }
    }

    // ============================================================
    // 检查更新
    // ============================================================
    private async void CheckUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isCheckingUpdate) return;
        _isCheckingUpdate = true;

        var button = sender as Button;
        if (button != null) button.IsEnabled = false;

        try
        {
            UpdateStatusText.Text = "正在检查更新...";
            LogService.Log("用户点击检查更新", "更新");

            var hasUpdate = await UpdateService.Instance.CheckForUpdatesAsync();

            if (!hasUpdate)
            {
                UpdateStatusText.Text = $"当前已是最新版本（{DisplayVersion}）";
                return;
            }

            // ★ 手动更新：弹窗确认 → 下载（带进度）→ 安装（按安装方式设置）
            var owner = TopLevel.GetTopLevel(this) as Window;
            var version = UpdateService.Instance.LatestVersion ?? "";

            var dialog = new UpdateAvailableDialog(version, UpdateService.Instance.GetChangelog());
            if (owner == null || !await dialog.ShowDialog<bool>(owner))
            {
                UpdateStatusText.Text = "已取消更新";
                return;
            }

            UpdateStatusText.Text = "正在下载更新...";
            var progress = new UpdateDownloadDialog();
            progress.Show(owner);

            bool downloaded = await UpdateService.Instance.DownloadAsync();
            try { await progress.CloseWithAnimationAsync(); } catch { }

            if (!downloaded)
            {
                UpdateStatusText.Text = "下载失败";
                MainWindow.PushToast("更新失败", UpdateService.Instance.LastError ?? "下载失败，请稍后重试。");
                return;
            }

            if (ConfigManager.Instance.AutoInstallUpdate)
            {
                UpdateStatusText.Text = "正在安装更新...";
                await UpdateService.Instance.InstallAsync();
            }
            else
            {
                var installDialog = new UpdateAvailableDialog(
                    version, "新版本已下载完成，是否立即安装？", "安装更新", "立即安装");
                if (owner != null && await installDialog.ShowDialog<bool>(owner))
                {
                    UpdateStatusText.Text = "正在安装更新...";
                    await UpdateService.Instance.InstallAsync();
                }
                else
                {
                    UpdateStatusText.Text = "已下载，未安装";
                    MainWindow.PushToast("更新已下载", "稍后会再次提示安装。");
                }
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText.Text = $"检查更新异常: {ex.Message}";
            LogService.Log($"检查更新异常: {ex.Message}", "更新");
            MainWindow.PushToast("检查更新异常", ex.Message);
        }
        finally
        {
            if (button != null) button.IsEnabled = true;
            _isCheckingUpdate = false;
        }
    }
}