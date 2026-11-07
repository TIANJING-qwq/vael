using System;
using Avalonia.Controls;

namespace SBtools.Plugins;

/// <summary>
/// 插件上下文，提供插件可用的所有 API。
/// </summary>
public interface IPluginContext
{
    // ============================================================
    // 基本信息
    // ============================================================
    /// <summary>当前插件 ID</summary>
    string PluginId { get; }

    /// <summary>Vael主程序版本，如 "0.3.1"</summary>
    string AppVersion { get; }

    /// <summary>
    /// 当前插件的数据目录。已自动创建，可以安全读写文件。
    /// 路径：%APPDATA%\SchoolBusytools\plugins\{PluginId}\
    /// </summary>
    string DataDirectory { get; }

    // ============================================================
    // 日志与通知
    // ============================================================
    /// <summary>写日志。会出现在「实验室」的日志列表里，同时写入文件</summary>
    void Log(string message);

    /// <summary>弹出右下角通知。soundFile 可选：wav 文件名（先找插件目录，再找程序 Sounds 目录）</summary>
    void Toast(string title, string message, int durationSeconds = 5, string? soundFile = null);

    // ============================================================
    // 系统操作
    // ============================================================
    /// <summary>用默认浏览器打开 URL</summary>
    void OpenUrl(string url);

    /// <summary>播放 Sounds 目录下的 .wav 文件</summary>
    void PlaySound(string wavFileName);

    /// <summary>设置系统音量（0-100）</summary>
    void SetSystemVolume(int level);

    // ============================================================
    // 配置读写（持久化到 plugins\{PluginId}\config.json）
    // ============================================================
    string GetConfig(string key, string defaultValue = "");
    void SetConfig(string key, string value);

    bool GetConfigBool(string key, bool defaultValue = false);
    void SetConfigBool(string key, bool value);

    int GetConfigInt(string key, int defaultValue = 0);
    void SetConfigInt(string key, int value);

    // ============================================================
    // 事件
    // ============================================================
    /// <summary>浏览器插件命中关键词时触发。参数为关键词</summary>
    event Action<string>? KeywordMatched;

    /// <summary>网络状态变化时触发。true=已连接</summary>
    event Action<bool>? NetworkStatusChanged;

    // ============================================================
    // UI 扩展（可在插件里创建界面）
    // ============================================================
    /// <summary>
    /// 在主窗口侧边栏注册一个选项卡。buildPage 在每次进入该页时调用，
    /// 返回任意 Avalonia Control（插件工程需引用 Avalonia 11.3）。
    /// </summary>
    void RegisterTab(string title, Func<Control> buildPage);

    /// <summary>
    /// 在「小组件」页注册一张卡片。icon 为 Segoe Fluent Icons 字形（可空）。
    /// </summary>
    void RegisterWidgetCard(string title, string description, string icon, Action onClick);

    /// <summary>弹出一个独立窗口承载任意内容（置顶、可拖动标题栏）</summary>
    void ShowWindow(string title, Control content);
}