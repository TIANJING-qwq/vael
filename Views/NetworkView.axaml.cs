using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class NetworkView : UserControl
{
    private NetworkViewModel? _vm;

    public NetworkView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        try
        {
            _vm = new NetworkViewModel();
            DataContext = _vm;
            _vm.Start();

            LoadScheduleFromScheduler();
        }
        catch (Exception ex)
        {
            LogService.Log($"NetworkView 加载失败: {ex.Message}", "网络");
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        try { _vm?.Stop(); } catch { }
        _vm = null;
        DataContext = null;
    }

    private async void ConnectButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.ConnectAsync(); }
        catch (Exception ex) { LogService.Log($"连接按钮异常: {ex.Message}", "网络"); }
    }

    private async void RefreshButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_vm == null) return;
        try { await _vm.RefreshAsync(); }
        catch (Exception ex) { LogService.Log($"刷新按钮异常: {ex.Message}", "网络"); }
    }

    private void LoadScheduleFromScheduler()
    {
        try
        {
            var s = MainWindow.Scheduler;
            if (s == null) return;

            ScheduleEnabledSwitch.IsChecked = s.Enabled;
            StartTimePicker.SelectedTime = s.StartTime;
            EndTimePicker.SelectedTime = s.EndTime;
            InitDayButtons(ConfigManager.Instance.ScheduleDays);

            // 频道下拉框
            ScheduleChannelComboBox.Items.Clear();
            foreach (var name in CctvChannels.Names)
                ScheduleChannelComboBox.Items.Add(name);

            int idx = Array.IndexOf(CctvChannels.Names, ConfigManager.Instance.ScheduleChannel);
            ScheduleChannelComboBox.SelectedIndex = idx >= 0 ? idx : 0;

            UpdateScheduleStatus();
        }
        catch (Exception ex)
        {
            LogService.Log($"加载调度配置失败: {ex.Message}", "调度");
        }
    }

    private void UpdateScheduleStatus()
    {
        var s = MainWindow.Scheduler;
        if (s == null)
        {
            ScheduleStatusText.Text = "调度器未初始化";
            return;
        }

        if (!s.Enabled)
        {
            ScheduleStatusText.Text = "当前未启用";
            return;
        }

        ScheduleStatusText.Text =
            $"已启用：{(s.Days.Count == 0 ? "每天" : string.Join('、', s.Days.OrderBy(d => (int)d == 0 ? 7 : (int)d).Select(d => "周" + "日一二三四五六"[(int)d])))} {FormatTime(s.StartTime)} 至 {FormatTime(s.EndTime)} 播放 {ConfigManager.Instance.ScheduleChannel}";
    }

    /// <summary>生成 周日~周六 7 个切换按钮（全不选=每天）</summary>
    private void InitDayButtons(int mask)
    {
        if (ScheduleDaysPanel.Children.Count > 0) { SetDayButtons(mask); return; }
        var names = "日,一,二,三,四,五,六".Split(',');
        for (int i = 0; i < 7; i++)
        {
            var b = new Avalonia.Controls.Primitives.ToggleButton
            {
                Content = "周" + names[i],
                FontSize = 12,
                Padding = new Avalonia.Thickness(8, 3),
                Margin = new Avalonia.Thickness(0, 0, 6, 0),
                Tag = i,
            };
            ScheduleDaysPanel.Children.Add(b);
        }
        SetDayButtons(mask);
    }

    private void SetDayButtons(int mask)
    {
        foreach (var c in ScheduleDaysPanel.Children)
            if (c is Avalonia.Controls.Primitives.ToggleButton t && t.Tag is int i)
                t.IsChecked = mask != 0 && (mask & (1 << i)) != 0;
    }

    /// <summary>全不选 = 0 = 每天</summary>
    private int GetDaysMask()
    {
        int m = 0;
        foreach (var c in ScheduleDaysPanel.Children)
            if (c is Avalonia.Controls.Primitives.ToggleButton { IsChecked: true } t && t.Tag is int i)
                m |= 1 << i;
        return m;
    }

    private static string FormatTime(TimeSpan t)
        => $"{t.Hours:D2}:{t.Minutes:D2}";

    private void TestPlaybackButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (!PlatformInfo.CanUseCctv)
            {
                MainWindow.PushToast("测试播放", "CCTV 直播仅支持 Windows（依赖 WebView2）");
                return;
            }

            var name = CctvChannels.Resolve(ScheduleChannelComboBox.SelectedItem as string);
            var url = CctvWebPlayerWindow.GetCctvLiveUrl(name) ?? "https://tv.cctv.com/live/cctv13/";

            var player = new CctvWebPlayerWindow(url);
            player.Show();
            MainWindow.PushToast("测试播放", $"正在播放 {name}");
        }
        catch (Exception ex)
        {
            LogService.Log($"测试播放失败: {ex.Message}", "播放");
        }
    }

    private void SaveScheduleButton_Click(object? sender, RoutedEventArgs e)
    {
        var cfg = ConfigManager.Instance;

        cfg.ScheduleEnabled = ScheduleEnabledSwitch.IsChecked == true;

        if (StartTimePicker.SelectedTime is TimeSpan start)
            cfg.ScheduleStartTime = start;

        if (EndTimePicker.SelectedTime is TimeSpan end)
            cfg.ScheduleEndTime = end;

        if (ScheduleChannelComboBox.SelectedItem is string channel && !string.IsNullOrEmpty(channel))
            cfg.ScheduleChannel = channel;

        // ConfigManager 只写 ScheduleConfig 静态量，这里再同步到调度器实例
        MainWindow.ApplyScheduleConfig();

        UpdateScheduleStatus();

        var desc = cfg.ScheduleEnabled
            ? $"已开启，每天 {FormatTime(cfg.ScheduleStartTime)} – {FormatTime(cfg.ScheduleEndTime)} 播放 {cfg.ScheduleChannel}"
            : "已关闭";

        MainWindow.PushToast("定时播放设置", desc);
    }
}