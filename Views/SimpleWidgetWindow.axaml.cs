using System;
using System.IO;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace SBtools.Views;

/// <summary>通用小组组件窗：时钟 / 倒计时 / 随机涩图（材质可定制）</summary>
public partial class SimpleWidgetWindow : Window
{
    public enum Kind { Clock, Countdown, Gallery }
    private readonly Kind _kind;
    private readonly TextBlock _big = new() { FontSize = 30, FontWeight = FontWeight.Bold, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly TextBlock _sub = new() { FontSize = 12, Opacity = 0.7, HorizontalAlignment = HorizontalAlignment.Center };
    private readonly Image? _img;
    private readonly string[]? _imgs;
    private DateTime _target;

    /// <summary>当前材质（小组件页可切换）</summary>
    public static string MaterialHex = "#D0202020";

    public SimpleWidgetWindow(Kind kind, string title, DateTime target = default)
    {
        _kind = kind;
        _target = target == default ? DateTime.Today.AddDays(1) : target;
        Title = title;
        Width = kind == Kind.Gallery ? 300 : 220;
        Height = kind == Kind.Gallery ? 240 : 130;
        SystemDecorations = SystemDecorations.None;
        ShowInTaskbar = false;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent;
        Topmost = true;

        var card = new Border
        {
            Background = Brush.Parse(MaterialHex),
            CornerRadius = new Avalonia.CornerRadius(12),
            BorderBrush = Brush.Parse("#33FFFFFF"),
            BorderThickness = new Avalonia.Thickness(1),
            Margin = new Avalonia.Thickness(6),
            ClipToBounds = true,
            Padding = new Avalonia.Thickness(12),
        };

        var panel = new StackPanel { Spacing = 6, VerticalAlignment = VerticalAlignment.Center };
        var head = new TextBlock { Text = title, FontSize = 12, Opacity = 0.7 };
        panel.Children.Add(head);
        if (kind == Kind.Gallery)
        {
            _img = new Image { Stretch = Stretch.Uniform, MaxHeight = 170 };
            panel.Children.Add(_img);
            try
            {
                var dir = Services.GalleryService.CacheDir;
                _imgs = Array.FindAll(Directory.GetFiles(dir, "*.*", SearchOption.AllDirectories),
                    f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                         f.EndsWith(".webp", StringComparison.OrdinalIgnoreCase));
                Shuffle();
            }
            catch { _imgs = Array.Empty<string>(); }
        }
        else { panel.Children.Add(_big); }
        panel.Children.Add(_sub);
        card.Child = panel;
        Content = card;

        PointerPressed += (_, e) =>
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) BeginMoveDrag(e);
            else if (_kind == Kind.Gallery) Shuffle();
        };

        Position = new Avalonia.PixelPoint(
            (int)(Screens.Primary?.WorkingArea.Width - (int)Width - 20 ?? 1200),
            120 + new Random().Next(120));

        var t = new System.Timers.Timer(1000) { AutoReset = true };
        t.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(Tick);
        t.Start();
        Tick();
    }

    private void Tick()
    {
        try
        {
            if (_kind == Kind.Clock)
            {
                var now = DateTime.Now;
                _big.Text = now.ToString("HH:mm:ss");
                _sub.Text = now.ToString("yyyy年M月d日 ddd").Replace("周", " 星期");
            }
            else if (_kind == Kind.Countdown)
            {
                var left = _target - DateTime.Now;
                if (left < TimeSpan.Zero) { _big.Text = "🎉"; _sub.Text = "目标已到！"; return; }
                _big.Text = $"{left.Days}天 {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
                _sub.Text = $"目标 {_target:yyyy-M-d HH:mm}";
            }
        }
        catch { }
    }

    private void Shuffle()
    {
        if (_imgs == null || _imgs.Length == 0 || _img == null) return;
        try
        {
            _img.Source = new Avalonia.Media.Imaging.Bitmap(_imgs[new Random().Next(_imgs.Length)]);
            _sub.Text = $"共 {_imgs.Length} 张 · 右键换图";
        }
        catch { }
    }
}
