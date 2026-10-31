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
    private PerformanceCounter? _cpu;
    private PerformanceCounter? _net;
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _lastBytes;
    private bool _animDone;

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX { public uint dwLength, dwMemoryLoad; public ulong ullTotalPhys, ullAvailPhys, ullAvailVirtual, ullTotalVirtual, ullAvailExtendedVirtual; }
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
                var d = b - _lastBytes;
                _lastBytes = b;
                NetText.Text = d >= 0
                    ? $"网速 {(d / 1024.0 / 2):F1} KB/s"
                    : "网速 --";
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
