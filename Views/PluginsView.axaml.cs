using Avalonia.Controls;
using Avalonia.Interactivity;
using SBtools.Plugins;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace SBtools.Views;

public partial class PluginsView : UserControl
{
    public PluginsView()
    {
        InitializeComponent();
        Refresh();
    }

    private void Refresh()
    {
        var list = this.FindControl<ItemsControl>("PluginList");
        var count = this.FindControl<TextBlock>("CountText");
        var empty = this.FindControl<StackPanel>("EmptyPanel");
        var hint = this.FindControl<TextBlock>("EmptyHint");

        // ============================================================
        // ★ 内置插件（始终置顶，不可禁用）
        // ============================================================
        var allPlugins = new List<PluginInfo>
        {
            new PluginInfo
            {
                Id = "com.schoolbusytools.xiyue.core",
                Name = "Vael核心功能",
                Version = "内置",
                Author = "Vael",
                Description = "Vael主程序的核心模块：校园网自动认证、定时 CCTV 播放、通知系统、图库、系统优化、插件加载器等。作为程序的基础功能，此项不可禁用。",
                IsLoaded = true,
                IsBuiltIn = true,
            },
        };

        // ============================================================
        // 用户插件
        // ============================================================
        allPlugins.AddRange(PluginManager.Instance.Plugins);

        if (list != null) list.ItemsSource = allPlugins;
        if (count != null)
            count.Text = allPlugins.Count > 1
                ? $"共 {allPlugins.Count} 个（含 1 个内置）"
                : "共 1 个（内置）";

        if (empty != null) empty.IsVisible = false;
        if (hint != null)
            hint.Text = $"把插件 DLL 放到：\n{PluginManager.PluginsDir}";
    }

    private void OpenFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var dir = PluginManager.PluginsDir;
            System.IO.Directory.CreateDirectory(dir);

            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo("explorer.exe", dir) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", dir);
            else
                Process.Start("xdg-open", dir);
        }
        catch { }
    }

    private void Reload_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            PluginManager.Instance.UnloadAll();
            PluginManager.Instance.LoadAll();
            Refresh();
            MainWindow.PushToast("插件", "已重新加载");
        }
        catch (Exception ex)
        {
            MainWindow.PushToast("插件重载失败", ex.Message);
        }
    }

    /// <summary>一键禁用：立即卸载（含选项卡/小组件卡片）并持久化</summary>
    private void Disable_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not PluginInfo p || string.IsNullOrEmpty(p.Id)) return;
        PluginManager.Instance.SetEnabled(p.Id, false);
        Refresh();
        MainWindow.PushToast("插件", $"{p.Name} 已禁用（选项卡与卡片已移除）", 4);
    }

    /// <summary>一键启用：立即激活并持久化</summary>
    private void Enable_Click(object? sender, RoutedEventArgs e)
    {
        if ((sender as Control)?.DataContext is not PluginInfo p || string.IsNullOrEmpty(p.Id)) return;
        PluginManager.Instance.SetEnabled(p.Id, true);
        Refresh();
        MainWindow.PushToast("插件", $"{p.Name} 已启用", 4);
    }
}