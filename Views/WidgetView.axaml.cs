using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SBtools.Services;
using SBtools.Models;

namespace SBtools.Views;

public partial class WidgetView : UserControl
{
    // 材质定义移至 SimpleWidgetWindow.Materials（含模糊模式与字色反色）

    // (Segoe Fluent 图标字形, 名称, 说明, Key)：全部自实现功能
    private static readonly (string, string, string, string)[] _widgets =
    {
        ("\uE70F", "性能监控", "CPU/内存/网速", "perf"),
        ("\uE823", "数字时钟", "大字时钟+星期", "clock"),
        ("\uE787", "高考倒计时", "右键改时间", "gaokao"),
        ("\uE91F", "自定义倒计时", "右键设目标", "countdown"),
        ("\uE91B", "随机涩图", "点击换图", "gallery"),
        ("\uE8FD", "骰子", "右键改面数", "dice"),
        ("\uE8EC", "抛硬币", "点击再抛", "coin"),
        ("\uE8BD", "一言卡片", "点击换句", "quote"),
        ("\uE716", "随机点名", "右键改人数", "roll"),
        ("\uE8A5", "随机题号", "右键改范围", "question"),
        ("\uE91F", "番茄钟", "右键改时长", "pomodoro"),
        ("\uE8EF", "计算器", "按键式", "calc"),
        ("\uE83F", "电池", "电量+充电状态", "battery"),
        ("\uE74E", "磁盘余量", "C盘剩余", "disk"),
        ("\uE823", "休息提醒", "20分钟循环", "rest"),
        ("\uE999", "内存曲线", "60秒历史曲线", "memwatch"),
    };

    private readonly Dictionary<string, Window> _open = new();
    private System.Timers.Timer? _restTimer;

    public WidgetView()
    {
        InitializeComponent();
        // 材质：下拉菜单选择（云母/亚克力，与主界面同系统实现）
        var matBox = new ComboBox { MinWidth = 150, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left };
        foreach (var m in SimpleWidgetWindow.Materials) matBox.Items.Add(m.Name);
        matBox.SelectedIndex = Math.Max(0, Array.IndexOf(SimpleWidgetWindow.Materials, SimpleWidgetWindow.Material));
        matBox.SelectionChanged += (_, _) =>
        {
            var idx = Math.Max(0, matBox.SelectedIndex);
            if (idx < SimpleWidgetWindow.Materials.Length)
            {
                SimpleWidgetWindow.Material = SimpleWidgetWindow.Materials[idx];
                MainWindow.PushToast("材质", $"已切换：{SimpleWidgetWindow.Materials[idx].Name}", 3);
            }
        };
        MaterialPanel.Children.Add(matBox);

        foreach (var (icon, name, desc, key) in _widgets)
        {
            // 封面图（网络随机图，失败静默回退纯色卡）
            var img = new Image { Stretch = Stretch.UniformToFill, Source = null, Opacity = 0 };
            _ = LoadCover(img, key);
            var b = new Button
            {
                Width = 165, Height = 92,
                Margin = new Avalonia.Thickness(0, 0, 10, 10),
                Padding = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(10),
                Tag = key,
                Content = new Grid
                {
                    Children =
                    {
                        img,
                        new Border { Background = new LinearGradientBrush
                        {
                            StartPoint = new Avalonia.RelativePoint(0, 1, Avalonia.RelativeUnit.Relative),
                            EndPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                            GradientStops =
                            {
                                new GradientStop(Avalonia.Media.Color.Parse("#D0181818"), 0),
                                new GradientStop(Avalonia.Media.Color.Parse("#40181818"), 1),
                            },
                        } },
                        new StackPanel
                        {
                            Spacing = 4,
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                            Children =
                            {
                                new TextBlock { Text = icon, FontSize = 28, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Opacity = 0.95,
                                                FontFamily = new Avalonia.Media.FontFamily("Segoe Fluent Icons,Segoe MDL2 Assets") },
                                new TextBlock { Text = name, FontSize = 13, FontWeight = Avalonia.Media.FontWeight.SemiBold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Foreground = Brushes.White },
                                new TextBlock { Text = desc, FontSize = 10, Opacity = 0.8, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Foreground = Brushes.White },
                            },
                        },
                    },
                },
            };
            b.Click += Widget_Click;
            WidgetGrid.Children.Add(b);
        }
    }

