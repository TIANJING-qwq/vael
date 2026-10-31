using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SBtools.Services;

/// <summary>
/// 给所有 ComboBox 的下拉弹出层加非线性入场动画
/// （淡入 + 自上而下位移，ExponentialEaseOut 缓动，无过冲）。
/// 动画作用在弹出内容（Popup.Child 里的 Border）上——
/// PopupRoot 是独立原生窗口，对它做 Opacity/Transform 不生效。
/// </summary>
public static class PopupAnimator
{
    private static readonly AttachedProperty<bool> HookedProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("PopupAnimatorHooked", typeof(PopupAnimator));

    /// <summary>扫描可视树，给所有尚未挂钩的 ComboBox 挂下拉动画。</summary>
    public static void HookAll(Visual root)
    {
        if (root == null) return;
        try
        {
            // ★ 尚未附加：等附加 + 首次布局完成（模板应用、子控件可见）再挂。
            //   仅等 AttachedToVisualTree 不够——那时模板还没应用，可视树是空的。
            if (root.GetVisualRoot() == null)
            {
                EventHandler<VisualTreeAttachmentEventArgs>? handler = null;
                handler = (_, _) =>
                {
                    root.AttachedToVisualTree -= handler;
                    HookAfterFirstLayout(root);
                };
                root.AttachedToVisualTree += handler;
                return;
            }

            HookAllCore(root);
        }
        catch { }
    }

    private static void HookAfterFirstLayout(Visual root)
    {
        if (root is not Avalonia.Layout.Layoutable l)
        {
            HookAllCore(root);
            return;
        }

        EventHandler? h = null;
        h = (_, _) =>
        {
            l.LayoutUpdated -= h;
            HookAllCore(root);
        };
        l.LayoutUpdated += h;
    }

    private static void HookAllCore(Visual root)
    {
        try
        {
            int n = 0;
            foreach (var box in EnumerateDescendants(root).OfType<ComboBox>())
            {
                if (box.GetValue(HookedProperty)) continue;
                box.SetValue(HookedProperty, true);
                box.DropDownOpened += (_, _) => AnimatePopup(box);
                box.SelectionChanged += (_, _) => AnimateSelection(box);
                n++;
            }
            if (n > 0)
                LogService.Log($"下拉动画：挂钩 {n} 个 ComboBox", "动画");
        }
        catch { }
    }

    // ============================================================
    // 动画本体
    // ============================================================

    /// <summary>选中项切换：收起状态下框内显示的内容做 淡入+上浮 入场。</summary>
    private static void AnimateSelection(ComboBox box)
    {
        try
        {
            var item = box.SelectedItem;
            if (item == null) return;

            var presenter = EnumerateDescendants(box)
                .OfType<ContentPresenter>()
                .FirstOrDefault(cp => ReferenceEquals(cp.Content, item));
            if (presenter == null) return;

            RunEnter(presenter, fromY: -6, ms: 240); // 从上往下，与展开方向一致
        }
        catch (Exception ex)
        {
            LogService.Log($"选中动画失败: {ex.Message}", "动画");
        }
    }

    /// <summary>下拉面板展开：淡入 + 自上而下位移入场（直接动画弹窗内容根，整块必然可见）。</summary>
    private static void AnimatePopup(ComboBox box)
    {
        try
        {
            var popup = EnumerateDescendants(box)
                .OfType<Popup>()
                .FirstOrDefault(p => p.IsOpen);
            if (popup == null) { LogService.Log("下拉动画：模板里未找到打开的 Popup", "动画"); return; }
            if (popup.Child == null) { LogService.Log("下拉动画：Popup.Child 为空", "动画"); return; }

            RunEnter(popup.Child, fromY: -8, ms: 250);
        }
        catch (Exception ex)
        {
            LogService.Log($"下拉动画失败: {ex.Message}", "动画");
        }
    }

    // ============================================================
    // 入场动画：位移手动补间 + 淡入用 KeyFrame（Opacity 有 animator）
    // ============================================================
    private static void RunEnter(Visual target, double fromY, int ms)
    {
        // 1) 位移补间：16ms 步进，ExponentialEaseOut 缓动（不可用动画系统，保证可见）
        try
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var easing = new ExponentialEaseOut();
            timer.Tick += (_, _) =>
            {
                double t = Math.Min(1.0, sw.Elapsed.TotalMilliseconds / ms);
                double y = fromY * (1 - easing.Ease(t));
                try { target.RenderTransform = TransformOperations.Parse($"translateY({y:0.##}px)"); }
                catch { }
                if (t >= 1.0)
                {
                    timer.Stop();
                    try { target.RenderTransform = null; } catch { }
                }
            };
            timer.Start();
        }
        catch { }

        // 2) 淡入：KeyFrame 只动 Opacity
        try
        {
            var anim = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(ms),
                Easing = new ExponentialEaseOut(),
                Children =
                {
                    new KeyFrame { Cue = new Cue(0d), Setters = { new Setter(Visual.OpacityProperty, 0d) } },
                    new KeyFrame { Cue = new Cue(1d), Setters = { new Setter(Visual.OpacityProperty, 1d) } },
                },
            };
            _ = anim.RunAsync(target);
        }
        catch (Exception ex)
        {
            LogService.Log($"入场淡入失败: {ex.Message}", "动画");
        }
    }

    // ============================================================
    // 可视树遍历
    // ============================================================
    private static IEnumerable<Visual> EnumerateDescendants(Visual root)
    {
        var stack = new Stack<Visual>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var v = stack.Pop();
            yield return v;
            foreach (var child in v.GetVisualChildren())
                stack.Push(child);
        }
    }
}
