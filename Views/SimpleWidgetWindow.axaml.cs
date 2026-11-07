using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Animation;
using SBtools.Models;

namespace SBtools.Views;

/// <summary>
/// 通用桌面小组件窗。左键=动作，拖动空白=移动，右键=详细配置；
/// 出现时非线性入场动画；材质全局实时生效。
/// </summary>
public partial class SimpleWidgetWindow : Window
{
    public enum Kind { Clock, Countdown, Gallery, Dice, Coin, Roll, Quote, Battery, Calc, Pomodoro, Perf }

    public sealed class Config
    {
        public DateTime Target = DateTime.Now.AddDays(30);
        public int Range = 45, DiceFaces = 6, PomoMinutes = 25;
        public string Title = "";
    }

    // ===== 材质系统：与主界面同源——DWM 系统背景（云母/亚克力），下拉选择 =====
    // Gloss=液态玻璃：在亚克力基础上叠加顶部高光/亮边/光泽渐变（AquaDesk 式分层光学）
    public sealed record MatInfo(string Name, string CardHex, int Backdrop, bool Gloss = false)
    {
        public bool Dark => Convert.ToInt32(CardHex.Substring(1, 2), 16) +
                            Convert.ToInt32(CardHex.Substring(3, 2), 16) +
                            Convert.ToInt32(CardHex.Substring(5, 2), 16) < 330;
        public Avalonia.Media.IBrush ForeBrush => Dark ? Brushes.White : Brushes.Black;
    }

    // DWMWA_SYSTEMBACKDROP_TYPE: 1=云母；亚克力用 ACCENT 合成模糊（对无边框窗最可靠）
    public static readonly MatInfo[] Materials =
    {
        new("云母", "#40282828", 1),
        new("亚克力", "#48282828", 2),
        new("液态玻璃", "#2E282828", 2, Gloss: true),
    };

    /// <summary>材质切换广播（DockWindow 等非 SimpleWidgetWindow 窗口跟随）</summary>
    public static event Action<MatInfo>? MaterialChanged;

