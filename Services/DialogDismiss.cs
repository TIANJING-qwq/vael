using Avalonia.Controls;

namespace SBtools.Services;

/// <summary>弹窗交互扩展</summary>
public static class DialogDismiss
{
    /// <summary>
    /// 弹窗失焦时自动取消（点击弹窗外的空白处 = 取消，返回 false）。
    /// 用于确认类弹窗：UpdateAvailableDialog / QuitPasswordDialog / CloseConfirmDialog。
    /// </summary>
    public static void DismissOnDeactivate(this Window w)
    {
        w.Deactivated += (_, _) =>
        {
            try { if (w.IsVisible) w.Close(false); } catch { }
        };
    }
}
