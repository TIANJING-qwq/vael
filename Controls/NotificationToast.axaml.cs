using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Controls;

public partial class NotificationToast : UserControl
{
    private CancellationTokenSource? _cts;
    private bool _isShown;

    private readonly ScaleTransform _progressScale = new(1, 1);

    private const int EnterDurationMs = 850;
    private const int ExitDurationMs  = 700;
    private const int FadeDurationMs  = 450;
    private const int SlideOffsetPx   = 360;

    public event EventHandler? Closed;

    public string Title
    {
        get => TitleText.Text ?? string.Empty;
        set => TitleText.Text = value;
    }

    public string Message
    {
        get => MessageText.Text ?? string.Empty;
        set => MessageText.Text = value;
    }

    public NotificationToast()
    {
        InitializeComponent();

        ProgressFill.RenderTransform = _progressScale;

        RootBorder.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(FadeDurationMs),
                Easing = new ExponentialEaseOut()
            },
            new TransformOperationsTransition
            {
                Property = RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(EnterDurationMs),
                // 用无过冲的缓动，避免通知向左弹时溢出到内容区
                Easing = new ExponentialEaseOut()
            }
        };
    }

    public async Task ShowAsync(int durationSeconds = 5)
    {
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _isShown = false;
            RootBorder.Opacity = 0;
            RootBorder.RenderTransform = TransformOperations.Parse($"translateX({SlideOffsetPx}px)");
            _progressScale.ScaleX = 1;

            if (!await SafeDelayAsync(30, token)) return;

            RootBorder.Opacity = 1;
            RootBorder.RenderTransform = TransformOperations.Parse("translateX(0px)");
            _isShown = true;

            var start = DateTime.Now;
            var totalMs = durationSeconds * 1000.0;

            while (!token.IsCancellationRequested)
            {
                var elapsed = (DateTime.Now - start).TotalMilliseconds;
                var remaining = Math.Max(0, totalMs - elapsed);
                _progressScale.ScaleX = remaining / totalMs;

                if (remaining <= 0) break;

                if (!await SafeDelayAsync(16, token)) return;
            }

            if (!token.IsCancellationRequested)
                await HideAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowAsync 兜底: {ex.Message}");
        }
    }

    /// <summary>设置底部进度条（0~1）。</summary>
    public void SetProgress(double percent)
    {
        _progressScale.ScaleX = Math.Clamp(percent, 0, 1);
    }

    /// <summary>显示一个常驻通知（不自动消失，进度条由 SetProgress 驱动）。</summary>
    public async Task ShowPersistentAsync()
    {
        try
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            _isShown = false;
            RootBorder.Opacity = 0;
            RootBorder.RenderTransform = TransformOperations.Parse($"translateX({SlideOffsetPx}px)");

            if (!await SafeDelayAsync(30, token)) return;

            RootBorder.Opacity = 1;
            RootBorder.RenderTransform = TransformOperations.Parse("translateX(0px)");
            _isShown = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ShowPersistentAsync 兜底: {ex.Message}");
        }
    }

    public async Task HideAsync()
    {
        if (!_isShown) return;
        _isShown = false;

        try
        {
            if (RootBorder.Transitions != null)
            {
                foreach (var t in RootBorder.Transitions)
                {
                    if (t is TransformOperationsTransition tot)
                        tot.Duration = TimeSpan.FromMilliseconds(ExitDurationMs);
                }
            }

            RootBorder.Opacity = 0;
            RootBorder.RenderTransform = TransformOperations.Parse($"translateX({SlideOffsetPx}px)");

            await Task.Delay(ExitDurationMs + 80);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"HideAsync 兜底: {ex.Message}");
        }
        finally
        {
            try { Closed?.Invoke(this, EventArgs.Empty); } catch { }
        }
    }

    private async void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            _cts?.Cancel();
            await HideAsync();
        }
        catch { }
    }

    /// <summary>
    /// ★ 永不抛异常的延迟：轮询方式，不使用带 token 的 Task.Delay
    /// </summary>
    private static async Task<bool> SafeDelayAsync(int ms, CancellationToken token)
    {
        var end = DateTime.Now.AddMilliseconds(ms);
        while (DateTime.Now < end)
        {
            if (token.IsCancellationRequested) return false;
            await Task.Delay(10).ConfigureAwait(false);
        }
        return true;
    }
}