using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;

namespace SBtools.Services;

/// <summary>
/// 窗口背景材质工具。支持 None / Acrylic / Mica 三种模式。
/// 云母和亚克力仅在深色模式下生效，浅色模式下自动回退到不透明背景。
/// </summary>
public static class WindowBackdropService
{
    public static void Apply(Window window, string mode)
    {
        if (window == null) return;

        try
        {
            bool isDark = window.ActualThemeVariant == ThemeVariant.Dark;
            var solidColor = isDark ? Color.Parse("#FF202020") : Color.Parse("#FFF3F3F3");

            // 浅色模式下：云母/亚克力不支持，自动回退到不透明
            bool transparentSupported = isDark;
            if (!transparentSupported && (mode == "Acrylic" || mode == "Mica"))
            {
                LogService.Log($"浅色模式下 {mode} 不受支持，回退到不透明背景", "主题");
                mode = "None";
            }

            switch (mode)
            {
                case "Acrylic":
                    window.TransparencyBackgroundFallback = new SolidColorBrush(solidColor);
                    window.TransparencyLevelHint = new[] { WindowTransparencyLevel.AcrylicBlur };
                    window.Background = new SolidColorBrush(Color.Parse("#01000000"));
                    break;

                case "Mica":
                    window.TransparencyBackgroundFallback = new SolidColorBrush(solidColor);
                    window.TransparencyLevelHint = new[] { WindowTransparencyLevel.Mica };
                    window.Background = new SolidColorBrush(Color.Parse("#01000000"));
                    break;

                case "None":
                default:
                    window.TransparencyLevelHint = new[] { WindowTransparencyLevel.None };
                    window.Background = new SolidColorBrush(solidColor);
                    break;
            }

            LogService.Log($"窗口背景材质已切换为: {mode}（isDark={isDark}）", "主题");
        }
        catch (Exception ex)
        {
            LogService.Log($"应用窗口材质失败: {ex.Message}", "主题");
        }
    }

    public static bool IsTransparentModeSupported(Window window)
    {
        if (window == null) return false;
        return window.ActualThemeVariant == ThemeVariant.Dark;
    }

    public static void RefreshAll()
    {
        try
        {
            var mode = Models.ConfigManager.Instance.WindowBackdrop;

            if (Application.Current?.ApplicationLifetime is
                Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                if (desktop.MainWindow is Window main)
                    Apply(main, mode);

                foreach (var w in desktop.Windows)
                {
                    if (w != desktop.MainWindow)
                        Apply(w, mode);
                }
            }
        }
        catch { }
    }
}
