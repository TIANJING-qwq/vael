using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Animation;

namespace SBtools.Views;

/// <summary>
/// 通用桌面小组件窗。左键=动作，拖动空白=移动，右键=详细配置；
/// 出现时非线性入场动画；材质全局实时生效。
/// </summary>
public partial class SimpleWidgetWindow : Window
{
    public enum Kind { Clock, Countdown, Gallery, Dice, Coin, Roll, Quote, Battery, Calc, Pomodoro }

    public sealed class Config
    {
        public DateTime Target = DateTime.Now.AddDays(30);
        public int Range = 45, DiceFaces = 6, PomoMinutes = 25;
        public string Title = "";
    }

    private static string _materialHex = "#D0202020";
    public static string MaterialHex
    {
        get => _materialHex;
        set
        {
            _materialHex = value;
            foreach (var w in _all) w.ApplyMaterial();
            try { WidgetWindow.Instance?.ApplyMaterial(value); } catch { }
        }
    }
    private static readonly List<SimpleWidgetWindow> _all = new();

    private readonly Kind _kind;
    private readonly Config _cfg;
    private readonly Border _card = new()
    {
        CornerRadius = new Avalonia.CornerRadius(12),
        BorderBrush = Brush.Parse("#33FFFFFF"),
        BorderThickness = new Avalonia.Thickness(1),
        Margin = new Avalonia.Thickness(6),
        ClipToBounds = true,
        Padding = new Avalonia.Thickness(14),
    };
    private readonly TextBlock _big = new() { FontSize = 24, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _sub = new() { FontSize = 11, Opacity = 0.65, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly Image? _img;
    private readonly Polyline? _graph;
    private readonly List<double> _history = new();
    private string[]? _imgs;
    private readonly Random _rnd = new();
    private System.Timers.Timer? _pomo;
    private DateTime _pomoEnd;
    // 计算器
    private TextBox? _calcInput;

    [StructLayout(LayoutKind.Sequential)]
    private class SYSTEM_POWER_STATUS { public byte ACLineStatus, BatteryFlag, BatteryLifePercent; public byte Reserved1; public int BatteryLifeTime, BatteryFullLifeTime; }
    [DllImport("user32.dll")] private static extern bool GetSystemPowerStatus([MarshalAs(UnmanagedType.LPStruct)] SYSTEM_POWER_STATUS lp);
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lp);

    public SimpleWidgetWindow(Kind kind, Config cfg)
    {
        _kind = kind; _cfg = cfg;
        Title = cfg.Title;
        Width = kind is Kind.Gallery ? 300 : 230;
        Height = kind is Kind.Gallery or Kind.Calc ? 260 : kind is Kind.Battery && cfg.Title == "内存占用" ? 190 : 150;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = true;

        var panel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        // 标题栏（拖动移动 + 关闭）
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleTb = new TextBlock { Text = cfg.Title, FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(titleTb, 0);
        head.Children.Add(titleTb);
        var close = new Button { Content = "✕", FontSize = 10, Padding = new Avalonia.Thickness(5, 1), Opacity = 0.5 };
        Grid.SetColumn(close, 1);
        close.Click += (_, _) => Close();
        head.Children.Add(close);
        head.PointerPressed += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); };
        panel.Children.Add(head);

        switch (kind)
        {
            case Kind.Gallery:
                _img = new Image { Stretch = Stretch.Uniform, MaxHeight = 165 };
                _img.PointerPressed += (_, e) => { if (e.GetCurrentPoint(_img).Properties.IsLeftButtonPressed) { Shuffle(); e.Handled = true; } };
                panel.Children.Add(_img);
                TryLoadImages();
                break;
            case Kind.Calc:
                BuildCalc(panel);
                break;
            case Kind.Pomodoro:
                var startStop = new Button { Content = "开始专注", HorizontalAlignment = HorizontalAlignment.Center, CornerRadius = new Avalonia.CornerRadius(8), Padding = new Avalonia.Thickness(16, 6) };
                startStop.Click += (_, _) => PomoToggle(startStop);
                panel.Children.Add(_big);
                panel.Children.Add(startStop);
                break;
            case Kind.Battery when cfg.Title == "内存占用":
                _graph = new Polyline { Stroke = Brush.Parse("#60CDFF"), StrokeThickness = 1.5, Points = new List<Avalonia.Point>(), Width = 196, Height = 46, HorizontalAlignment = HorizontalAlignment.Center };
                panel.Children.Add(_big);
                panel.Children.Add(_graph);
                break;
            default:
                _big.PointerPressed += (_, e) => { if (e.GetCurrentPoint(_big).Properties.IsLeftButtonPressed) { Action(); e.Handled = true; } };
                panel.Children.Add(_big);
                break;
        }
        panel.Children.Add(_sub);
        _card.Child = panel;
        Content = _card;
        ApplyMaterial();

        // 窗体空白处：左键拖动 / 右键配置
        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) { OpenConfig(); e.Handled = true; }
        };

        _all.Add(this);
        Closed += (_, _) => _all.Remove(this);
        Position = new Avalonia.PixelPoint(
            (int)(Screens.Primary?.WorkingArea.Width - (int)Width - 20 ?? 1200),
            90 + _rnd.Next(200));

        var t = new System.Timers.Timer(1000) { AutoReset = true };
        t.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Tick);
        t.Start();
        Tick();
        Action(); // 初始内容
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // ★ 非线性入场：透明+缩小 → 就位（ExponentialEaseOut，无过冲）
        _card.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(300), Easing = new Avalonia.Animation.Easings.ExponentialEaseOut() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(400), Easing = new Avalonia.Animation.Easings.ExponentialEaseOut() },
        };
        _card.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
        Opacity = 0;
        _card.RenderTransform = TransformOperations.Parse("scale(0.88)");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Opacity = 1;
            _card.RenderTransform = TransformOperations.Parse("scale(1)");
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyMaterial() => _card.Background = Brush.Parse(_materialHex);

    private void TryLoadImages()
    {
        try
        {
            var dir = Services.GalleryService.CacheDir;
            _imgs = Array.FindAll(Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories),
                f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                     f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                     f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
        }
        catch { _imgs = Array.Empty<string>(); }
    }

    private void Tick()
    {
        try
        {
            switch (_kind)
            {
                case Kind.Clock:
                    var now = DateTime.Now;
                    _big.Text = now.ToString("HH:mm:ss");
                    _sub.Text = now.ToString("yyyy年M月d日 ") + "周" + "日一二三四五六"[(int)now.DayOfWeek];
                    break;
                case Kind.Countdown:
                    var left = _cfg.Target - DateTime.Now;
                    if (left < TimeSpan.Zero) { _big.Text = "已到达"; _sub.Text = "目标时间已过，右键修改"; }
                    else
                    {
                        _big.Text = $"{left.Days}天 {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
                        _sub.Text = $"目标 {_cfg.Target:yyyy-M-d HH:mm}";
                    }
                    break;
                case Kind.Battery:
                    if (_cfg.Title == "C 盘")
                    {
                        var c = new System.IO.DriveInfo("C");
                        _big.Text = $"{c.TotalFreeSpace / 1024.0 / 1024 / 1024:F0} GB";
                        _sub.Text = $"C 盘剩余 / 共 {c.TotalSize / 1024.0 / 1024 / 1024:F0} GB";
                    }
                    else if (_cfg.Title == "内存占用")
                    {
                        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                        if (GlobalMemoryStatusEx(ref mem))
                        {
                            _big.Text = $"内存 {mem.dwMemoryLoad}%";
                            _sub.Text = $"已用 {(mem.ullTotalPhys - mem.ullAvailPhys) / 1024.0 / 1024 / 1024:F1} / {mem.ullTotalPhys / 1024.0 / 1024 / 1024:F1} GB";
                            // 曲线：最近 60 个采样
                            _history.Add(mem.dwMemoryLoad);
                            if (_history.Count > 60) _history.RemoveAt(0);
                            if (_graph != null)
                            {
                                var pts = new List<Avalonia.Point>();
                                for (int i = 0; i < _history.Count; i++)
                                    pts.Add(new Avalonia.Point(i * 196.0 / 59.0, 46 - _history[i] * 0.44));
                                _graph.Points = pts;
                            }
                        }
                    }
                    else
                    {
                        var s = new SYSTEM_POWER_STATUS();
                        if (GetSystemPowerStatus(s))
                        {
                            _big.Text = $"电量 {s.BatteryLifePercent}%";
                            _sub.Text = s.ACLineStatus == 1 ? "充电中（交流电）" : "使用电池";
                        }
                    }
                    break;
                case Kind.Pomodoro when _pomo != null:
                    var l = _pomoEnd - DateTime.Now;
                    _big.Text = l > TimeSpan.Zero ? $"{(int)l.TotalMinutes:00}:{l.Seconds:00}" : "完成";
                    break;
            }
        }
        catch { }
    }

    private void Action()
    {
        switch (_kind)
        {
            case Kind.Dice:
                _big.Text = $"{_rnd.Next(1, _cfg.DiceFaces + 1)} 点";
                _sub.Text = $"{_cfg.DiceFaces} 面骰 · 点击再掷 · 右键配置";
                break;
            case Kind.Coin:
                _big.Text = _rnd.Next(2) == 0 ? "正面" : "反面";
                _sub.Text = "点击再抛";
                break;
            case Kind.Roll:
                _big.Text = $"第 {_rnd.Next(1, _cfg.Range + 1)} 号";
                _sub.Text = $"范围 1-{_cfg.Range} · 点击重抽 · 右键配置";
                break;
            case Kind.Quote:
                var qs = new[] { "行到水穷处，坐看云起时。", "日拱一卒，功不唐捐。", "凡是过往，皆为序章。", "山不让尘，川不辞盈。", "追风赶月莫停留，平芜尽处是春山。", "路虽远，行则将至。", "不积跬步，无以至千里。" };
                _big.FontSize = 15;
                _big.Text = qs[_rnd.Next(qs.Length)];
                _sub.Text = "点击换一句";
                break;
            case Kind.Gallery: Shuffle(); break;
            case Kind.Calc: break;
        }
    }

    private void Shuffle()
    {
        if (_imgs == null || _imgs.Length == 0) { _sub.Text = "无缓存图（先在涩图合集下载）"; return; }
        try
        {
            _img!.Source = new Avalonia.Media.Imaging.Bitmap(_imgs[_rnd.Next(_imgs.Length)]);
            _sub.Text = $"共 {_imgs.Length} 张 · 点击换图";
        }
        catch { }
    }

    // ================= 计算器（现代化按键式） =================
    private void BuildCalc(StackPanel panel)
    {
        _calcInput = new TextBox { FontSize = 20, TextAlignment = TextAlignment.Right, IsReadOnly = true, Text = "0" };
        panel.Children.Add(_calcInput);

        var grid = new Grid { Margin = new Avalonia.Thickness(0, 8, 0, 0) };
        for (int i = 0; i < 4; i++) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        for (int i = 0; i < 5; i++) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(38) });

        string[] keys = { "C", "←", "%", "÷", "7", "8", "9", "×", "4", "5", "6", "−", "1", "2", "3", "+", "0", ".", "（", "）" };
        var expr = "";
        for (int i = 0; i < keys.Length; i++)
        {
            var k = keys[i];
            var b = new Button
            {
                Content = k, FontSize = 14,
                CornerRadius = new Avalonia.CornerRadius(8),
                Margin = new Avalonia.Thickness(2),
                Background = "0123456789.".Contains(k) && k.Length == 1
                    ? Brush.Parse("#22FFFFFF") : Brush.Parse("#33FFFFFF"),
                Tag = k,
            };
            Grid.SetColumn(b, i % 4); Grid.SetRow(b, i / 4);
            b.Click += (_, _) =>
            {
                try
                {
                    if (k == "C") expr = "";
                    else if (k == "←") expr = expr.Length > 0 ? expr[..^1] : "";
                    else if (k == "=") { }
                    else expr += k;
                    if (long.TryParse(expr, out _) || expr == "") _calcInput.Text = expr == "" ? "0" : expr;
                    else _calcInput.Text = expr;
                }
                catch { }
            };
            grid.Children.Add(b);
        }
        // = 大按钮占满最后一行
        var eq = new Button
        {
            Content = "=", FontSize = 16, FontWeight = FontWeight.Bold,
            CornerRadius = new Avalonia.CornerRadius(8),
            Margin = new Avalonia.Thickness(2),
            Background = Brush.Parse("#0078D4"), Foreground = Brushes.White,
        };
        Grid.SetColumnSpan(eq, 4); Grid.SetRow(eq, 4);
        eq.Click += (_, _) =>
        {
            try
            {
                var e2 = expr.Replace('×', '*').Replace('÷', '/').Replace('−', '-');
                _calcInput.Text = new System.Data.DataTable().Compute(e2, null)?.ToString() ?? "0";
                expr = _calcInput.Text;
            }
            catch { _calcInput.Text = "错误"; expr = ""; }
        };
        grid.Children.Add(eq);
        panel.Children.Add(grid);
        _sub.Text = "按键输入 · = 求值";
    }

    private void PomoToggle(Button btn)
    {
        if (_pomo == null)
        {
            _pomoEnd = DateTime.Now.AddMinutes(_cfg.PomoMinutes);
            _pomo = new System.Timers.Timer(1000) { AutoReset = true };
            _pomo.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                Tick();
                if (DateTime.Now >= _pomoEnd) { _pomo?.Stop(); _pomo = null; btn.Content = "开始专注"; _big.Text = "完成"; }
            });
            _pomo.Start();
            btn.Content = "放弃";
            _sub.Text = $"专注 {_cfg.PomoMinutes} 分钟 · 右键配置";
        }
        else { _pomo.Stop(); _pomo = null; btn.Content = "开始专注"; _big.Text = "已停止"; }
    }

    /// <summary>右键：该组件的详细配置窗</summary>
    private void OpenConfig()
    {
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };
        var w = new Window { Title = $"{_cfg.Title} · 配置", Width = 320, Height = 230, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        var tb1 = new TextBox { Watermark = "参数" };
        var ok = new Button { Content = "保存并刷新", HorizontalAlignment = HorizontalAlignment.Right };

        switch (_kind)
        {
            case Kind.Countdown:
                panel.Children.Add(new TextBlock { Text = "目标时间（如 2026-06-07 09:00）" });
                tb1.Text = _cfg.Target.ToString("yyyy-MM-dd HH:mm"); panel.Children.Add(tb1);
                break;
            case Kind.Roll:
                panel.Children.Add(new TextBlock { Text = _cfg.Title == "随机题号" ? "题号上限" : "总人数" });
                tb1.Text = _cfg.Range.ToString(); panel.Children.Add(tb1);
                break;
            case Kind.Dice:
                panel.Children.Add(new TextBlock { Text = "骰子面数" });
                tb1.Text = _cfg.DiceFaces.ToString(); panel.Children.Add(tb1);
                break;
            case Kind.Pomodoro:
                panel.Children.Add(new TextBlock { Text = "专注时长（分钟）" });
                tb1.Text = _cfg.PomoMinutes.ToString(); panel.Children.Add(tb1);
                break;
            case Kind.Gallery:
                TryLoadImages(); Shuffle();
                panel.Children.Add(new TextBlock { Text = $"已重新加载 {_imgs?.Length ?? 0} 张图" });
                break;
            default:
                panel.Children.Add(new TextBlock { Text = "该组件无配置项。\n左键=动作 · 标题栏拖动=移动 · 右键=本配置窗。" });
                break;
        }
        panel.Children.Add(ok);
        ok.Click += (_, _) =>
        {
            try
            {
                switch (_kind)
                {
                    case Kind.Countdown: if (DateTime.TryParse(tb1.Text, out var dt)) _cfg.Target = dt; break;
                    case Kind.Roll: if (int.TryParse(tb1.Text, out var n) && n > 0) _cfg.Range = n; break;
                    case Kind.Dice: if (int.TryParse(tb1.Text, out var f) && f > 1) _cfg.DiceFaces = f; break;
                    case Kind.Pomodoro: if (int.TryParse(tb1.Text, out var m) && m > 0) _cfg.PomoMinutes = m; break;
                }
                Action(); Tick();
            }
            catch { }
            w.Close();
        };
        w.Content = new ScrollViewer { Content = panel };
        w.Show(this);
    }
}
