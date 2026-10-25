using System.Collections.Generic;

namespace SBtools.Models;

/// <summary>常见问题的自动解决动作类型。</summary>
public enum ProblemActionKind
{
    None,
    RestartExplorer,      // 重启资源管理器
    OpenControlPanel,     // 打开控制面板
    OpenDateAndTime,      // 打开日期和时间（改时区修复闪断）
    RunSfcDism,           // sfc + DISM 修复
    NetshWinsockReset,    // 网络栈重置
    NetshIpReset,         // TCP/IP 重置
    FlushDns,             // 刷新 DNS
    RenewIp,              // 释放/续租 IP
    RebuildIconCache,     // 重建图标缓存
    ThumbnailCache,       // 清理缩略图缓存
    OpenMsSettings,       // 打开设置页（Arg = ms-settings 页名）
    RunCommand,           // 在可见命令行执行（Arg = 命令）
    OpenUrl,              // 打开链接（Arg）
    OpenFolder,           // 打开文件夹（Arg）
    ResetStoreCache,      // 重置应用商店缓存
    CheckDisk,            // 磁盘检查
    RestartAudio,         // 重启音频服务
    ResetWmi,             // 修复 WMI 仓库
    PowerHibernationOff,  // 关闭休眠
    PowerFastStartupOff,  // 关闭快速启动
    OpenUpdateHistory,    // 打开更新历史
    OpenTroubleshoot,     // 打开疑难解答
    DisableAutoReboot,    // 关闭蓝屏自动重启
}

/// <summary>一个自动解决动作。</summary>
public sealed class ProblemAction
{
    public string Label { get; }
    public ProblemActionKind Kind { get; }
    public string? Arg { get; }

    public ProblemAction(string label, ProblemActionKind kind, string? arg = null)
    {
        Label = label;
        Kind = kind;
        Arg = arg;
    }
}

/// <summary>一条 Windows 常见问题。</summary>
public sealed class WindowsProblem
{
    public int Id { get; }
    public string Category { get; }
    public string Title { get; }
    /// <summary>现象描述。</summary>
    public string Symptom { get; }
    /// <summary>人工解决方法（文字步骤）。</summary>
    public string Manual { get; }
    /// <summary>自动解决动作（可能为空 = 仅人工）。</summary>
    public IReadOnlyList<ProblemAction> Auto { get; }

    /// <summary>是否有自动方案（供界面显示“仅人工”提示）。</summary>
    public bool HasAuto => Auto.Count > 0;

    public WindowsProblem(int id, string category, string title, string symptom,
        string manual, IReadOnlyList<ProblemAction> auto)
    {
        Id = id;
        Category = category;
        Title = title;
        Symptom = symptom;
        Manual = manual;
        Auto = auto;
    }
}
