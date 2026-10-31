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

    // ===== 材质系统：mode = solid(纯色) / acrylic(系统模糊) / liquid(亚克力+高光边) =====
    public sealed record MatInfo(string Name, string CardHex, string TintHex, string Mode)
    {
        public bool Dark => Convert.ToInt32(CardHex.Substring(1, 2), 16) +
                            Convert.ToInt32(CardHex.Substring(3, 2), 16) +
                            Convert.ToInt32(CardHex.Substring(5, 2), 16) < 330;
        public Avalonia.Media.IBrush ForeBrush => Dark ? Brushes.White : Brushes.Black;
    }

    public static readonly MatInfo[] Materials =
    {
        new("曜石黑", "#D0202020", "#00000000", "solid"),
        new("云雾白", "#E0F5F5F5", "#00000000", "solid"),
        new("薄荷",   "#D01F3D3B", "#00000000", "solid"),
        new("紫罗兰", "#D03B2A6E", "#00000000", "solid"),
        new("玻璃",   "#30FFFFFF", "#50202020", "acrylic"),
        new("毛玻璃", "#60303030", "#90303030", "acrylic"),
        new("亚克力", "#40FFFFFF", "#B0202020", "acrylic"),
        new("液态玻璃", "#50F0F4FF", "#80505060", "liquid"),
    };

    private static MatInfo _mat = Materials[0];
    public static MatInfo Material
    {
        get => _mat;
        set { _mat = value; foreach (var w in _all) w.ApplyMaterial(); try { WidgetWindow.Instance?.ApplyMaterial(value); } catch { } }
    }
    // 兼容旧接口
    public static string MaterialHex { get => _mat.CardHex; set => Material = Materials.FirstOrDefault(m => m.CardHex == value) ?? Materials[0]; }
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
        // 液态玻璃：背景抓图层 + 可读性纱层 + 内容层
        _backdrop = new Image { Stretch = Stretch.Fill, Opacity = 0 };
        _scrim = new Border { Opacity = 0 }; // 压暗纱层，保证文字对比度
        var grid = new Grid();
        grid.Children.Add(_backdrop);
        grid.Children.Add(_scrim);
        grid.Children.Add(panel);
        _card.Child = grid;
        Content = _card;
        ApplyMaterial();

        // 窗体：左键空白=拖动 / 右键=配置（控件自身动作已 Handled，不会到这）
        PointerPressed += (_, e) =>
        {
            var pt = e.GetCurrentPoint(this);
            if (pt.Properties.IsRightButtonPressed) { OpenConfig(); e.Handled = true; }
            else if (pt.Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };

        _all.Add(this);
        Closed += (_, _) => { _all.Remove(this); StopLiquid(); };
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
        var blur = _mat.Mode != "solid"; // 亚克力/液态玻璃：窗口级系统模糊
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        if (_mat.Mode == "liquid")
        {
            // ★ 液态玻璃：关闭系统模糊（消灭方形边），改用实时抓屏演算
            EnableBlurBehind(this, null);
            StartLiquid();
        }
        else
        {
            EnableBlurBehind(this, blur ? _mat.TintHex : null);
            StopLiquid();
        }
        _card.Background = _mat.Mode == "liquid"
            // 液态玻璃：多层对角渐变（左上高光 → 中段镜面 → 右下亮边），即使系统模糊不可用也有玻璃质感
            ? new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Avalonia.Media.Color.Parse("#90FFFFFF"), 0),
                    new GradientStop(Avalonia.Media.Color.Parse("#40505060"), 0.35),
                    new GradientStop(Avalonia.Media.Color.Parse("#20404050"), 0.6),
                    new GradientStop(Avalonia.Media.Color.Parse("#70F0F4FF"), 1),
                },
            }
            : new SolidColorBrush(Avalonia.Media.Color.Parse(_mat.CardHex));

        // 液态玻璃：白色渐变高光描边；其余半透明白描边
        _card.BorderBrush = _mat.Mode == "liquid"
            ? new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(1, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Avalonia.Media.Color.Parse("#C0FFFFFF"), 0),
                    new GradientStop(Avalonia.Media.Color.Parse("#30FFFFFF"), 0.5),
                    new GradientStop(Avalonia.Media.Color.Parse("#A0FFFFFF"), 1),
                },
            }
            : Brush.Parse("#33FFFFFF");
        _card.BorderThickness = new Avalonia.Thickness(_mat.Mode == "liquid" ? 1.5 : 1);

        // ★ 字色反色：液态玻璃压暗纱层+白字（背景是实时抓屏，对比度必须靠纱层保证）；
        //   其余按材质亮度反色（白底黑字 / 深底白字）
        var fore = _mat.Mode == "liquid" ? Brushes.White : _mat.ForeBrush;
        if (_scrim != null)
            _scrim.Background = _mat.Mode == "liquid"
                ? new SolidColorBrush(Avalonia.Media.Color.Parse("#66000000"))
                : Brushes.Transparent;
        SetForegrounds(_card, fore);

        // DWM 圆角（Win11：消除系统模糊层的方形感）
        if (blur && OperatingSystem.IsWindows())
        {
            try
            {
                var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd != IntPtr.Zero)
                {
                    int pref = 2; // DWMWCP_ROUND
                    _ = DwmSetWindowAttribute(hwnd, 33, ref pref, 4);
                }
            }
            catch { }
        }
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
        if (_zipEntries == null || _zipEntries.Length == 0) { _sub.Text = "无缓存图（先在涩图合集下载）"; return; }
        try
        {
            using var zip = System.IO.Compression.ZipFile.OpenRead(Services.GalleryService.ZipPath);
            var entry = zip.GetEntry(_zipEntries[_rnd.Next(_zipEntries.Length)]);
            if (entry == null) return;
            using var ms = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(ms);
            ms.Position = 0;
            _img!.Source = new Avalonia.Media.Imaging.Bitmap(ms);
            _sub.Text = $"共 {_zipEntries.Length} 张 · 点击换图";
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
