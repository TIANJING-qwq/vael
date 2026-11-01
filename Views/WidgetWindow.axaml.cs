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

    /// <summary>应用完整材质（云母/亚克力系统背景+反色），与小组件同源</summary>
    public void ApplyMaterial(SimpleWidgetWindow.MatInfo m)
    {
        SimpleWidgetWindow.EnableBlurBehind(this, null); // 清 ACCENT 残留
        try
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd != IntPtr.Zero)
            {
                var bd = m.Backdrop; // 1=云母 2=亚克力（与主界面同系统实现）
                _ = DwmSetWindowAttribute(hwnd, 38, ref bd, 4);
                int pref = 2;
                _ = DwmSetWindowAttribute(hwnd, 33, ref pref, 4);
            }
        }
        catch { }

        Card.Background = Avalonia.Media.Brush.Parse(m.CardHex);
        var fg = m.ForeBrush;
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
        // ★ 打开即应用当前材质（与其他小组件一致，不再每次默认黑卡）
        try { ApplyMaterial(SimpleWidgetWindow.Material); } catch { }
        Closed += (_, _) => { if (Instance == this) Instance = null; };
        Position = new Avalonia.PixelPoint(
            (int)(Screens.Primary?.WorkingArea.Width - 260 ?? 1200),
            (int)(Screens.Primary?.WorkingArea.Height - 190 ?? 700));

        // 拖动
        PointerPressed += (_, e) => { if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e); };

        if (OperatingSystem.IsWindows())
        {
            // ★ 计数器创建（含网络实例枚举）很慢 → 后台线程，UI 不卡顿，就绪前显示 --
            _ = Task.Run(() =>
            {
                try
                {
                    var cpu = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    var net = new PerformanceCounter("Network Interface", "Bytes Total/sec", GetNetInstance());
                    Avalonia.Threading.Dispatcher.UIThread.Post(() => { _cpu = cpu; _net = net; });
                }
                catch { }
            });
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
