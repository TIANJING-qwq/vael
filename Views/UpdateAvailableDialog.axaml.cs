using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using SBtools.Controls;
using SBtools.Services;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class UpdateAvailableDialog : Window
{
    private bool _allowClose;
    private bool _closingAnimated;

    public UpdateAvailableDialog() : this("未知", "暂无更新说明") { }

    public UpdateAvailableDialog(string version, string changelog)
        : this(version, changelog, "发现新版本", "立即更新") { }

    public UpdateAvailableDialog(string version, string changelog, string title, string confirmText)
    {
        InitializeComponent();
        this.DismissOnDeactivate(false); // 点弹窗外空白处自动取消

        TitleText.Text = title;
        VersionText.Text = $"最新版本：{version}";
        ChangelogHost.Content = MarkdownRenderer.Render(changelog);
        DownloadButton.Content = confirmText;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        Opened += async (_, _) =>
        {
            await Task.Delay(30);
            RootCard.Opacity = 1;
            RootCard.RenderTransform = TransformOperations.Parse("scale(1.0)");
        };

        Closing += async (_, e) =>
        {
            if (_allowClose) return;
            e.Cancel = true;

            if (_closingAnimated) return;
            _closingAnimated = true;

            RootCard.Opacity = 0;
            RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

            await Task.Delay(220);
            _allowClose = true;
            Close();
        };
    }

    private void RootCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            // 点击按钮时不触发窗口拖动
            Avalonia.StyledElement? el = e.Source as Avalonia.StyledElement;
            while (el != null)
            {
                if (el is Button) return;
                el = el.Parent;
            }

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                BeginMoveDrag(e);
        }
        catch { }
    }

    private async void LaterButton_Click(object? sender, RoutedEventArgs e)
    {
        await CloseWithAnimationAsync(false);
    }

    private async void DownloadButton_Click(object? sender, RoutedEventArgs e)
    {
        await CloseWithAnimationAsync(true);
    }

    private async Task CloseWithAnimationAsync(bool result)
    {
        if (_allowClose) return;
        if (_closingAnimated) return;
        _closingAnimated = true;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        await Task.Delay(220);
        _allowClose = true;
        Close(result);
    }
}