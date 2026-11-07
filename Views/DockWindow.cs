using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Newtonsoft.Json;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using IOPath = System.IO.Path;

namespace SBtools.Views;

/// <summary>
/// AquaDesk 风 Dock：底部玻璃程序坞。
/// 图标悬停放大 + 邻居让位（高斯衰减曲线）、运行指示条、点击启动；
/// 液态玻璃视觉与小组件材质联动；状态独立持久化。
/// 左键空白拖动 / 右键配置 / 点击图标启动。
/// </summary>
public class DockWindow : Window
{
    public static DockWindow? Instance { get; private set; }

    // ===== 持久化状态 =====
    public sealed class DockState
    {
        public List<DockApp> Apps { get; set; } = new();
        public int X { get; set; } = -1;
        public int Y { get; set; } = -1;
        public bool Topmost { get; set; } = true;
        public bool Opened { get; set; }
    }
    public sealed class DockApp
    {
        public string Path { get; set; } = "";
        public string Label { get; set; } = "";
    }

    private static DockState _state = LoadState();
    private static readonly string StatePath =
        IOPath.Combine(ConfigManager.AppDataDir, "dock_state.json");

    private static DockState LoadState()
    {
        try
        {
            if (File.Exists(StatePath))
                return JsonConvert.DeserializeObject<DockState>(File.ReadAllText(StatePath)) ?? new DockState();
        }
        catch { }
        // 首次使用：预置几个系统应用
        var s = new DockState();
        s.Apps.Add(new DockApp { Path = Environment.SystemDirectory + "\\notepad.exe", Label = "记事本" });
        s.Apps.Add(new DockApp { Path = Environment.SystemDirectory + "\\calc.exe", Label = "计算器" });
        s.Apps.Add(new DockApp { Path = Environment.GetFolderPath(Environment.SpecialFolder.Windows) + "\\explorer.exe", Label = "资源管理器" });
        return s;
    }

    private static void SaveState() { try { Directory.CreateDirectory(IOPath.GetDirectoryName(StatePath)!); File.WriteAllText(StatePath, JsonConvert.SerializeObject(_state, Formatting.Indented)); } catch { } }

    public static bool ShouldRestore => _state.Opened;

