using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Animation;
using SBtools.Models;

namespace SBtools.Views;

public partial class WidgetWindow : Window
{
    /// <summary>当前实例（主窗口开关用）</summary>
    public static WidgetWindow? Instance;

    /// <summary>应用完整材质（模糊/液态玻璃/字色反色），由 SimpleWidgetWindow.Material 广播</summary>
    public void ApplyMaterial(SimpleWidgetWindow.MatInfo m)
    {
        var blur = m.Mode != "solid";
        // 窗口级亚克力模糊（与小组件同一 Win32 方案）
        SimpleWidgetWindow.EnableBlurBehind(this, blur ? m.TintHex : null);

        // DWM 圆角，消除系统模糊层方形感
        if (OperatingSystem.IsWindows())
        {
            try
            {
                var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
                if (hwnd != IntPtr.Zero) { int pref = 2; _ = DwmSetWindowAttribute(hwnd, 33, ref pref, 4); }
            }
            catch { }
        }

        // 卡片底：液态玻璃=多层渐变，其余=纯色
        Card.Background = m.Mode == "liquid"
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
            : Avalonia.Media.Brush.Parse(m.CardHex);

        // 字色按材质亮度反色（浅材质黑字，深材质白字）
        var hex = m.CardHex;
        var r = Convert.ToInt32(hex.Substring(1, 2), 16);
        var g = Convert.ToInt32(hex.Substring(3, 2), 16);
        var b = Convert.ToInt32(hex.Substring(5, 2), 16);
        var fg = r + g + b < 330 ? Brushes.White : Brushes.Black;
        SetFg(this);
        void SetFg(object? o)
        {
            if (o is TextBlock tb) tb.Foreground = fg;
            if (o is Button btn && (btn.Content is string || btn.Content is TextBlock)) btn.Foreground = fg;
            if (o is Avalonia.LogicalTree.ILogical l)
                foreach (var c in l.LogicalChildren) SetFg(c);
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    private PerformanceCounter? _cpu;
    private PerformanceCounter? _net;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _lastBytes;
    private bool _animDone;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

    public WidgetWindow()
    {
        InitializeComponent();
        Instance = this;
        Closed += (_, _) => { if (Instance == this) Instance = null; };
        Position = new Avalonia.PixelPoint(
            (int)(Screens.Primary?.WorkingArea.Width - 260 ?? 1200),
            (int)(Screens.Primary?.WorkingArea.Height - 190 ?? 700));

        // 拖动
        PointerPressed += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); };

        if (OperatingSystem.IsWindows())
        {
            try
            {
                _cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _net = new PerformanceCounter("Network Interface", "Bytes Total/sec", GetNetInstance());
            }
            catch { }
        }

        var t = new System.Timers.Timer(2000) { AutoReset = true };
        t.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Refresh);
        t.Start();
        Refresh();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        // ★ 自启动入场动画：透明 0.6 + 缩放 0.92 → 1（非线性，无过冲）
        if (_animDone) return;
        _animDone = true;
        Card.Transitions = new Transitions
        {
            new DoubleTransition { Property = OpacityProperty, Duration = TimeSpan.FromMilliseconds(350), Easing = new Avalonia.Animation.Easings.ExponentialEaseOut() },
            new TransformOperationsTransition { Property = RenderTransformProperty, Duration = TimeSpan.FromMilliseconds(420), Easing = new Avalonia.Animation.Easings.ExponentialEaseOut() },
        };
        Opacity = 0.6;
        Card.RenderTransform = TransformOperations.Parse("scale(0.92)");
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            Opacity = 1;
            Card.RenderTransform = TransformOperations.Parse("scale(1)");
        }, Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private static string GetNetInstance()
    {
        var cat = new PerformanceCounterCategory("Network Interface");
        var names = cat.GetInstanceNames();
        return names.Length > 0 ? names[0] : "";
    }

    private void Refresh()
    {
        try
        {
            if (_cpu != null) CpuText.Text = $"CPU  {_cpu.NextValue():F0}%";

            var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref mem))
                MemText.Text = $"内存 {mem.dwMemoryLoad}%";

            if (_net != null && _sw.Elapsed.TotalSeconds > 2)
            {
                var b = (long)_net.NextValue();
                var d = Math.Max(0, b - _lastBytes);
                _lastBytes = b;
                // 自动换单位
                NetText.Text = d switch
                {
                    >= 1024 * 1024 => $"网速 {d / 1024.0 / 1024:0.##} MB/s",
                    >= 1024 => $"网速 {d / 1024.0:0.#} KB/s",
                    _ => $"网速 {d} B/s",
                };
            }
        }
        catch { }
    }

    private void CloseBtn_Click(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        ConfigManager.Instance.DesktopWidgetEnabled = false;
        Close();
    }
}