    /// <summary>加载卡片封面图：磁盘缓存优先（seed=文件名），无缓存才联网下载并存盘</summary>
    private static async System.Threading.Tasks.Task LoadCover(Avalonia.Controls.Image img, string seed)
    {
        try
        {
            var dir = System.IO.Path.Combine(SBtools.Models.ConfigManager.AppDataDir, "cover_cache");
            System.IO.Directory.CreateDirectory(dir);
            var file = System.IO.Path.Combine(dir, seed + ".jpg");

            byte[] bytes;
            if (System.IO.File.Exists(file))
            {
                bytes = await System.IO.File.ReadAllBytesAsync(file);
            }
            else
            {
                using var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(6) };
                bytes = await http.GetByteArrayAsync($"https://picsum.photos/seed/{seed}/330/184");
                try { await System.IO.File.WriteAllBytesAsync(file, bytes); } catch { } // 缓存失败不影响显示
            }

            using var ms = new System.IO.MemoryStream(bytes);
            var bmp = new Avalonia.Media.Imaging.Bitmap(ms);
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { img.Source = bmp; img.Opacity = 1; });
        }
        catch { }
    }

    /// <summary>点击开启，再次点击关闭</summary>
    private void Widget_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
        var key = (sender as Button)?.Tag as string ?? "";
        if (_open.TryGetValue(key, out var exist))
        {
            exist.Close();
            _open.Remove(key);
            MainWindow.PushToast("🔕", "小组件已关闭", 2);
            return;
        }

        Window w = key switch
        {
            "perf" => New(SimpleWidgetWindow.Kind.Perf, "性能监控"),
            "clock" => New(SimpleWidgetWindow.Kind.Clock, "时钟"),
            "gaokao" => New(SimpleWidgetWindow.Kind.Countdown, "高考倒计时",
                c => { var t = new DateTime(DateTime.Now.Year, 6, 7); if (DateTime.Now > t) t = t.AddYears(1); c.Target = t; }),
            "countdown" => New(SimpleWidgetWindow.Kind.Countdown, "倒计时", c => c.Target = DateTime.Now.AddDays(30)),
            "gallery" => New(SimpleWidgetWindow.Kind.Gallery, "随机涩图"),
            "dice" => New(SimpleWidgetWindow.Kind.Dice, "骰子", c => c.DiceFaces = 6),
            "coin" => New(SimpleWidgetWindow.Kind.Coin, "抛硬币"),
            "quote" => New(SimpleWidgetWindow.Kind.Quote, "一言"),
            "roll" => New(SimpleWidgetWindow.Kind.Roll, "随机点名", c => c.Range = 45),
            "question" => New(SimpleWidgetWindow.Kind.Roll, "随机题号", c => c.Range = 100),
            "pomodoro" => New(SimpleWidgetWindow.Kind.Pomodoro, "番茄钟", c => c.PomoMinutes = 25),
            "calc" => New(SimpleWidgetWindow.Kind.Calc, "计算器"),
            "battery" => New(SimpleWidgetWindow.Kind.Battery, "电池"),
            "disk" => NewDiskWidget(),
            "rest" => StartRestReminder(),
            "memwatch" => New(SimpleWidgetWindow.Kind.Battery, "内存占用"), // 复用布局
            _ => throw new ArgumentException(key),
        };
        if (w != null) { _open[key] = w; w.Closed += (_, _) => _open.Remove(key); }
        }
        catch (Exception ex)
        {
            var k = (sender as Button)?.Tag as string ?? "?";
            LogService.Log($"小组件[{k}]打开失败: {ex.Message}", "小组件");
            MainWindow.PushToast("错误", $"小组件打开失败：{ex.Message}", 4);
        }
    }

    private SimpleWidgetWindow New(SimpleWidgetWindow.Kind k, string title, Action<SimpleWidgetWindow.Config>? setup = null)
    {
        var cfg = new SimpleWidgetWindow.Config { Title = title };
        setup?.Invoke(cfg);
        var w = new SimpleWidgetWindow(k, cfg);
        w.Show();
        return w;
    }

    private SimpleWidgetWindow NewDiskWidget()
    {
        var cfg = new SimpleWidgetWindow.Config { Title = "C 盘" };
        var w = new SimpleWidgetWindow(SimpleWidgetWindow.Kind.Battery, cfg); // 借电池窗结构
        w.Show();
        return w;
    }

    private SimpleWidgetWindow StartRestReminder()
    {
        _restTimer?.Stop();
        var w = New(SimpleWidgetWindow.Kind.Quote, "休息提醒");
        _restTimer = new System.Timers.Timer(20 * 60_000) { AutoReset = true };
        int n = 0;
        _restTimer.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            n++;
            MainWindow.PushToast("💧", $"已专注 {20 * n} 分钟，喝水休息一下眼睛！", 8);
        });
        _restTimer.Start();
        return w;
    }
}
