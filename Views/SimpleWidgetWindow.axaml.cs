using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SBtools.Views;

/// <summary>
/// 通用桌面小组件窗：时钟/倒计时/涩图/骰子/硬币/点名/一言/电池/计算器。
/// 点击切换内容动作，右键打开该组件的详细配置；材质全局实时生效。
/// </summary>
public partial class SimpleWidgetWindow : Window
{
    public enum Kind { Clock, Countdown, Gallery, Dice, Coin, Roll, Quote, Battery, Calc, Pomodoro }

    public sealed class Config
    {
        public DateTime Target = DateTime.Now.AddDays(30);
        public int Range = 45;          // 点名总人数
        public int DiceFaces = 6;       // 骰子面数
        public int PomoMinutes = 25;    // 番茄钟时长
        public string Title = "";
    }

    // ===== 全局材质（实时广播到所有实例） =====
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
    private readonly TextBlock _big = new() { FontSize = 26, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly TextBlock _sub = new() { FontSize = 11, Opacity = 0.65, HorizontalAlignment = HorizontalAlignment.Center, TextWrapping = TextWrapping.Wrap };
    private readonly Image? _img;
    private string[]? _imgs;
    private readonly Random _rnd = new();
    private System.Timers.Timer? _pomo;
    private DateTime _pomoEnd;
    private readonly TextBox? _calcInput;
    private readonly TextBlock? _calcOut;

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
        Height = kind is Kind.Gallery or Kind.Calc ? 240 : 150;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = true;

        var panel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(new TextBlock { Text = cfg.Title, FontSize = 11, Opacity = 0.7, VerticalAlignment = VerticalAlignment.Center });
        var close = new Button { Content = "✕", FontSize = 10, Padding = new Avalonia.Thickness(5, 1), Opacity = 0.5 };
        Grid.SetColumn(close, 1);
        close.Click += (_, _) => Close();
        head.Children.Add(close);
        panel.Children.Add(head);

        switch (kind)
        {
            case Kind.Gallery:
                _img = new Image { Stretch = Stretch.Uniform, MaxHeight = 165 };
                panel.Children.Add(_img);
                TryLoadImages(); Shuffle();
                break;
            case Kind.Calc:
                _calcInput = new TextBox { Watermark = "如 12*(3+4)/2" };
                _calcOut = new TextBlock { FontSize = 20, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center };
                var calcBtn = new Button { Content = "＝ 计算", HorizontalAlignment = HorizontalAlignment.Center };
                calcBtn.Click += (_, _) => CalcEval();
                _calcInput.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) CalcEval(); };
                panel.Children.Add(_calcInput); panel.Children.Add(calcBtn); panel.Children.Add(_calcOut);
                break;
            case Kind.Pomodoro:
                var startStop = new Button { Content = "开始专注", HorizontalAlignment = HorizontalAlignment.Center };
                startStop.Click += (_, _) => PomoToggle(startStop);
                panel.Children.Add(_big); panel.Children.Add(startStop);
                break;
            default:
                panel.Children.Add(_big);
                break;
        }
        panel.Children.Add(_sub);
        _card.Child = panel;
        Content = _card;
        ApplyMaterial();

        // 左键：动作（掷/换图/换一言）；拖动：移动；右键：详细配置
        PointerPressed += (_, e) =>
        {
            var p = e.GetCurrentPoint(this);
            if (e.GetCurrentPoint(this).Properties.IsRightButtonPressed) OpenConfig();
            else if (p.Properties.IsLeftButtonPressed)
            {
                var onBig = _big.IsPointerOver || (_img?.IsPointerOver ?? false) || (_calcOut?.IsPointerOver ?? false);
                if (onBig) Action(); else BeginMoveDrag(e);
            }
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
        if (kind != Kind.Countdown && kind != Kind.Quote && kind != Kind.Roll && kind != Kind.Dice && kind != Kind.Coin && kind != Kind.Battery) Action();
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
                    if (left < TimeSpan.Zero) { _big.Text = "🎉"; _sub.Text = "目标已到！"; }
                    else
                    {
                        _big.Text = $"{left.Days}天 {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
                        _sub.Text = $"目标 {_cfg.Target:yyyy-M-d HH:mm} · 点击无动作";
                    }
                    break;
                case Kind.Battery:
                    if (_cfg.Title == "C 盘")
                    {
                        var c = new System.IO.DriveInfo("C");
                        var free = c.TotalFreeSpace / 1024.0 / 1024 / 1024;
                        _big.Text = $"💽 {free:F0} GB";
                        _sub.Text = $"C 盘剩余 / 共 {c.TotalSize / 1024.0 / 1024 / 1024:F0} GB";
                    }
                    else if (_cfg.Title == "内存占用")
                    {
                        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
                        if (GlobalMemoryStatusEx(ref mem))
                        {
                            _big.Text = $"📈 {mem.dwMemoryLoad}%";
                            _sub.Text = $"已用 {mem.ullTotalPhys / 1024.0 / 1024 / 1024 * (100 - mem.dwMemoryLoad) / 100:F1} / {mem.ullTotalPhys / 1024.0 / 1024 / 1024:F1} GB";
                        }
                    }
                    else
                    {
                        var s = new SYSTEM_POWER_STATUS();
                        if (GetSystemPowerStatus(s))
                        {
                            _big.Text = $"🔋 {s.BatteryLifePercent}%";
                            _sub.Text = s.ACLineStatus == 1 ? "充电中（交流电）" : "使用电池";
                        }
                    }
                    break;
                case Kind.Pomodoro when _pomo != null:
                    var l = _pomoEnd - DateTime.Now;
                    _big.Text = l > TimeSpan.Zero ? $"{(int)l.TotalMinutes:00}:{l.Seconds:00}" : "✅ 完成！";
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
                _big.Text = $"🎲 {_rnd.Next(1, _cfg.DiceFaces + 1)}";
                _sub.Text = $"{_cfg.DiceFaces} 面骰 · 点击再掷 · 右键配置";
                break;
            case Kind.Coin:
                _big.Text = _rnd.Next(2) == 0 ? "🪙 正面" : "🪙 反面";
                _sub.Text = "点击再抛";
                break;
            case Kind.Roll:
                _big.Text = $"👦 第 {_rnd.Next(1, _cfg.Range + 1)} 号";
                _sub.Text = $"全班 {_cfg.Range} 人 · 点击重抽 · 右键配置";
                break;
            case Kind.Quote:
                var qs = new[] { "行到水穷处，坐看云起时。", "日拱一卒，功不唐捐。", "凡是过往，皆为序章。", "山不让尘，川不辞盈。", "追风赶月莫停留，平芜尽处是春山。", "路虽远，行则将至。", "不积跬步，无以至千里。" };
                _big.FontSize = 15; _big.Text = qs[_rnd.Next(qs.Length)];
                _sub.Text = "点击换一句";
                break;
            case Kind.Gallery: Shuffle(); break;
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

    private void CalcEval()
    {
        try
        {
            var r = new System.Data.DataTable().Compute(_calcInput!.Text ?? "", null);
            _calcOut!.Text = r?.ToString() ?? "?";
            _sub.Text = "支持 + - * / % 括号";
        }
        catch (Exception ex) { _calcOut!.Text = "表达式错误"; _sub.Text = ex.Message; }
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
                if (DateTime.Now >= _pomoEnd) { _pomo?.Stop(); _pomo = null; btn.Content = "开始专注"; }
            });
            _pomo.Start();
            btn.Content = "放弃";
            _sub.Text = $"专注 {_cfg.PomoMinutes} 分钟 · 右键配置时长";
        }
        else { _pomo.Stop(); _pomo = null; btn.Content = "开始专注"; _big.Text = "已停止"; }
    }

    /// <summary>右键：该组件的详细配置窗</summary>
    private void OpenConfig()
    {
        var panel = new StackPanel { Margin = new Avalonia.Thickness(16), Spacing = 10 };
        var w = new Window
        {
            Title = $"{_cfg.Title} · 配置", Width = 320, Height = 220,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };
        TextBox tb1 = new() { Watermark = "参数 1" };
        TextBox tb2 = new() { Watermark = "参数 2" };
        var ok = new Button { Content = "保存并刷新", HorizontalAlignment = HorizontalAlignment.Right };
        switch (_kind)
        {
            case Kind.Countdown:
                panel.Children.Add(new TextBlock { Text = "目标时间（格式 2026-06-07 09:00）" });
                tb1.Text = _cfg.Target.ToString("yyyy-MM-dd HH:mm");
                panel.Children.Add(tb1);
                panel.Children.Add(new TextBlock { Text = "提前提醒天数（可留空）", Opacity = 0.6 });
                panel.Children.Add(tb2);
                break;
            case Kind.Roll:
                panel.Children.Add(new TextBlock { Text = "总人数" });
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
            case Kind.Calc:
                panel.Children.Add(new TextBlock { Text = "支持 + - * / % 与括号，回车即算。无额外配置。" });
                break;
            default:
                panel.Children.Add(new TextBlock { Text = "该组件无配置项。\n左键=动作，标题栏✕=关闭，拖动=移动。" });
                break;
        }
        panel.Children.Add(ok);
        ok.Click += (_, _) =>
        {
            try
            {
                switch (_kind)
                {
                    case Kind.Countdown:
                        if (DateTime.TryParse(tb1.Text, out var dt)) _cfg.Target = dt;
                        break;
                    case Kind.Roll:
                        if (int.TryParse(tb1.Text, out var n) && n > 0) _cfg.Range = n;
                        break;
                    case Kind.Dice:
                        if (int.TryParse(tb1.Text, out var f) && f > 1) _cfg.DiceFaces = f;
                        break;
                    case Kind.Pomodoro:
                        if (int.TryParse(tb1.Text, out var m) && m > 0) _cfg.PomoMinutes = m;
                        break;
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
