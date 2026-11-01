using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Threading;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SBtools.Views;

/// <summary>
/// 实时计算的开屏动画（无视频、无预渲染帧）。
/// 所有几何、透明度、进度都由 DispatcherTimer + Stopwatch 在每一帧计算；
/// 根节点放在 Viewbox 中，因此窗口尺寸变化时会整体等比适配。
/// 启动进度由 App 在真实初始化节点调用 SetProgress 推进，未上报时自动缓慢前进。
/// </summary>
public partial class SplashWindow : Window
{
    private const double MinDisplayMs = 1500;
    private const double ReadyHoldMs = 380;
    private const double DismissMs = 260;
    private const double SweepCircumference = Math.PI * 310.0;

    private static SplashWindow? _instance;
    private static bool _testPlaying;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(15) };
    private readonly Stopwatch _clock = new();

    private readonly Border[] _chips;
    private readonly Ellipse[] _dots;
    private readonly TextBlock[] _labels;
    private readonly TextBlock[] _values;
    private readonly double[] _chipAt = { -1, -1, -1, -1 };
    private readonly bool[] _chipOn = new bool[4];

    private double _progress;
    private double _targetProgress;
    private string? _statusOverride;
    private bool _dismissing;
    private bool _fading;
    private bool _readyVisual;
    private double _readyAt;

    // ---------- 预置画刷 ----------
    private static readonly IBrush ChipBgOff = new SolidColorBrush(Color.Parse("#12FFFFFF"));
    private static readonly IBrush ChipBorderOff = new SolidColorBrush(Color.Parse("#1FFFFFFF"));
    private static readonly IBrush ChipDotOff = new SolidColorBrush(Color.Parse("#556B8FB3"));
    private static readonly IBrush ChipLabelOff = new SolidColorBrush(Color.Parse("#66A0C8E8"));
    private static readonly IBrush ChipValueOff = new SolidColorBrush(Color.Parse("#8899BBDD"));

    private static readonly IBrush ChipBgOn = new SolidColorBrush(Color.Parse("#0F7DF0C4"));
    private static readonly IBrush ChipBorderOn = new SolidColorBrush(Color.Parse("#3D7DF0C4"));
    private static readonly IBrush ChipDotOn = new SolidColorBrush(Color.Parse("#FF7DF0C4"));
    private static readonly IBrush ChipLabelOn = new SolidColorBrush(Color.Parse("#CFEFFF"));
    private static readonly IBrush ChipValueOn = new SolidColorBrush(Color.Parse("#B9FFE4"));

    private static readonly IBrush ReadyText = new SolidColorBrush(Color.Parse("#B9FFE4"));
    private static readonly IBrush BarOff = MakeGradient(("#4C8CFF", 0.0), ("#8FE6FF", 0.72), ("#EAFBFF", 1.0));
    private static readonly IBrush BarReady = MakeGradient(("#4DD9B0", 0.0), ("#7DF0C4", 0.55), ("#D9FFF2", 1.0));

    public SplashWindow()
    {
        InitializeComponent();

        _chips = new[] { Chip0, Chip1, Chip2, Chip3 };
        _dots = new[] { Chip0Dot, Chip1Dot, Chip2Dot, Chip3Dot };
        _labels = new[] { Chip0Label, Chip1Label, Chip2Label, Chip3Label };
        _values = new[] { Chip0Value, Chip1Value, Chip2Value, Chip3Value };

        for (int i = 0; i < _chips.Length; i++) ApplyChip(i, false);

        _timer.Tick += OnTick;

        Opened += (_, _) =>
        {
            _clock.Restart();
            _timer.Start();
        };

        Closed += (_, _) =>
        {
            try { _timer.Stop(); } catch { }
        };
    }

    // ============================================================
    // 对外 API（保持原调用方式）
    // ============================================================
    public static void ShowSplash()
    {
        try { _instance?.Close(); } catch { }
        _instance = null;

        var w = new SplashWindow();
        _instance = w;
        w.Show();
    }

    /// <summary>
    /// 由 App 在真实初始化节点调用。value 0..1；status 为空时使用内置阶段文案。
    /// 动画本身仍会按真实时间继续推进，不会因为某一步没有上报而卡死。
    /// </summary>
    public static void SetProgress(double value, string? status = null)
    {
        var inst = _instance;
        if (inst == null) return;

        void Apply()
        {
            inst._targetProgress = Math.Clamp(value, 0, 1);
            if (!string.IsNullOrWhiteSpace(status)) inst._statusOverride = status;
        }

        if (Dispatcher.UIThread.CheckAccess()) Apply();
        else Dispatcher.UIThread.Post(Apply);
    }

    /// <summary>主窗口就绪后调用：补到 100%，短暂停留 READY，再淡出关闭。</summary>
    public static async Task DismissAsync()
    {
        var inst = _instance;
        if (inst == null || inst._dismissing) return;

        inst._dismissing = true;
        inst._targetProgress = 1;

        try
        {
            var elapsed = inst._clock.Elapsed.TotalMilliseconds;
            if (elapsed < MinDisplayMs)
                await Task.Delay((int)(MinDisplayMs - elapsed));

            // 等待平滑进度真正到达 100%（最多约 3 秒）
            for (int i = 0; i < 150 && inst._progress < 0.995; i++)
                await Task.Delay(20);

            await Task.Delay((int)ReadyHoldMs);

            inst._fading = true;
            inst._timer.Stop();

            inst.Root.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = Visual.OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(DismissMs),
                    Easing = new ExponentialEaseOut()
                }
            };
            inst.Root.Opacity = 0;

            await Task.Delay((int)DismissMs + 40);
        }
        catch { }
        finally
        {
            try { inst._timer.Stop(); } catch { }
            try { inst.Close(); } catch { }
            if (ReferenceEquals(_instance, inst)) _instance = null;
        }
    }

    /// <summary>实验室「测试动画」：按完整时间轴播放一遍。</summary>
    public static async Task PlayTestAsync()
    {
        if (_testPlaying) return;
        _testPlaying = true;
        try
        {
            await DismissAsync();

            ShowSplash();
            await Task.Delay(1300);
            SetProgress(0.42, "载入桌面组件");
            await Task.Delay(900);
            SetProgress(0.68, "检查系统优化项");
            await Task.Delay(800);
            SetProgress(0.90, "连接插件与通知服务");
            await Task.Delay(500);

            await DismissAsync();
        }
        finally
        {
            _testPlaying = false;
        }
    }

    // ============================================================
    // 每帧计算
    // ============================================================
    private void OnTick(object? sender, EventArgs e)
    {
        if (_fading) return;

        double t = _clock.Elapsed.TotalSeconds;

        // 根节点淡入
        Root.Opacity = EaseOut(Seg(t, 0.0, 0.35));

        // 电源横线
        double pl = EaseOut(Seg(t, 0.02, 0.40));
        double lineW = 620 * pl;
        PowerLine.Width = lineW;
        Canvas.SetLeft(PowerLine, 460 - lineW / 2);
        PowerLine.Opacity = pl > 0 && pl < 1 ? Math.Sin(pl * Math.PI) * 0.9 : 0;

        // 中心柔光
        double gp = EaseOut(Seg(t, 0.18, 0.85));
        Glow.Opacity = gp * 0.92;
        Glow.RenderTransform = new ScaleTransform(0.90 + 0.10 * gp, 0.90 + 0.10 * gp);

        // 环形描边
        double sp = EaseInOut(Seg(t, 0.15, 1.15));
        Sweep.StrokeDashOffset = SweepCircumference * (1 - sp);
        Sweep.Opacity = sp > 0 && sp < 1 ? 0.60 : 0;

        // 图标自下而上揭开
        double wp = EaseInOut(Seg(t, 0.50, 1.08));
        IconStage.Opacity = EaseOut(Seg(t, 0.45, 0.75));
        IconReveal.Height = 260 * wp;
        Canvas.SetTop(WipeLine, 260 * (1 - wp));
        WipeLine.Opacity = wp > 0 && wp < 1 ? Math.Sin(wp * Math.PI) * 0.95 : 0;

        double iconScale = 1.02 - 0.02 * wp;
        IconStage.RenderTransform = new ScaleTransform(iconScale, iconScale);

        // 词标
        double wm = EaseOutExpo(Seg(t, 0.95, 1.30));
        Wordmark.Opacity = wm;
        Wordmark.RenderTransform = new TranslateTransform(0, (1 - wm) * 18);
        Wordmark.LetterSpacing = 6 - 5 * wm;

        // 副标题
        double tg = EaseOut(Seg(t, 1.10, 1.40));
        Tagline.Opacity = tg;
        Tagline.RenderTransform = new TranslateTransform(0, (1 - tg) * 10);

        Meta.Opacity = EaseOut(Seg(t, 1.25, 1.55)) * 0.9;

        // 进度：真实上报 + 时间兜底
        double auto = 0.06 + 0.58 * (1 - Math.Exp(-t / 2.2));
        double target = Math.Max(_targetProgress, auto);
        if (_targetProgress >= 0.999) target = 1.0;

        _progress += (target - _progress) * 0.09;
        if (target >= 0.999 && _progress > 0.995) _progress = 1.0;
        _progress = Math.Clamp(_progress, 0, 1);

        double pIn = EaseOut(Seg(t, 0.85, 1.10));
        BarGroup.Opacity = pIn;
        StatusGroup.Opacity = EaseOut(Seg(t, 0.90, 1.15));
        ChipsGroup.Opacity = EaseOut(Seg(t, 0.95, 1.20));

        BarFill.Width = 440 * _progress;
        Pct.Text = $"{Math.Round(_progress * 100):0}%";

        // 状态文案
        string status;
        if (_progress >= 0.995) status = "准备就绪";
        else if (!string.IsNullOrEmpty(_statusOverride)) status = _statusOverride!;
        else if (_progress < 0.30) status = "正在初始化运行环境";
        else if (_progress < 0.62) status = "载入桌面小组件";
        else if (_progress < 0.84) status = "检查系统优化项";
        else status = "连接插件与通知服务";

        if (StatusText.Text != status) StatusText.Text = status;

        // 模块 chip
        double[] thresholds = { 0.32, 0.62, 0.84, 0.995 };
        for (int i = 0; i < _chips.Length; i++)
        {
            if (!_chipOn[i] && _progress >= thresholds[i])
            {
                _chipOn[i] = true;
                _chipAt[i] = t;
                ApplyChip(i, true);
            }

            double age = _chipAt[i] >= 0 ? t - _chipAt[i] : 0;
            double scale = _chipOn[i] ? 1 + 0.055 * Math.Exp(-age * 14) : 1;
            _chips[i].RenderTransform = new ScaleTransform(scale, scale);
        }

        // READY 视觉
        if (_progress >= 0.995)
        {
            if (!_readyVisual)
            {
                _readyVisual = true;
                _readyAt = t;
                BarFill.Background = BarReady;
                Pct.Foreground = ReadyText;
                StatusText.Foreground = ReadyText;
                StatusDot.Fill = ReadyText;
            }

            Glow.Opacity = 0.90 + 0.05 * Math.Sin((t - _readyAt) * 5.5);
        }
        else
        {
            BarFill.Background = BarOff;
        }
    }

    private void ApplyChip(int i, bool on)
    {
        _chips[i].Background = on ? ChipBgOn : ChipBgOff;
        _chips[i].BorderBrush = on ? ChipBorderOn : ChipBorderOff;
        _dots[i].Fill = on ? ChipDotOn : ChipDotOff;
        _labels[i].Foreground = on ? ChipLabelOn : ChipLabelOff;
        _values[i].Foreground = on ? ChipValueOn : ChipValueOff;
    }

    // ============================================================
    // 小工具
    // ============================================================
    private static double Seg(double t, double a, double b)
        => Math.Clamp((t - a) / (b - a), 0, 1);

    private static double EaseOut(double x) => 1 - Math.Pow(1 - x, 3);

    private static double EaseInOut(double x)
        => x < 0.5 ? 4 * x * x * x : 1 - Math.Pow(-2 * x + 2, 3) / 2;

    private static double EaseOutExpo(double x)
        => x >= 1 ? 1 : 1 - Math.Pow(2, -10 * x);

    private static IBrush MakeGradient(params (string Color, double Offset)[] stops)
    {
        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0, RelativeUnit.Relative)
        };
        foreach (var (color, offset) in stops)
            brush.GradientStops.Add(new GradientStop(Color.Parse(color), offset));
        return brush;
    }
}
