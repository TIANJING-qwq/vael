using Newtonsoft.Json;
using SBtools.Models;
using SBtools.Services;
using SBtools.Views;
using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Versioning;

namespace SBtools.Plugins;

internal class PluginContext : IPluginContext
{
    private readonly string _configPath;
    private Dictionary<string, string> _config;

    public string PluginId { get; }
    public string AppVersion { get; }
    public string DataDirectory { get; }

    public event Action<string>? KeywordMatched;
    public event Action<bool>? NetworkStatusChanged;

    public PluginContext(string pluginId)
    {
        PluginId = pluginId;
        AppVersion = typeof(PluginContext).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        DataDirectory = Path.Combine(
            ConfigManager.AppDataDir, "plugins", pluginId);

        try { Directory.CreateDirectory(DataDirectory); } catch { }

        _configPath = Path.Combine(DataDirectory, "config.json");
        _config = LoadConfig();

        // 订阅全局事件
        try { LocalIpcServer.Instance.KeywordMatched += OnKeywordMatched; } catch { }
    }

    private void OnKeywordMatched(string keyword)
    {
        try { KeywordMatched?.Invoke(keyword); } catch { }
    }

    internal void RaiseNetworkStatusChanged(bool connected)
    {
        try { NetworkStatusChanged?.Invoke(connected); } catch { }
    }

    internal void Detach()
    {
        try { LocalIpcServer.Instance.KeywordMatched -= OnKeywordMatched; } catch { }
    }

    // ============================================================
    // 日志与通知
    // ============================================================
    public void Log(string message)
    {
        LogService.Log($"[{PluginId}] {message}", "插件");
    }

    public void Toast(string title, string message, int durationSeconds = 5)
    {
        MainWindow.PushToast(title, message, durationSeconds);
    }

    // ============================================================
    // 系统操作
    // ============================================================
    public void OpenUrl(string url)
    {
        try
        {
            if (OperatingSystem.IsWindows())
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            else if (OperatingSystem.IsMacOS())
                Process.Start("open", url);
            else
                Process.Start("xdg-open", url);
        }
        catch (Exception ex)
        {
            Log($"打开 URL 失败: {ex.Message}");
        }
    }

    public void PlaySound(string wavFileName)
    {
        try
        {
            var path = Path.Combine(AppContext.BaseDirectory, "Sounds", wavFileName);
            if (!File.Exists(path))
            {
                Log($"音效文件不存在: {path}");
                return;
            }

            if (OperatingSystem.IsWindows())
            {
                PlaySoundWindows(path);
            }
            else if (OperatingSystem.IsMacOS())
            {
                Process.Start("afplay", $"\"{path}\"");
            }
            else
            {
                Process.Start("aplay", $"\"{path}\"");
            }
        }
        catch (Exception ex)
        {
            Log($"播放音效失败: {ex.Message}");
        }
    }

    [SupportedOSPlatform("windows")]
    private void PlaySoundWindows(string path)
    {
        var player = new System.Media.SoundPlayer(path);
        player.Play();
    }

    public void SetSystemVolume(int level)
    {
        try
        {
            SystemVolume.SetVolume(Math.Clamp(level, 0, 100) / 100f);
        }
        catch (Exception ex)
        {
            Log($"设置音量失败: {ex.Message}");
        }
    }

    // ============================================================
    // 配置读写
    // ============================================================
    private Dictionary<string, string> LoadConfig()
    {
        try
        {
            if (File.Exists(_configPath))
            {
                var json = File.ReadAllText(_configPath);
                var dict = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                if (dict != null) return dict;
            }
        }
        catch { }
        return new Dictionary<string, string>();
    }

    private void SaveConfig()
    {
        try
        {
            File.WriteAllText(_configPath,
                JsonConvert.SerializeObject(_config, Formatting.Indented));
        }
        catch { }
    }

    public string GetConfig(string key, string defaultValue = "")
    {
        return _config.TryGetValue(key, out var v) ? v : defaultValue;
    }

    public void SetConfig(string key, string value)
    {
        _config[key] = value;
        SaveConfig();
    }

    public bool GetConfigBool(string key, bool defaultValue = false)
    {
        var v = GetConfig(key, defaultValue ? "true" : "false");
        return v.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    public void SetConfigBool(string key, bool value)
    {
        SetConfig(key, value ? "true" : "false");
    }

    public int GetConfigInt(string key, int defaultValue = 0)
    {
        var v = GetConfig(key, defaultValue.ToString());
        return int.TryParse(v, out var n) ? n : defaultValue;
    }

    public void SetConfigInt(string key, int value)
    {
        SetConfig(key, value.ToString());
    }

    // ============================================================
    // UI 扩展
    // ============================================================
    private readonly List<string> _tabIds = new();

    public void RegisterTab(string title, Func<Avalonia.Controls.Control> buildPage)
    {
        var tabId = PluginId;
        _tabIds.Add(tabId);
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            MainWindow.RegisterPluginTab(tabId, title, buildPage));
    }

    public void RegisterWidgetCard(string title, string description, string icon, Action onClick)
        => WidgetView.RegisterPluginCard(PluginId, title, description, icon, onClick);

    public void ShowWindow(string title, Avalonia.Controls.Control content)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            var w = new Window
            {
                Title = title,
                Width = 480, Height = 380,
                Content = new Avalonia.Controls.ScrollViewer { Content = content },
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            w.Show(MainWindow._instance);
        });
    }

    /// <summary>卸载时移除本插件注册的全部 UI</summary>
    internal void RemoveUi()
    {
        foreach (var id in _tabIds)
            Avalonia.Threading.Dispatcher.UIThread.Post(() => MainWindow.RemovePluginTab(id));
        _tabIds.Clear();
        WidgetView.UnregisterPluginCards(PluginId);
    }
}