using Avalonia.Controls;

namespace SBtools.Services;

/// <summary>弹窗交互扩展</summary>
public static class DialogDismiss
{
    /// <summary>
    /// 弹窗失焦时自动取消（点击弹窗外的空白处 = 取消）。
    /// cancelResult 必须与 ShowDialog&lt;T&gt; 的类型一致：
    /// bool 弹窗传 false，枚举弹窗传对应的取消值（如 CloseAction.Cancel）。
    /// </summary>
    public static void DismissOnDeactivate<T>(this Window w, T cancelResult)
    {
        w.Deactivated += (_, _) =>
        {
            try { if (w.IsVisible) w.Close(cancelResult); } catch { }
        };
    }
}
