using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using FluentAvalonia.Styling;
using SBtools.Controls;
using SBtools.Models;
using SBtools.Services;
using SBtools.Views;
using System;
using System.Linq;

namespace SBtools;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        ApplyTheme(ConfigManager.Instance.ThemeMode);
        ToastHost.AutoCollapseOnNew = ConfigManager.Instance.AutoCollapseOnNewToast;

        // 修复旧版自启动注册项
        try { AutoStartManager.RepairIfNeeded(); } catch { }

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var args = Environment.GetCommandLineArgs();
            bool isAutoStart = args.Any(a =>
                a.Equals("--autostart", StringComparison.OrdinalIgnoreCase));

            bool startMinimized = isAutoStart && ConfigManager.Instance.AutoStartMinimized;

            LogService.Log(
                $"启动参数: [{string.Join(" ", args)}], " +
                $"isAutoStart={isAutoStart}, " +
                $"AutoStartMinimized={ConfigManager.Instance.AutoStartMinimized}, " +
                $"startMinimized={startMinimized}",
                "启动");

            if (startMinimized)
            {
                // ★ 关键修复：
                // 不设置 desktop.MainWindow —— 避免某些 Avalonia 版本自动 Show()
                // 只保留 MainWindow 内部的静态 _instance 引用，供托盘使用
                var window = new MainWindow(startMinimized);
                LogService.Log("自启动静默模式：不设置 MainWindow，主窗口不显示", "启动");
            }
            else
            {
                // ★ 先播放开场动画（受「启动动画」开关控制）；主窗口就绪后立即结束动画
                if (ConfigManager.Instance.StartupAnimationEnabled)
                {
                    try
                    {
                        SplashWindow.ShowSplash();
                        SplashWindow.SetProgress(0.12, "初始化运行环境");
                    }
                    catch (Exception ex) { LogService.Log($"开场动画启动失败: {ex.Message}", "启动"); }
                }

                SplashWindow.SetProgress(0.35, "载入桌面组件");
                var window = new MainWindow(startMinimized);
                SplashWindow.SetProgress(0.58, "构建主界面");
                desktop.MainWindow = window;
                window.Opened += async (_, _) =>
                {
                    SplashWindow.SetProgress(0.86, "连接插件与通知服务");
                    await SplashWindow.DismissAsync();
                    try { window.Activate(); } catch { }
                };
                window.Show();
            }
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyTheme(string mode)
    {
        if (Current == null) return;

        var variant = mode switch
        {
            "Light" => ThemeVariant.Light,
            "Dark"  => ThemeVariant.Dark,
            _       => ThemeVariant.Default,
        };

        Current.RequestedThemeVariant = variant;

        foreach (var style in Current.Styles)
        {
            if (style is FluentAvaloniaTheme fa)
                fa.PreferSystemTheme = (mode == "Default");
        }

        LogService.Log($"主题已切换为: {mode}", "主题");
    }
}