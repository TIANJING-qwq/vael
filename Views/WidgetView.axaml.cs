using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using SBtools.Services;

namespace SBtools.Views;

public partial class WidgetView : UserControl
{
    private static readonly (string Hex, string Name)[] _materials =
    {
        ("#D0202020", "曜石黑"), ("#E0F3F3F3", "云雾白"),
        ("#80FFFFFF", "玻璃"), ("#D01F3D3B", "薄荷"),
        ("#D03B2A6E", "紫罗兰"), ("#CCB01A2A", "绯红"),
        ("#D0142A4C", "深空蓝"), ("#C0FFFFFF", "毛玻璃"),
    };

    private static readonly (string Icon, string Name, string Desc, string Act)[] _widgets =
    {
        ("📊", "性能监控", "CPU/内存/网速", "perf"),
        ("🕐", "数字时钟", "大字时钟+日期", "clock"),
        ("⏳", "高考倒计时", "距次年6月7日", "gaokao"),
        ("📅", "自定义倒计时", "输入天数（提示框）", "days"),
        ("🖼", "随机涩图", "右键换图", "gallery"),
        ("🎲", "掷骰子", "1-6 点", "dice"),
        ("🪙", "翻硬币", "正/反", "coin"),
        ("💬", "每日一言", "随机一句话", "quote"),
        ("🔋", "电池状态", "电量与充放电", "battery"),
        ("💽", "磁盘余量", "C 盘剩余空间", "disk"),
        ("🔢", "随机点名", "输入总人数范围", "roll"),
        ("📝", "随机题号", "1-100 抽题", "question"),
        ("🍅", "番茄钟", "25 分钟专注", "pomodoro"),
        ("⏰", "休息提醒", "20 分钟后提醒", "rest"),
        ("🔕", "上课静音", "一键系统静音", "mute"),
        ("🔒", "快速锁屏", "Win+L", "lock"),
        ("⏻", "快速关机", "60 秒倒计时", "shutdown"),
        ("🔄", "快速重启", "60 秒倒计时", "reboot"),
        ("🧮", "打开计算器", "系统计算器", "calc"),
        ("⌨️", "打开屏幕键盘", "osk", "osk"),
        ("📋", "剪贴板历史", "Win+V", "cliphist"),
        ("🌐", "浏览器插件", "打开插件目录", "plugin"),
    };

    public WidgetView()
    {
        InitializeComponent();
        BuildMaterials();
        BuildGrid();
    }

    private void BuildMaterials()
    {
        foreach (var (hex, name) in _materials)
        {
            var b = new Button
            {
                Content = name,
                FontSize = 12,
                Padding = new Avalonia.Thickness(10, 5),
                CornerRadius = new Avalonia.CornerRadius(6),
                Background = Brush.Parse(hex),
                Foreground = hex == "#E0F3F3F3" || hex == "#C0FFFFFF" || hex == "#80FFFFFF"
                    ? Brushes.Black : Brushes.White,
                Tag = hex,
                Margin = new Avalonia.Thickness(0, 0, 8, 8),
            };
            b.Click += (_, _) =>
            {
                SimpleWidgetWindow.MaterialHex = hex;
                LogService.Log($"小组件材质: {name} ({hex})", "小组件");
                MainWindow.PushToast("🎨", $"材质已切换：{name}", 3);
            };
            MaterialPanel.Children.Add(b);
        }
    }

