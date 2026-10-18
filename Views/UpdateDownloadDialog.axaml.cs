using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class UpdateDownloadDialog : Window
{
    private bool _isCancelling;
    private bool _allowClose;
    private bool _closingAnimated;

    public UpdateDownloadDialog()
    {
        InitializeComponent();

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

        UpdateService.Instance.DownloadProgressChanged += OnProgressChanged;
        UpdateService.Instance.DownloadSpeedChanged += OnSpeedChanged;
        UpdateService.Instance.DownloadCancelled += OnDownloadCancelled;
    }

    public async Task CloseWithAnimationAsync()
    {
        if (_allowClose) return;
        if (_closingAnimated) return;
        _closingAnimated = true;

        RootCard.Opacity = 0;
        RootCard.RenderTransform = TransformOperations.Parse("scale(0.92)");

        await Task.Delay(220);
        _allowClose = true;
        Close();
    }

    private void RootCard_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
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

    private void OnProgressChanged(int percent)
    {
        Dispatcher.UIThread.Post(() =>
        {
            ProgressBar.Value = percent;
            PercentText.Text = $"{percent}%";
        });
    }

    private void OnSpeedChanged(string speed)
    {
        Dispatcher.UIThread.Post(() =>
        {
            SpeedText.Text = speed;
        });
    }

    private void OnDownloadCancelled()
    {
        Dispatcher.UIThread.Post(async () =>
        {
            HintText.Text = "下载已取消";
            CancelButton.IsEnabled = false;
            ProgressBar.Value = 0;
            PercentText.Text = "已取消";
            SpeedText.Text = "";

            await Task.Delay(1000);
            await CloseWithAnimationAsync();
        });
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_isCancelling) return;
        _isCancelling = true;

        CancelButton.IsEnabled = false;
        HintText.Text = "正在取消...";

        UpdateService.Instance.CancelDownload();
    }

    protected override void OnClosed(EventArgs e)
    {
        UpdateService.Instance.DownloadProgressChanged -= OnProgressChanged;
        UpdateService.Instance.DownloadSpeedChanged -= OnSpeedChanged;
        UpdateService.Instance.DownloadCancelled -= OnDownloadCancelled;
        base.OnClosed(e);
    }
}