    private static MatInfo _mat = Materials[0];
    public static MatInfo Material
    {
        get => _mat;
        set
        {
            _mat = value;
            foreach (var w in _all) w.ApplyMaterial();
            try { WidgetWindow.Instance?.ApplyMaterial(value); } catch { }
            try { MaterialChanged?.Invoke(value); } catch { }
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
        Margin = new Avalonia.Thickness(0),
        ClipToBounds = true,
        Padding = new Avalonia.Thickness(14),
    };
    private readonly TextBlock _big = new() { FontSize = 24, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _sub = new() { FontSize = 11, Opacity = 0.65, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly Image? _img;
    private readonly Polyline? _graph;
    private readonly List<double> _history = new();
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
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent, WindowTransparencyLevel.AcrylicBlur };
        Background = Brushes.Transparent;
        Topmost = true;

        var panel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };

        // 标题栏（拖动移动 + 关闭）
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleTb = new TextBlock { Text = cfg.Title, FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(titleTb, 0);
        head.Children.Add(titleTb);
        // 涩图：上一张小按钮
        if (kind == Kind.Gallery)
        {
            var prev = new Button { Content = "‹", FontSize = 13, Padding = new Avalonia.Thickness(7, 1), Opacity = 0.7, CornerRadius = new Avalonia.CornerRadius(5) };
            Grid.SetColumn(prev, 1);
            prev.Click += (_, _) => Prev();
            head.Children.Add(prev);
        }
        var close = new Button { Content = "✕", FontSize = 10, Padding = new Avalonia.Thickness(5, 1), Opacity = 0.5 };
        Grid.SetColumn(close, 2);
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
            case Kind.Perf:
                _big.Text = "CPU  --";
                _sub.Text = "内存 -- · 网速 --";
                _big.PointerPressed += (_, e) => { e.Handled = true; }; // 点击不换内容，仅拖动用
                panel.Children.Add(_big);
                InitPerf();
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
        // 液态玻璃：背景抓图层 + 可读性纱层 + 真折射玻璃层 + 内容层
        _backdrop = new Image { Stretch = Stretch.Fill, Opacity = 0 };
        _scrim = new Border { Opacity = 0 }; // 压暗纱层，保证文字对比度
        _topLight = new Border { Height = 1, VerticalAlignment = VerticalAlignment.Top, IsVisible = false }; // 玻璃顶缘亮线
        _liquid = new Controls.LiquidGlass { CornerRadius = 12, IsVisible = false }; // 真折射玻璃（采样壁纸+SKSL）
        var grid = new Grid();
        grid.Children.Add(_backdrop);
        grid.Children.Add(_scrim);
        grid.Children.Add(_liquid);
        grid.Children.Add(panel);
        grid.Children.Add(_topLight);
        _card.Child = grid;
        Content = _card;
        ApplyMaterial();

        // 窗体：左键空白=拖动 / 中键=涩图上一张 / 右键=配置（控件自身动作已 Handled，不会到这）
        PointerPressed += (_, e) =>
        {
            var pt = e.GetCurrentPoint(this);
            if (pt.Properties.IsRightButtonPressed) { OpenConfig(); e.Handled = true; }
            else if (pt.Properties.IsMiddleButtonPressed) { Prev(); e.Handled = true; }
            else if (pt.Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };

        _all.Add(this);
        EnsureAutoSave(); // ★ 定时持久化：进程被结束也能保住会话
        Closed += (_, _) => { _all.Remove(this); StopLiquid(); SaveSession(); };
        Position = new Avalonia.PixelPoint(
            (int)(Screens.Primary?.WorkingArea.Width - (int)Width - 20 ?? 1200),
            90 + _rnd.Next(200));

        var t = new System.Timers.Timer(1000) { AutoReset = true };
        t.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Tick);
        t.Start();
        Tick();
        Action(); // 初始内容
    }

    private bool _reallyClosing;

    /// <summary>关闭前播淡出+缩退动画（非线性），动画完再真关</summary>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        base.OnClosing(e);
        if (_reallyClosing || !IsVisible) return;
        _reallyClosing = true;
        e.Cancel = true;
        _card.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(260), Easing = new Avalonia.Animation.Easings.ExponentialEaseOut() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(280), Easing = new Avalonia.Animation.Easings.ExponentialEaseOut() },
        };
        _card.Opacity = 0;
        _card.RenderTransform = TransformOperations.Parse("scale(0.86)");
        var t = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        t.Tick += (_, _) => { t.Stop(); Close(); };
        t.Start();
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
        _card.Opacity = 0;
        _card.RenderTransform = TransformOperations.Parse("scale(0.88)");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _card.Opacity = 1;
            _card.RenderTransform = TransformOperations.Parse("scale(1)");
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void ApplyMaterial()
    {
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        // 亚克力：ACCENT 合成模糊（真模糊）；云母：DWM 系统背景（无边框窗尽力生效）+ 半透明底
        EnableBlurBehind(this, _mat.Backdrop == 2 ? "#99202020" : null);
        try
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd != IntPtr.Zero)
            {
                var bd = 1; // 云母尝试系统背景（仅云母模式）
                if (_mat.Backdrop == 1) _ = DwmSetWindowAttribute(hwnd, 38, ref bd, 4);
                int pref = 2; // DWM 圆角
                _ = DwmSetWindowAttribute(hwnd, 33, ref pref, 4);
            }
        }
        catch { }

        _card.Background = _mat.Gloss
            ? null // 真液态玻璃：底由折射着色器绘制（边缘真实弯曲+色散）
            : new SolidColorBrush(Avalonia.Media.Color.Parse(_mat.CardHex));
        _card.BorderBrush = Brush.Parse(_mat.Gloss ? "#59FFFFFF" : "#26FFFFFF");
        _card.BorderThickness = new Avalonia.Thickness(1);
        if (_scrim != null) _scrim.Background = Brushes.Transparent;
        if (_liquid != null) _liquid.IsVisible = _mat.Gloss;
        if (_topLight != null)
        {
            _topLight.IsVisible = false; // 高光由着色器生成（镜面+内阴影）
            _topLight.Background = Brush.Parse("#3DFFFFFF");
        }

        // 字色按材质亮度反色
        SetForegrounds(_card, _mat.ForeBrush);
        StopLiquid();
    }

    // ===== 会话记录：退出时保存打开的小组件，启动时恢复 =====
    private sealed class SessionItem
    {
        public int K { get; set; }             // Kind
        public string T { get; set; } = "";    // Title
        public string G { get; set; } = "";    // Target(O格式)
        public int R { get; set; }             // Range
        public int F { get; set; }             // DiceFaces
        public int P { get; set; }             // PomoMinutes
        public int X { get; set; }             // 位置X
        public int Y { get; set; }             // 位置Y
        public bool Z { get; set; }            // 置顶
    }

    public static void SaveSession()
    {
        try
        {
            var list = _all.Select(w => new SessionItem
            {
                K = (int)w._kind,
                T = w._cfg.Title,
                G = w._cfg.Target.ToString("o"),
                R = w._cfg.Range,
                F = w._cfg.DiceFaces,
                P = w._cfg.PomoMinutes,
                X = w.Position.X,
                Y = w.Position.Y,
                Z = w.Topmost,
            }).ToList();
            ConfigManager.Instance.WidgetSession = System.Text.Json.JsonSerializer.Serialize(list);
        }
        catch { }
    }

    public static void RestoreSession()
    {
        try
        {
            var json = ConfigManager.Instance.WidgetSession;
            if (string.IsNullOrWhiteSpace(json)) return;
            var list = System.Text.Json.JsonSerializer.Deserialize<List<SessionItem>>(json);
            if (list == null) return;
            foreach (var it in list)
            {
                var cfg = new Config { Title = it.T, Range = it.R, DiceFaces = it.F, PomoMinutes = it.P };
                if (DateTime.TryParse(it.G, out var g)) cfg.Target = g;
                var w = new SimpleWidgetWindow((Kind)it.K, cfg) { Topmost = it.Z };
                w.Position = new Avalonia.PixelPoint(it.X, it.Y);
                w.Show();
            }
        }
        catch { }
    }

    // ===== 性能检测（与其他小组件同一实现/样式） =====
    private System.Diagnostics.PerformanceCounter? _pCpu, _pNet;
    private long _pLastBytes;
    private long _lastPerfTick;

    private void InitPerf()
    {
        if (!OperatingSystem.IsWindows()) return;
        _ = System.Threading.Tasks.Task.Run(() =>
        {
            try
            {
                var cpu = new System.Diagnostics.PerformanceCounter("Processor", "% Processor Time", "_Total");
                System.Diagnostics.PerformanceCounter? net = null;
                try
                {
                    var names = new System.Diagnostics.PerformanceCounterCategory("Network Interface").GetInstanceNames();
                    if (names.Length > 0)
                        net = new System.Diagnostics.PerformanceCounter("Network Interface", "Bytes Total/sec", names[0]);
                }
                catch { }
                Avalonia.Threading.Dispatcher.UIThread.Post(() => { _pCpu = cpu; _pNet = net; });
            }
            catch { }
        });
    }

    private void TickPerf()
    {
        if (Environment.TickCount64 - _lastPerfTick < 2000) return; // 2 秒节流
        _lastPerfTick = Environment.TickCount64;
        try
        {
            if (_pCpu != null) _big.Text = $"CPU  {_pCpu.NextValue():F0}%";
            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            var memPart = GlobalMemoryStatusEx(ref mem) ? $"内存 {mem.dwMemoryLoad}%" : "内存 --";
            var netPart = "网速 --";
            if (_pNet != null)
            {
                var b = (long)_pNet.NextValue();
                var d = Math.Max(0, b - _pLastBytes);
                _pLastBytes = b;
                netPart = d switch
                {
                    >= 1024 * 1024 => $"网速 {d / 1024.0 / 1024:0.##} MB/s",
                    >= 1024 => $"网速 {d / 1024.0:0.#} KB/s",
                    _ => $"网速 {d} B/s",
                };
            }
            _sub.Text = $"{memPart} · {netPart}";
        }
        catch { }
    }

    /// <summary>某类小组件是否已开</summary>
    public static bool HasOpen(Kind k) => _all.Any(w => w._kind == k);

    private static Avalonia.Threading.DispatcherTimer? _autoSave;

    /// <summary>每 10 秒自动保存会话（不依赖 Closed——进程退出/被杀也能恢复）</summary>
    private static void EnsureAutoSave()
    {
        if (_autoSave != null) return;
        _autoSave = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) };
        _autoSave.Tick += (_, _) => SaveSession();
        _autoSave.Start();
    }

    private static void SetForegrounds(object? o, IBrush brush)
    {
        if (o is TextBlock tb) tb.Foreground = brush;
        if (o is TextBox tbox) tbox.Foreground = brush; // 计算器输入框
        if (o is Button btn && (btn.Content is string || btn.Content is TextBlock)) btn.Foreground = brush;
        if (o is Avalonia.LogicalTree.ILogical l)
            foreach (var c in l.LogicalChildren) SetForegrounds(c, brush);
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // ===== 开源通行实现：直接 SetWindowCompositionAttribute 启用窗口级亚克力模糊 =====
    // （Avalonia 的 AcrylicBlur hint 在 Win11 22631+ 失效：https://github.com/AvaloniaUI/Avalonia/issues/17684
    //   PicView 等开源项目均用此 P/Invoke 方案：ACCENT_ENABLE_ACRYLICBLURBEHIND）
    private enum AccentState { DISABLED = 0, GRADIENT = 1, TRANSPARENTGRADIENT = 2, BLURBEHIND = 3, ACRYLICBLURBEHIND = 4 }
    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy { public AccentState AccentState; public int AccentFlags; public uint GradientColor; public int AnimationId; }
    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData { public int Attribute; public IntPtr Data; public int SizeOfData; }
    [DllImport("user32.dll")]
    private static extern int SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);

    /// <summary>窗口级亚克力模糊（真实模糊窗口背后的桌面内容）。tint 传 null 关闭。</summary>
    public static void EnableBlurBehind(Window w, string? argbTint)
    {
        try
        {
            var hwnd = w.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd == IntPtr.Zero) return;
            var ap = new AccentPolicy();
            if (argbTint != null)
            {
                var c = Avalonia.Media.Color.Parse(argbTint); // #AARRGGBB
                ap.AccentState = AccentState.ACRYLICBLURBEHIND;
                ap.GradientColor = (uint)((c.A << 24) | (c.B << 16) | (c.G << 8) | c.R); // Win32 要 ABGR
            }
            var size = Marshal.SizeOf<AccentPolicy>();
            var data = new WindowCompositionAttributeData
            {
                Attribute = 19 /* WCA_ACCENT_POLICY */,
                Data = Marshal.AllocHGlobal(size),
                SizeOfData = size,
            };
            Marshal.StructureToPtr(ap, data.Data, false);
            SetWindowCompositionAttribute(hwnd, ref data);
            Marshal.FreeHGlobal(data.Data);
        }
        catch { }
    }

    // ===== 液态玻璃：实时抓取窗口背后屏幕 → 模糊 → 贴回（无方框、真演算） =====
    private Image? _backdrop;
    private Border? _scrim;
    private Border? _topLight;
    private Controls.LiquidGlass? _liquid;
    private System.Timers.Timer? _liquidTimer;

    private void StartLiquid()
    {
        if (_backdrop != null) _backdrop.Opacity = 1;
        if (_scrim != null) _scrim.Opacity = 1;
        if (_liquidTimer != null) return;
        _liquidTimer = new System.Timers.Timer(150) { AutoReset = true };
        _liquidTimer.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateBackdrop);
        _liquidTimer.Start();
        UpdateBackdrop();
    }

    private void StopLiquid()
    {
        _liquidTimer?.Stop(); _liquidTimer = null;
        if (_backdrop != null) _backdrop.Opacity = 0;
        if (_scrim != null) _scrim.Opacity = 0;
    }

    private void UpdateBackdrop()
    {
        try
        {
            if (!IsVisible || _backdrop == null) return;
            var tl = Position; // 窗口左上角（设备像素）
            var scale = Screens.Primary?.Scaling ?? 1;
            int w = Math.Max(1, (int)(Width * scale)), h = Math.Max(1, (int)(Height * scale));
            if (w > 2000 || h > 2000) return;
            var buf = CaptureScreen(tl.X, tl.Y, w, h);
            if (buf == null) return;
            BoxBlur(buf, w, h, 6); // 双向盒式模糊
            var wb = new Avalonia.Media.Imaging.WriteableBitmap(
                new Avalonia.PixelSize(w, h), new Avalonia.Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);
            using (var l = wb.Lock())
                System.Runtime.InteropServices.Marshal.Copy(buf, 0, l.Address, buf.Length);
            _backdrop.Source = wb;
        }
        catch { }
    }

    /// <summary>GDI 抓屏（指定设备像素区域），返回 BGRA 缓冲</summary>
    private static byte[]? CaptureScreen(int x, int y, int w, int h)
    {
        var scr = GetDC(IntPtr.Zero);
        var mem = CreateCompatibleDC(scr);
        var bmi = new BITMAPINFO { biSize = 40, biWidth = w, biHeight = -h, biPlanes = 1, biBitCount = 32, biCompression = 0 };
        IntPtr ptr = IntPtr.Zero;
        var hbm = CreateDIBSection(scr, ref bmi, 0, out ptr, IntPtr.Zero, 0);
        if (hbm == IntPtr.Zero || ptr == IntPtr.Zero) { DeleteDC(mem); ReleaseDC(IntPtr.Zero, scr); return null; }
        var old = SelectObject(mem, hbm);
        BitBlt(mem, 0, 0, w, h, scr, x, y, 0x00CC0020 /*SRCCOPY*/);
        var buf = new byte[w * h * 4];
        Marshal.Copy(ptr, buf, 0, buf.Length);
        SelectObject(mem, old);
        DeleteObject(hbm); DeleteDC(mem); ReleaseDC(IntPtr.Zero, scr);
        return buf;
    }

    /// <summary>水平+垂直盒式模糊（原地，含提亮高光感）</summary>
    private static void BoxBlur(byte[] b, int w, int h, int r)
    {
        var tmp = new byte[b.Length];
        int div = r * 2 + 1;
        // 水平
        for (int y = 0; y < h; y++)
        {
            int row = y * w * 4;
            for (int c = 0; c < 3; c++)
            {
                int sum = 0;
                for (int i = -r; i <= r; i++) sum += b[row + Math.Clamp(i, 0, w - 1) * 4 + c];
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x * 4 + c] = (byte)(sum / div);
                    sum += b[row + Math.Clamp(x + r + 1, 0, w - 1) * 4 + c]
                         - b[row + Math.Clamp(x - r, 0, w - 1) * 4 + c];
                }
            }
        }
        // 垂直
        for (int x = 0; x < w; x++)
        {
            for (int c = 0; c < 3; c++)
            {
                int sum = 0;
                for (int i = -r; i <= r; i++) sum += tmp[Math.Clamp(i, 0, h - 1) * w * 4 + x * 4 + c];
                for (int y = 0; y < h; y++)
                {
                    b[y * w * 4 + x * 4 + c] = (byte)(sum / div);
                    sum += tmp[Math.Clamp(y + r + 1, 0, h - 1) * w * 4 + x * 4 + c]
                         - tmp[Math.Clamp(y - r, 0, h - 1) * w * 4 + x * 4 + c];
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO { public int biSize, biWidth, biHeight, biPlanes, biBitCount, biCompression; }
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateDIBSection(IntPtr dc, ref BITMAPINFO bmi, uint usage, out IntPtr bits, IntPtr file, uint offset);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dst, int dx, int dy, int w, int h, IntPtr src, int sx, int sy, uint rop);

    private void TryLoadImages()
    {
        // ★ 图库存放在 zip 包内（不解压），从 zip 读条目列表
        try
        {
            _zipEntries = System.IO.Compression.ZipFile.OpenRead(Services.GalleryService.ZipPath)
                .Entries.Where(en => en.Length > 0 &&
                    (en.Name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                     en.Name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                     en.Name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                     en.Name.EndsWith(".webp", StringComparison.OrdinalIgnoreCase) ||
                     en.Name.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase)))
                .Select(en => en.FullName)
                .ToArray();
        }
        catch { _zipEntries = Array.Empty<string>(); }
    }

    private string[]? _zipEntries;

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
                case Kind.Perf: TickPerf(); break;
            }
        }
        catch { }
    }

    private Avalonia.Threading.DispatcherTimer? _pick;

    /// <summary>老虎机式抽取动画：快速跳变 → 平方减速 → 定格（非线性）</summary>
    private void AnimatedPick(string[] pool, Func<string> final, string sub)
    {
        _pick?.Stop();
        var dt = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
        int i = 0;
        dt.Tick += (_, _) =>
        {
            i++;
            if (i >= 16) { dt.Stop(); _big.Text = final(); _sub.Text = sub; return; }
            _big.Text = pool[_rnd.Next(pool.Length)];
            dt.Interval = TimeSpan.FromMilliseconds(40 + i * i); // 平方减速
        };
        dt.Start();
        _pick = dt;
    }

    /// <summary>抛硬币动画：硬币上抛（sin 轨径）+ 两圈旋转，落地定格</summary>
    private void CoinFlip()
    {
        _pick?.Stop();
        var final = _rnd.Next(2) == 0 ? "正面" : "反面";
        _big.RenderTransformOrigin = new Avalonia.RelativePoint(0.5, 0.5, Avalonia.RelativeUnit.Relative);
        var sw = System.Diagnostics.Stopwatch.StartNew();
        const int dur = 900;
        var t = new Avalonia.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        t.Tick += (_, _) =>
        {
            var ms = sw.ElapsedMilliseconds;
            if (ms >= dur)
            {
                t.Stop();
                _big.RenderTransform = new Avalonia.Media.TranslateTransform(0, 0);
                _big.Text = final;
                _sub.Text = "点击再抛";
                return;
            }
            var p = ms / (double)dur;
            var y = -46 * Math.Sin(Math.PI * p); // 上抛再落下
            var rot = 720 * p;                    // 空中转两圈
            _big.Text = (ms / 90) % 2 == 0 ? "正面" : "反面"; // 翻面闪烁
            // 强类型 Transform（动态数值不走字符串解析）
            _big.RenderTransform = new Avalonia.Media.TransformGroup
            {
                Children =
                {
                    new Avalonia.Media.RotateTransform(rot),
                    new Avalonia.Media.TranslateTransform(0, y),
                },
            };
        };
        t.Start();
        _pick = t;
    }

    private void Action()
    {
        switch (_kind)
        {
            case Kind.Dice:
            {
                var faces = _cfg.DiceFaces;
                var pool = Enumerable.Range(1, faces).Select(n => $"{n}").ToArray();
                AnimatedPick(pool, () => $"{_rnd.Next(1, faces + 1)} 点", $"{faces} 面骰 · 点击再掷 · 右键配置");
                break;
            }
            case Kind.Coin:
                CoinFlip();
                break;
            case Kind.Roll:
            {
                var range = _cfg.Range;
                var pool = Enumerable.Range(1, Math.Min(range, 60)).Select(n => $"第 {n} 号").ToArray();
                AnimatedPick(pool, () => $"第 {_rnd.Next(1, range + 1)} 号", $"范围 1-{range} · 点击重抽 · 右键配置");
                break;
            }
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

    private readonly Stack<string> _hist = new();
    private string? _cur;

    private void Shuffle()
    {
        if (_zipEntries == null || _zipEntries.Length == 0) { _sub.Text = "无缓存图（先在涩图合集下载）"; return; }
        ShowEntry(_zipEntries[_rnd.Next(_zipEntries.Length)]);
        _sub.Text = $"共 {_zipEntries.Length} 张 · 左键换图 · 中键上一张";
    }

    /// <summary>上一张（历史栈）</summary>
    private void Prev()
    {
        if (_kind != Kind.Gallery) return;
        if (_hist.Count == 0) { _sub.Text = "没有更早的了"; return; }
        ShowEntry(_hist.Pop());
        _sub.Text = $"回退 · 剩余历史 {_hist.Count} 张 · 中键继续回退";
    }

    private void ShowEntry(string name)
    {
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(Services.GalleryService.ZipPath);
            var entry = zip.GetEntry(name) ?? throw new IOException();
            using var ms = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(ms);
            ms.Position = 0;
            _img!.Source = new Avalonia.Media.Imaging.Bitmap(ms);
            if (_cur != null && _cur != name) _hist.Push(_cur);
            _cur = name;
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
                panel.Children.Add(new TextBlock { Text = $"已重新加载 {_zipEntries?.Length ?? 0} 张图" });
                var prevBtn = new Button { Content = "返回上一张", HorizontalAlignment = HorizontalAlignment.Left };
                prevBtn.Click += (_, _) => { Prev(); w.Close(); };
                panel.Children.Add(prevBtn);
                break;
            default:
                panel.Children.Add(new TextBlock { Text = "该组件无配置项。\n左键=动作 · 标题栏拖动=移动 · 右键=本配置窗。" });
                break;
        }
        panel.Children.Add(ok);

        // ★ 通用：置顶切换（每个小组件独立）
        var topBtn = new Button
        {
            Content = Topmost ? "取消置顶（当前：置顶）" : "设为置顶（当前：不置顶）",
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        topBtn.Click += (_, _) =>
        {
            Topmost = !Topmost;
            topBtn.Content = Topmost ? "取消置顶（当前：置顶）" : "设为置顶（当前：不置顶）";
        };
        panel.Children.Add(topBtn);
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