    // ===== 图标提取（Windows）：SHGetFileInfo → PNG 缓存 =====
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon; public int iIcon; public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string DisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)] public string TypeName;
    }
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);
    [DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr hIcon);
    private const uint SHGFI_ICON = 0x100, SHGFI_LARGEICON = 0x0;

    private static readonly string IconCacheDir = IOPath.Combine(ConfigManager.AppDataDir, "dock_icons");

    private static IImage? LoadAppIcon(string exePath)
    {
        try
        {
            if (!OperatingSystem.IsWindows() || !File.Exists(exePath)) return null;
            Directory.CreateDirectory(IconCacheDir);
            var cache = IOPath.Combine(IconCacheDir,
                (Convert.ToHexString(System.Text.Encoding.UTF8.GetBytes(exePath.ToLowerInvariant())) + ".png")[..24]);
            if (File.Exists(cache)) return new Bitmap(cache);

            var shfi = new SHFILEINFO();
            var h = SHGetFileInfo(exePath, 0, ref shfi, (uint)Marshal.SizeOf<SHFILEINFO>(), SHGFI_ICON | SHGFI_LARGEICON);
            if (h == IntPtr.Zero || shfi.hIcon == IntPtr.Zero) return null;
            using (var icon = System.Drawing.Icon.FromHandle(shfi.hIcon))
            using (var bmp = icon.ToBitmap())
            {
                bmp.Save(cache, System.Drawing.Imaging.ImageFormat.Png);
            }
            DestroyIcon(shfi.hIcon);
            return new Bitmap(cache);
        }
        catch { return null; }
    }

    // ===== UI =====
    private readonly Border _card;
    private readonly StackPanel _strip;
    private readonly List<IconButton> _buttons = new();
    private readonly Avalonia.Threading.DispatcherTimer _anim = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private readonly Avalonia.Threading.DispatcherTimer _runCheck = new() { Interval = TimeSpan.FromSeconds(2) };
    private double _mouseX = double.NaN;

    private sealed class IconButton : Button
    {
        public required DockApp App;
        public required Ellipse Dot;
        public double CurScale = 1, CurLift;
        public required ScaleTransform Scale;
        public required TranslateTransform Lift;
        public Avalonia.Media.IImage? Icon;
    }

    public DockWindow()
    {
        Instance = this;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = _state.Topmost;
        Title = "Aqua Dock";

        _card = new Border
        {
            CornerRadius = new CornerRadius(18),
            Padding = new Thickness(10, 6, 10, 8),
            ClipToBounds = false, // ★ 图标放大可越出卡片上缘（mac 行为）
        };
        _strip = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6, VerticalAlignment = VerticalAlignment.Bottom };
        _card.Child = _strip;
        // ★ 修穿模：卡片上方预留 46px 透明区，放大 1.55x 的图标有地方长，不会被窗口边界裁掉
        Content = new Grid { Margin = new Thickness(0, 46, 0, 0), Children = { _card } };

        RebuildIcons();
        ApplyMaterial(SimpleWidgetWindow.Material);
        SimpleWidgetWindow.MaterialChanged += ApplyMaterial;

        // 尺寸随图标数自适应
        SizeToContent = SizeToContent.WidthAndHeight;
        if (_state.X >= 0 && _state.Y >= 0)
        {
            Position = new PixelPoint(_state.X, _state.Y);
            WindowStartupLocation = WindowStartupLocation.Manual;
        }
        else
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(
                Math.Max(60, (int)((Screens.Primary?.WorkingArea.Width ?? 1400) / 2 - 260)),
                (Screens.Primary?.WorkingArea.Bottom ?? 900) - 110 - 46); // 46=顶部图标放大预留区
        }

        // 拖动：空白处左键
        PointerPressed += (_, e) =>
        {
            var pt = e.GetCurrentPoint(this);
            if (pt.Properties.IsRightButtonPressed) { OpenConfig(); e.Handled = true; }
            else if (pt.Properties.IsLeftButtonPressed) BeginMoveDrag(e);
        };
        PointerMoved += (_, e) => _mouseX = e.GetPosition(_strip).X;
        PointerExited += (_, _) => _mouseX = double.NaN;

        _anim.Tick += (_, _) => AnimateMagnify();
        _anim.Start();
        _runCheck.Tick += (_, _) => UpdateRunningDots();
        _runCheck.Start();

        Closing += (_, _) =>
        {
            _state.X = Position.X; _state.Y = Position.Y;
            _state.Topmost = Topmost; _state.Opened = false;
            SaveState();
        };
        Closed += (_, _) =>
        {
            SimpleWidgetWindow.MaterialChanged -= ApplyMaterial;
            _anim.Stop(); _runCheck.Stop();
            if (Instance == this) Instance = null;
        };
        Opened += (_, _) => { _state.Opened = true; SaveState(); };
    }

    /// <summary>小组件页 / 会话恢复入口：已开则聚焦，未开则创建</summary>
    public static DockWindow ShowOrCreate()
    {
        if (Instance != null) { Instance.Activate(); return Instance; }
        var w = new DockWindow();
        w.Show();
        return w;
    }

    // ============================================================
    // 图标条
    // ============================================================
    private void RebuildIcons()
    {
        _strip.Children.Clear();
        _buttons.Clear();

        foreach (var app in _state.Apps)
        {
            var icon = LoadAppIcon(app.Path);
            var dot = new Ellipse
            {
                Width = 4, Height = 4,
                Fill = Brushes.White, Opacity = 0,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            var img = new Image
            {
                Width = 40, Height = 40,
                Source = icon,
                IsVisible = icon != null,
            };
            var glyph = new TextBlock
            {
                Text = "\uE7B8", FontSize = 26, IsVisible = icon == null,
                FontFamily = new FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets"),
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            };
            var cell = new Grid { Width = 44, Height = 44, VerticalAlignment = VerticalAlignment.Bottom };
            cell.Children.Add(img);
            cell.Children.Add(glyph);
            cell.Children.Add(new Border { Child = dot, VerticalAlignment = VerticalAlignment.Bottom, HorizontalAlignment = HorizontalAlignment.Center, Height = 6 });

            var btn = new IconButton
            {
                App = app, Dot = dot, Icon = icon,
                Content = cell,
                Scale = new ScaleTransform(1, 1),
                Lift = new TranslateTransform(0, 0),
                Padding = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Bottom,
            };
            ToolTip.SetTip(btn, app.Label);
            btn.RenderTransformOrigin = new RelativePoint(0.5, 1, RelativeUnit.Relative); // 底部为轴：放大向上长
            btn.RenderTransform = new TransformGroup { Children = { btn.Scale, btn.Lift } };
            btn.Click += (_, _) =>
            {
                try { Process.Start(new ProcessStartInfo(app.Path) { UseShellExecute = true }); }
                catch (Exception ex) { MainWindow.PushToast("Dock", $"启动失败：{ex.Message}", 4); }
            };
            _strip.Children.Add(btn);
            _buttons.Add(btn);
        }

        // 「+ 添加」按钮
        var add = new Button
        {
            Content = new TextBlock
            {
                Text = "+", FontSize = 22, Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center,
            },
            Width = 44, Height = 44,
            Padding = new Thickness(0),
            CornerRadius = new CornerRadius(10),
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Bottom,
        };
        add.Click += (_, _) => OpenConfig();
        _strip.Children.Add(add);
        UpdateRunningDots();
    }

    /// <summary>悬停放大 + 邻居让位：高斯衰减（AquaDesk 式鱼眼），帧间平滑逼近</summary>
    private void AnimateMagnify()
    {
        const double sigma = 58;      // 影响半径
        const double maxScale = 0.55; // 最大增幅
        const double maxLift = 15;

        foreach (var b in _buttons)
        {
            double targetScale = 1, targetLift = 0;
            if (!double.IsNaN(_mouseX) && b.Bounds.Width > 0)
            {
                var center = b.Bounds.X + b.Bounds.Width / 2;
                var d = (center - _mouseX) / sigma;
                var g = Math.Exp(-d * d);
                targetScale = 1 + maxScale * g;
                targetLift = -maxLift * g;
            }
            b.CurScale += (targetScale - b.CurScale) * 0.28; // 非线性逼近
            b.CurLift += (targetLift - b.CurLift) * 0.28;
            b.Scale.ScaleX = b.Scale.ScaleY = b.CurScale;
            b.Lift.Y = b.CurLift;
        }
    }

    private void UpdateRunningDots()
    {
        try
        {
            var running = new HashSet<string>(
                Process.GetProcesses().Select(SafeName),
                StringComparer.OrdinalIgnoreCase);
            foreach (var b in _buttons)
            {
                var n = IOPath.GetFileNameWithoutExtension(b.App.Path);
                b.Dot.Opacity = running.Contains(n) ? 0.85 : 0;
            }
        }
        catch { }
    }

    private static string SafeName(Process p)
    {
        try { return IOPath.GetFileNameWithoutExtension(p.ProcessName); } catch { return ""; }
    }

    // ============================================================
    // 材质（液态玻璃分层）
    // ============================================================
    private void ApplyMaterial(SimpleWidgetWindow.MatInfo m)
    {
        SimpleWidgetWindow.EnableBlurBehind(this, m.Backdrop == 2 ? "#99202020" : null);
        try
        {
            var hwnd = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (hwnd != IntPtr.Zero)
            {
                var bd = 1;
                if (m.Backdrop == 1) _ = DwmSetWindowAttribute(hwnd, 38, ref bd, 4);
                int pref = 2;
                _ = DwmSetWindowAttribute(hwnd, 33, ref pref, 4);
                // ★ 去白框：DWM 边框色设为 NONE（Win11 默认会给窗口描一圈系统色边）
                int none = unchecked((int)0xFFFFFFFE);
                _ = DwmSetWindowAttribute(hwnd, 34, ref none, 4);
            }
        }
        catch { }

        _card.Background = new SolidColorBrush(Color.Parse(m.CardHex));
        _card.BorderBrush = Brush.Parse("#26FFFFFF");
        _card.BorderThickness = new Thickness(1);
        _card.Child = _strip;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);

    // ============================================================
    // 配置窗：添加/删除应用、置顶
    // ============================================================
    private void OpenConfig()
    {
        var panel = new StackPanel { Margin = new Thickness(16), Spacing = 8 };
        var w = new Window
        {
            Title = "Aqua Dock · 配置",
            Width = 380, Height = 430,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
        };

        panel.Children.Add(new TextBlock { Text = "应用列表（点击图标启动 · 右键 Dock 空白处打开本窗）", FontSize = 12, Opacity = 0.6 });

        var list = new StackPanel { Spacing = 4 };
        void RefreshList()
        {
            list.Children.Clear();
            foreach (var app in _state.Apps)
            {
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                var name = new TextBlock
                {
                    Text = $"{app.Label}  ·  {IOPath.GetFileName(app.Path)}",
                    VerticalAlignment = VerticalAlignment.Center, FontSize = 13,
                };
                Grid.SetColumn(name, 0);
                row.Children.Add(name);
                var del = new Button { Content = "移除", Padding = new Thickness(10, 2), FontSize = 12, Tag = app };
                Grid.SetColumn(del, 1);
                del.Click += (_, _) =>
                {
                    _state.Apps.Remove(app);
                    SaveState(); RebuildIcons(); RefreshList();
                };
                row.Children.Add(del);
                list.Children.Add(row);
            }
            if (_state.Apps.Count == 0)
                list.Children.Add(new TextBlock { Text = "（空）", Opacity = 0.5, FontSize = 12 });
        }
        RefreshList();
        panel.Children.Add(new ScrollViewer { Content = list, MaxHeight = 220 });

        var addBtn = new Button { Content = "＋ 添加程序（选择 .exe）", HorizontalAlignment = HorizontalAlignment.Stretch };
        addBtn.Click += async (_, _) =>
        {
            try
            {
                var files = await StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
                {
                    Title = "选择程序",
                    AllowMultiple = true,
                    FileTypeFilter = new[] { new Avalonia.Platform.Storage.FilePickerFileType("程序") { Patterns = new[] { "*.exe" } } },
                });
                foreach (var f in files)
                {
                    var path = f.TryGetLocalPath();                    if (string.IsNullOrEmpty(path)) continue;
                    _state.Apps.Add(new DockApp { Path = path, Label = IOPath.GetFileNameWithoutExtension(path) });
                }
                SaveState(); RebuildIcons(); RefreshList();
            }
            catch (Exception ex) { MainWindow.PushToast("Dock", $"添加失败：{ex.Message}", 4); }
        };
        panel.Children.Add(addBtn);

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

        var hint = new TextBlock
        {
            Text = $"材质跟随小组件设置（当前：{SimpleWidgetWindow.Material.Name}，可在「小组件」页切换液态玻璃）\n悬停图标放大 · 下方亮点=正在运行 · 左键空白拖动",
            FontSize = 11, Opacity = 0.55, TextWrapping = TextWrapping.Wrap,
        };
        panel.Children.Add(hint);

        w.Content = new ScrollViewer { Content = panel };
        w.Show(this);
    }
}