    private void BuildGrid()
    {
        foreach (var (icon, name, desc, act) in _widgets)
        {
            var b = new Button
            {
                Width = 160, Height = 92,
                Margin = new Avalonia.Thickness(0, 0, 10, 10),
                Padding = new Avalonia.Thickness(0),
                CornerRadius = new Avalonia.CornerRadius(10),
                Tag = act,
                Content = new StackPanel
                {
                    Spacing = 4,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Children =
                    {
                        new TextBlock { Text = icon, FontSize = 24, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                        new TextBlock { Text = name, FontSize = 13, FontWeight = Avalonia.Media.FontWeight.SemiBold, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                        new TextBlock { Text = desc, FontSize = 10, Opacity = 0.55, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center },
                    },
                },
            };
            b.Click += Widget_Click;
            WidgetGrid.Children.Add(b);
        }
    }

    private void Widget_Click(object? sender, RoutedEventArgs e) => _ = RunAsync((sender as Button)?.Tag as string ?? "");

    private async System.Threading.Tasks.Task RunAsync(string act)
    {
        try
        {
            switch (act)
            {
                case "perf": MainWindow.ToggleWidget(); break;
                case "clock": new SimpleWidgetWindow(SimpleWidgetWindow.Kind.Clock, "时钟").Show(); break;
                case "gaokao":
                {
                    var t = new DateTime(DateTime.Now.Year, 6, 7);
                    if (DateTime.Now > t) t = t.AddYears(1);
                    new SimpleWidgetWindow(SimpleWidgetWindow.Kind.Countdown, "高考倒计时", t).Show();
                    break;
                }
                case "days":
                {
                    var d = await PromptIntAsync("倒计时天数", 30);
                    if (d > 0)
                        new SimpleWidgetWindow(SimpleWidgetWindow.Kind.Countdown, "倒计时",
                            DateTime.Now.AddDays(d)).Show();
                    break;
                }
                case "gallery": new SimpleWidgetWindow(SimpleWidgetWindow.Kind.Gallery, "随机涩图").Show(); break;
                case "dice": MainWindow.PushToast("🎲", $"掷出 {Random.Shared.Next(1, 7)} 点", 4); break;
                case "coin": MainWindow.PushToast("🪙", Random.Shared.Next(2) == 0 ? "正面" : "反面", 4); break;
                case "quote":
                {
                    var qs = new[] { "行到水穷处，坐看云起时。", "日拱一卒，功不唐捐。", "凡是过往，皆为序章。", "山不让尘，川不辞盈。", "追风赶月莫停留，平芜尽处是春山。" };
                    MainWindow.PushToast("💬", qs[Random.Shared.Next(qs.Length)], 5);
                    break;
                }
                case "battery":
                    Process.Start(new ProcessStartInfo("cmd", "/c start ms-settings:batterysaver") { UseShellExecute = true });
                    MainWindow.PushToast("🔋", "已打开电池设置（设置内可看电量详情）", 4);
                    break;
                case "disk":
                {
                    var c = new System.IO.DriveInfo("C");
                    MainWindow.PushToast("💽", $"C 盘剩余 {c.TotalFreeSpace / 1024.0 / 1024 / 1024:F1} GB / 共 {c.TotalSize / 1024.0 / 1024 / 1024:F0} GB", 6);
                    break;
                }
                case "roll":
                {
                    var n = await PromptIntAsync("总人数", 45);
                    if (n > 0) MainWindow.PushToast("🔢", $"第 {Random.Shared.Next(1, n + 1)} 号，请回答！", 6);
                    break;
                }
                case "question": MainWindow.PushToast("📝", $"抽到第 {Random.Shared.Next(1, 101)} 题", 5); break;
                case "pomodoro": StartTimer("🍅 番茄钟完成！休息 5 分钟", 25); break;
                case "rest": StartTimer("⏰ 该休息一下眼睛了！", 20); break;
                case "mute":
                    Process.Start(new ProcessStartInfo("cmd", "/c start ms-settings:sound") { UseShellExecute = true });
                    MainWindow.PushToast("🔕", "已打开声音设置（或直接按键盘静音键）", 4);
                    break;
                case "lock": Process.Start("rundll32.exe", "user32.dll,LockWorkStation"); break;
                case "shutdown": Process.Start("shutdown", "/s /t 60"); MainWindow.PushToast("⏻", "60 秒后关机（shutdown /a 取消）", 5); break;
                case "reboot": Process.Start("shutdown", "/r /t 60"); MainWindow.PushToast("🔄", "60 秒后重启（shutdown /a 取消）", 5); break;
                case "calc": Process.Start(new ProcessStartInfo("cmd", "/c start calc") { UseShellExecute = true }); break;
                case "osk": Process.Start(new ProcessStartInfo("cmd", "/c start osk") { UseShellExecute = true }); break;
                case "cliphist": MainWindow.PushToast("📋", "请按 Win+V 打开剪贴板历史（可在小组件页开启）", 5); break;
                case "plugin":
                    Process.Start("explorer.exe", System.IO.Path.Combine(AppContext.BaseDirectory, "Extensions", "browser-plugin"));
                    MainWindow.PushToast("🌐", "已打开插件目录：chrome://extensions 开发者模式→加载已解压", 6);
                    break;
            }
        }
        catch (Exception ex) { LogService.Log($"小组件[{act}]失败: {ex.Message}", "小组件"); }
    }

    /// <summary>极简数字输入弹窗（确定返回输入值，取消/空返回 0）</summary>
    private async System.Threading.Tasks.Task<int> PromptIntAsync(string title, int def)
    {
        try
        {
            var input = new TextBox { Watermark = $"输入数字（默认 {def}）", Width = 220 };
            var ok = new Button { Content = "确定", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
            var w = new Window
            {
                Title = title, Width = 300, Height = 150,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Content = new StackPanel
                {
                    Margin = new Avalonia.Thickness(16), Spacing = 10,
                    Children = { new TextBlock { Text = title }, input, ok },
                },
            };
            int result = 0;
            ok.Click += (_, _) => { int.TryParse(input.Text, out result); w.Close(); };
            await w.ShowDialog((Window)TopLevel.GetTopLevel(this)!);
            return result;
        }
        catch { return def; }
    }

    private void StartTimer(string msg, int minutes)
    {
        var t = new System.Timers.Timer(minutes * 60_000) { AutoReset = false };
        t.Elapsed += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() => MainWindow.PushToast("⏰", msg, 10));
        t.Start();
        MainWindow.PushToast("⏱", $"{msg.Split('！')[0]}已开始（{minutes} 分钟）", 4);
    }
}
