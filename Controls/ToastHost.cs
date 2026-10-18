using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using System;
using System.Collections.Specialized;
using System.Linq;

namespace SBtools.Controls;

public class ToastHost : Panel
{
    // ★ 全局设置：新通知到达时是否自动折叠堆叠
    public static bool AutoCollapseOnNew { get; set; } = true;

    public static readonly StyledProperty<double> GapProperty =
        AvaloniaProperty.Register<ToastHost, double>(nameof(Gap), 10);

    public static readonly StyledProperty<double> StackedVisibleProperty =
        AvaloniaProperty.Register<ToastHost, double>(nameof(StackedVisible), 32);

    public double Gap
    {
        get => GetValue(GapProperty);
        set => SetValue(GapProperty, value);
    }

    public double StackedVisible
    {
        get => GetValue(StackedVisibleProperty);
        set => SetValue(StackedVisibleProperty, value);
    }

    private bool _expanded;

    public bool IsExpanded
    {
        get => _expanded;
        set
        {
            if (_expanded != value)
            {
                _expanded = value;
                InvalidateArrange();
            }
        }
    }

    public ToastHost()
    {
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Bubble);
        Children.CollectionChanged += OnChildrenChanged;
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // ★ 新通知加入时，根据设置决定是否折叠回堆叠状态
        if (e.Action == NotifyCollectionChangedAction.Add && _expanded)
        {
            if (AutoCollapseOnNew)
                IsExpanded = false;
        }
        InvalidateArrange();
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.Handled) return;
        if (!CanStack()) return;
        IsExpanded = !IsExpanded;
        e.Handled = true;
    }

    private bool CanStack()
    {
        if (Children.Count <= 1) return false;
        double total = 0;
        foreach (var c in Children) total += c.DesiredSize.Height;
        total += (Children.Count - 1) * Gap;
        return total > Bounds.Height;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double maxW = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            maxW = Math.Max(maxW, child.DesiredSize.Width);
        }
        return new Size(maxW, availableSize.Height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        int n = Children.Count;
        if (n == 0) return finalSize;

        var heights = new double[n];
        for (int i = 0; i < n; i++)
            heights[i] = Children[i].DesiredSize.Height;

        var ys = ComputePositions(heights, finalSize.Height);

        for (int i = 0; i < n; i++)
        {
            if (Children[i] is Control child)
            {
                double w = Math.Min(child.DesiredSize.Width, finalSize.Width);

                child.Arrange(new Rect(0, 0, w, heights[i]));

                var tt = EnsureTransform(child, ys[i]);
                tt.X = finalSize.Width - w;
                tt.Y = ys[i];
            }
        }

        return finalSize;
    }

    private static TranslateTransform EnsureTransform(Control c, double initialY)
    {
        if (c.RenderTransform is TranslateTransform existing) return existing;

        var tt = new TranslateTransform { X = 0, Y = initialY };
        tt.Transitions = new Transitions
        {
            new DoubleTransition
            {
                Property = TranslateTransform.YProperty,
                Duration = TimeSpan.FromMilliseconds(420),
                Easing = new ExponentialEaseOut()
            }
        };
        c.RenderTransform = tt;
        return tt;
    }

    private double[] ComputePositions(double[] heights, double containerHeight)
    {
        int n = heights.Length;
        var ys = new double[n];

        double fullTotal = heights.Sum() + (n - 1) * Gap;

        if (_expanded)
        {
            double y = 0;
            for (int i = 0; i < n; i++)
            {
                ys[i] = y;
                y += heights[i] + Gap;
            }
        }
        else if (fullTotal <= containerHeight)
        {
            double y = 0;
            for (int i = 0; i < n; i++)
            {
                ys[i] = y;
                y += heights[i] + Gap;
            }
        }
        else
        {
            double lastH = heights[n - 1];
            double maxStack = n > 1 ? (containerHeight - lastH) / (n - 1) : containerHeight;
            double effectiveStack = Math.Min(StackedVisible, maxStack);
            if (effectiveStack < 4) effectiveStack = 4;

            ys[0] = 0;
            for (int i = 1; i < n; i++)
                ys[i] = ys[i - 1] + effectiveStack;
        }

        return ys;
    }
}