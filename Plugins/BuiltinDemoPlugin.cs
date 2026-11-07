using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SBtools.Plugins;

namespace SBtools.Plugins;

/// <summary>
/// 内置示例插件：演示全部插件 API——
/// 选项卡、小组件卡片、独立窗口、配置读写、日志、通知、事件。
/// 可在「插件」页一键禁用/启用。
/// </summary>
public sealed class BuiltinDemoPlugin : IXiYuePlugin
{
    public string Id => "com.vael.demo";
    public string Name => "示例插件";
    public string Version => "1.0.0";
    public string Author => "Vael";
    public string Description => "内置演示：创建选项卡与小组件卡片、弹独立窗口、读写配置、接收事件。插件页可一键启用/禁用。";

    private IPluginContext _ctx = null!;
    private int _clickCount;

    public void OnLoad(IPluginContext context)
    {
        _ctx = context;
        context.Log("示例插件已加载（内置）");

        // 1) 侧边栏选项卡
        context.RegisterTab("示例插件页", BuildPage);

        // 2) 小组件页卡片
        context.RegisterWidgetCard("插件演示卡", "点击弹演示窗口", "\uE8A7", () =>
        {
            _clickCount++;
            context.Toast("示例插件", $"卡片被点击 {_clickCount} 次", 3);
            context.ShowWindow("示例插件 · 演示窗口", BuildPage());
        });

        // 3) 事件订阅演示
        context.NetworkStatusChanged += connected =>
            context.Log(connected ? "网络已连接（事件演示）" : "网络断开（事件演示）");
    }

    /// <summary>演示页：配置读写 + 通知 + 日志</summary>
    private Control BuildPage()
    {
        var keyBox = new TextBox { Watermark = "配置键（如 nickname）", Width = 260 };
        var valBox = new TextBox { Watermark = "配置值", Width = 260 };
        var result = new TextBlock { Opacity = 0.7, TextWrapping = TextWrapping.Wrap };

        var saveBtn = new Button { Content = "保存配置", Padding = new Thickness(16, 6) };
        saveBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(keyBox.Text)) return;
            _ctx.SetConfig(keyBox.Text.Trim(), valBox.Text ?? "");
            result.Text = $"已保存：{keyBox.Text} = {valBox.Text}（持久化到插件目录 config.json）";
            _ctx.Toast("示例插件", $"配置已保存：{keyBox.Text}", 3);
        };

        var readBtn = new Button { Content = "读取配置", Padding = new Thickness(16, 6) };
        readBtn.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(keyBox.Text)) return;
            result.Text = $"{keyBox.Text} = {_ctx.GetConfig(keyBox.Text.Trim(), "（未设置）")}";
        };

        var logBtn = new Button { Content = "写一条日志", Padding = new Thickness(16, 6) };
        logBtn.Click += (_, _) => { _ctx.Log("这是示例插件写的日志"); result.Text = "日志已写入（实验室 → 日志）"; };

        // ★ 通知 + 音效演示（Toast 第四个参数；wav 放插件目录或程序 Sounds/ 下）
        var sndBtn = new Button { Content = "通知+音效", Padding = new Thickness(16, 6) };
        sndBtn.Click += (_, _) =>
        {
            _ctx.Toast("示例插件", "这条通知带着音效（Toast 第 4 个参数）", 5, "冰冰冰.wav");
            result.Text = "已发送带音效的通知";
        };

        var cnt = new TextBlock { Opacity = 0.7, Text = $"启动至今配置读取演示：{_ctx.GetConfigInt("demo_reads", 0) + 1}" };
        _ctx.SetConfigInt("demo_reads", _ctx.GetConfigInt("demo_reads", 0) + 1);

        var buttons = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        buttons.Children.Add(saveBtn);
        buttons.Children.Add(readBtn);
        buttons.Children.Add(logBtn);
        buttons.Children.Add(sndBtn);

        return new StackPanel
        {
            Margin = new Thickness(24, 16), Spacing = 10, MaxWidth = 520,
            Children =
            {
                new TextBlock { Text = "示例插件页", FontSize = 24, FontWeight = FontWeight.Bold },
                new TextBlock { Text = "这一页由插件通过 RegisterTab 创建，界面完全由插件代码生成。\n插件还可以注册小组件卡片、弹独立窗口、读写自己的配置。", Opacity = 0.65, TextWrapping = TextWrapping.Wrap },
                keyBox, valBox, buttons, result,
                new Separator { Margin = new Thickness(0, 8) },
                cnt,
                new TextBlock { Text = $"主程序版本 {_ctx.AppVersion} · 数据目录：\n{_ctx.DataDirectory}", Opacity = 0.5, TextWrapping = TextWrapping.Wrap, FontSize = 11 },
            },
        };
    }

    public void OnUnload() => _ctx?.Log("示例插件已卸载");
}
