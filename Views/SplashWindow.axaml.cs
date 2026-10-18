using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Views;

/// <summary>
/// 开场动画（Splash）——「画一个圆」的克制表达。
///
/// 以高级动效的眼光做减法：去掉雷达弧、波纹、粒子、进度条等喧闹元素，
/// 只保留三件事——图标轻轻落定、一道细环画满一圈（连接建立）、词标与一条细线浮现。
/// 全程单一强调色、柔和留白、BackEaseOut 的干脆收束。
/// </summary>
public partial class SplashWindow : Window
{
    // ============ 可调参数 ============
    /// <summary>动画最低展示时长（毫秒）。设为 0 即严格「就绪立刻终止」。</summary>
    private const double MinDisplayMs = 900;
    /// <summary>交接淡出时长（毫秒）。</summary>
    private const double DismissMs = 220;
    /// <summary>细环扫描一帧的步长。</summary>
    private const double TickMs = 16;

    private static SplashWindow? _instance;
    private static CancellationTokenSource _cts = new();
    private static DateTime _shownAt;
    private static bool _dismissed;
    private static bool _testPlaying;

    private readonly double _sweepCirc = Math.PI * 124.0; // 细环周长 ≈ 389.557

    private DispatcherTimer? _tick;
    private double _sweepProgress;

    public SplashWindow()
    {
        InitializeComponent();

        // —— 过渡 ——
        Root.Opacity = 0;
        Root.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(520), Easing = new ExponentialEaseOut() }
        };

        Glow.Transitions = FadeTransitions(900);

        SetupFadeScale(AppIcon, "scale(0.94)", 520, 700);

        Sweep.Opacity = 0;
        Sweep.Transitions = FadeTransitions(320);

        SetupRise(Wordmark, "translateY(10px)", 600, 700);
        SetupFadeScale(Hairline, "scale(0,1)", 320, 620);
        SetupRise(Tagline, "translateY(6px)", 500, 600);

        Opened += (_, _) =>
        {
            if (_dismissed) return;
            _ = StartSequenceAsync();
        };
    }

    // ============================================================
    // 对外 API
    // ============================================================
    public static void ShowSplash()
    {
        _cts = new CancellationTokenSource();
        _dismissed = false;
        _shownAt = DateTime.UtcNow;
        var w = new SplashWindow();
        _instance = w;
        w.Show();
    }

    /// <summary>主程序就绪后调用：立即结束并快速交接淡出。</summary>
    public static async Task DismissAsync()
    {
        var inst = _instance;
        _instance = null;
        if (inst == null) return;

        _dismissed = true;
        _cts.Cancel();

        try
        {
            var elapsed = (DateTime.UtcNow - _shownAt).TotalMilliseconds;
            var wait = MinDisplayMs - elapsed;
            if (wait > 0) await Task.Delay((int)wait);

            try { inst._tick?.Stop(); } catch { }

            inst.Root.Transitions = new Transitions
            {
                new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(DismissMs), Easing = new ExponentialEaseOut() }
            };
            inst.Root.Opacity = 0;
            await Task.Delay((int)DismissMs + 40);
        }
        catch { }

        try { inst._tick?.Stop(); } catch { }
        try { inst.Close(); } catch { }
    }

    /// <summary>实验室「测试动画」：独立播放完整动画后自动结束。</summary>
    public static async Task PlayTestAsync()
    {
        if (_testPlaying) return;
        _testPlaying = true;
        try
        {
            await DismissAsync();

            _cts = new CancellationTokenSource();
            _dismissed = false;
            _shownAt = DateTime.UtcNow;
            var w = new SplashWindow();
            _instance = w;
            w.Show();

            await Task.Delay(2600);
            await DismissAsync();
        }
        finally
        {
            _testPlaying = false;
        }
    }

    // ============================================================
    // 动画时间轴
    // ============================================================
    private async Task StartSequenceAsync()
    {
        var ct = _cts.Token;
        try
        {
            // 卡片浮现
            Root.Opacity = 1;
            await Task.Delay(130, ct);

            // 柔光 + 图标轻轻落定
            Glow.Opacity = 1;
            await Task.Delay(60, ct);
            AppIcon.Opacity = 1;
            AppIcon.RenderTransform = TransformOperations.Parse("scale(1.0)");

            // 细环画满一圈（连接建立），背景推进
            Sweep.Opacity = 0.55;
            StartSweep();
            await Task.Delay(880, ct);

            // 词标浮现
            Wordmark.Opacity = 1;
            Wordmark.RenderTransform = TransformOperations.Parse("translateY(0px)");
            await Task.Delay(240, ct);

            // 细线自中心展开
            Hairline.Opacity = 1;
            Hairline.RenderTransform = TransformOperations.Parse("scale(1,1)");
            await Task.Delay(220, ct);

            // 副标题
            Tagline.Opacity = 0.75;
            Tagline.RenderTransform = TransformOperations.Parse("translateY(0px)");

            // 结束：保持至主界面就绪后由 DismissAsync 收尾
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private void StartSweep()
    {
        _sweepProgress = 0;
        Sweep.StrokeDashOffset = _sweepCirc;
        _tick = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(TickMs) };
        _tick.Tick += OnTick;
        _tick.Start();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        _sweepProgress += TickMs / 1300.0;
        if (_sweepProgress > 1.0) _sweepProgress = 1.0;

        // smoothstep（缓入缓出），让画圆更沉稳
        double p = _sweepProgress;
        double eased = p * p * (3.0 - 2.0 * p);
        Sweep.StrokeDashOffset = _sweepCirc * (1.0 - eased);

        if (_sweepProgress >= 1.0)
        {
            Sweep.Opacity = 0;
            try { _tick?.Stop(); } catch { }
        }
    }

    // ============================================================
    // 过渡辅助
    // ============================================================
    private static Transitions FadeTransitions(double ms) => new()
    {
        new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(ms), Easing = new ExponentialEaseOut() }
    };

    private static void SetupFadeScale(Control c, string from, double fadeMs, double scaleMs)
    {
        c.Opacity = 0;
        c.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);
        c.RenderTransform = TransformOperations.Parse(from);
        c.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(fadeMs), Easing = new ExponentialEaseOut() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(scaleMs), Easing = new BackEaseOut() }
        };
    }

    private static void SetupRise(Control c, string from, double fadeMs, double riseMs)
    {
        c.Opacity = 0;
        c.RenderTransform = TransformOperations.Parse(from);
        c.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(fadeMs), Easing = new ExponentialEaseOut() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(riseMs), Easing = new BackEaseOut() }
        };
    }
}
