using System;

namespace SBtools.Services;

/// <summary>
/// 平台能力检测。跨平台构建时，界面据此**自动禁用/隐藏**不可用的功能。
/// </summary>
public static class PlatformInfo
{
    public static bool IsWindows => OperatingSystem.IsWindows();
    public static bool IsLinux => OperatingSystem.IsLinux();
    public static bool IsMacOS => OperatingSystem.IsMacOS();

    public static string Name =>
        IsWindows ? "Windows" :
        IsLinux ? "Linux" :
        IsMacOS ? "macOS" : "Unknown";

    // ============================================================
    // 功能可用性
    // ============================================================

    /// <summary>CCTV 直播（依赖 Edge WebView2，仅 Windows）。</summary>
    public static bool CanUseCctv => IsWindows;

    /// <summary>系统优化（注册表 / 服务 / PowerShell，仅 Windows）。</summary>
    public static bool CanUseOptimization => IsWindows;

    /// <summary>系统修复（sfc / DISM / netsh / schtasks / 第三方工具，仅 Windows）。</summary>
    public static bool CanUseSystemRepair => IsWindows;

    /// <summary>以管理员身份启动（计划任务 schtasks，仅 Windows）。</summary>
    public static bool CanUseAdminStart => IsWindows;

    /// <summary>数据备份（跨平台：Windows 用 robocopy，其它平台用托管复制）。</summary>
    public static bool CanUseBackup => true;

    /// <summary>音量键 / 系统音量（user32，仅 Windows）。</summary>
    public static bool CanUseVolumeKeys => IsWindows;

    /// <summary>开机自启（Windows 用注册表，其它平台暂不支持）。</summary>
    public static bool CanUseAutoStart => IsWindows;

    /// <summary>桌面材质（云母/亚克力，仅 Windows 11）。</summary>
    public static bool CanUseBackdrop =>
        IsWindows && Environment.OSVersion.Version.Build >= 22000;
}
