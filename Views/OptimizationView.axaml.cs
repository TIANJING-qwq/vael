using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class OptimizationView : UserControl
{
    private readonly Dictionary<string, ToggleSwitch> _toggles = new();
    private readonly Dictionary<string, OptimizationItem> _items = new();

    // ★ 防止 toggle 回滚触发二次调用
    private bool _isUpdatingFromCode;

    public OptimizationView()
    {
        InitializeComponent();

        BuildCategory("服务优化", "ServicesPanel");
        BuildCategory("AI 组件", "AiPanel");
        BuildCategory("隐私", "PrivacyPanel");
        BuildCategory("性能", "PerformancePanel");
        BuildCategory("清理", "CleanupPanel");

        AttachedToVisualTree += async (_, _) =>
        {
            await Task.Delay(200);
            await RefreshAllStatusAsync();
        };
    }

    // ============================================================
    // 分类卡片 → 详情
    // ============================================================
    private void ServicesCard_Click(object? sender, RoutedEventArgs e) => ShowCategory("服务优化", "ServicesPanel");
    private void AiCard_Click(object? sender, RoutedEventArgs e) => ShowCategory("AI 组件", "AiPanel");
    private void PrivacyCard_Click(object? sender, RoutedEventArgs e) => ShowCategory("隐私", "PrivacyPanel");
    private void PerformanceCard_Click(object? sender, RoutedEventArgs e) => ShowCategory("性能", "PerformancePanel");
    private void CleanupCard_Click(object? sender, RoutedEventArgs e) => ShowCategory("清理", "CleanupPanel");

    private void ShowCategory(string title, string panelName)
    {
        CategoryCardsView.IsVisible = false;
        CategoryDetailView.IsVisible = true;
        DetailTitle.Text = title;
        SystemToolsHost.IsVisible = false;

        ServicesPanel.IsVisible = panelName == "ServicesPanel";
        AiPanel.IsVisible = panelName == "AiPanel";
        PrivacyPanel.IsVisible = panelName == "PrivacyPanel";
        PerformancePanel.IsVisible = panelName == "PerformancePanel";
        CleanupPanel.IsVisible = panelName == "CleanupPanel";
        AnimateIn(CategoryDetailView);
    }

    private SystemToolsView? _toolsView;
    private OptimizationCatalogView? _catalogView;
    private ToolboxView? _toolboxView;

    private void SystemToolsCard_Click(object? sender, RoutedEventArgs e)
    {
        _toolsView ??= new SystemToolsView();
        ShowHosted("系统工具", _toolsView);
    }

    private void CatalogCard_Click(object? sender, RoutedEventArgs e)
    {
        _catalogView ??= new OptimizationCatalogView();
        ShowHosted("优化大全", _catalogView);
    }

    private void ToolboxCard_Click(object? sender, RoutedEventArgs e)
    {
        _toolboxView ??= new ToolboxView();
        ShowHosted("实用工具箱", _toolboxView);
    }

    private void ShowHosted(string title, Control view)
    {
        CategoryCardsView.IsVisible = false;
        CategoryDetailView.IsVisible = true;
        DetailTitle.Text = title;

        ServicesPanel.IsVisible = false;
        AiPanel.IsVisible = false;
        PrivacyPanel.IsVisible = false;
        PerformancePanel.IsVisible = false;
        CleanupPanel.IsVisible = false;

        SystemToolsHost.IsVisible = true;
        if (!ReferenceEquals(SystemToolsHost.Content, view))
            SystemToolsHost.Content = view;
        AnimateIn(CategoryDetailView);
    }

    private void BackToCategories_Click(object? sender, RoutedEventArgs e)
    {
        CategoryDetailView.IsVisible = false;
        CategoryCardsView.IsVisible = true;
        SystemToolsHost.IsVisible = false;
        AnimateIn(CategoryCardsView);
    }

    /// <summary>详情/列表进入动画：淡入 + 上浮（与关于页风格一致）</summary>
    private static void AnimateIn(Control c)
    {
        try
        {
            c.Transitions = new Transitions
            {
                new DoubleTransition
                {
                    Property = OpacityProperty,
                    Duration = TimeSpan.FromMilliseconds(220),
                    Easing = new ExponentialEaseOut(),
                },
                new TransformOperationsTransition
                {
                    Property = RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(300),
                    Easing = new BackEaseOut(),
                },
            };
            c.Opacity = 0;
            c.RenderTransform = TransformOperations.Parse("translateY(14px)");
            Dispatcher.UIThread.Post(() =>
            {
                c.Opacity = 1;
                c.RenderTransform = TransformOperations.Parse("translateY(0px)");
            });
        }
        catch { }
    }

    private void BuildCategory(string category, string panelName)
    {
        var panel = this.FindControl<StackPanel>(panelName);
        if (panel == null) return;

        var items = SystemOptimizer.AllItems.Where(i => i.Category == category).ToList();
        foreach (var item in items)
        {
            _items[item.Id] = item;
            panel.Children.Add(BuildItemCard(item));
        }
    }

    private Border BuildItemCard(OptimizationItem item)
    {
        var (riskText, riskColor) = item.Risk switch
        {
            RiskLevel.Safe => ("安全", "#22AA22"),
            RiskLevel.Medium => ("中等", "#FFAA00"),
            RiskLevel.Advanced => ("高级", "#FF5555"),
            _ => ("未知", "#888888")
        };

        var toggle = new ToggleSwitch
        {
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            Margin = new Avalonia.Thickness(12, 0, 0, 0)
        };
        toggle.IsCheckedChanged += async (_, _) => await OnToggleChangedAsync(item.Id, toggle);
        _toggles[item.Id] = toggle;

        var riskBadge = new Border
        {
            Background = SolidColorBrush.Parse(riskColor),
            CornerRadius = new Avalonia.CornerRadius(10),
            Padding = new Avalonia.Thickness(8, 2, 8, 2),
            Margin = new Avalonia.Thickness(0, 4, 0, 0),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = riskText,
                FontSize = 10,
                Foreground = Brushes.White
            }
        };

        var infoPanel = new StackPanel
        {
            Spacing = 4,
            Children =
            {
                new TextBlock
                {
                    Text = item.Name,
                    FontSize = 14,
                    FontWeight = FontWeight.SemiBold
                },
                new TextBlock
                {
                    Text = item.Description,
                    FontSize = 12,
                    Opacity = 0.65,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                },
                riskBadge
            }
        };

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto")
        };
        Grid.SetColumn(infoPanel, 0);
        Grid.SetColumn(toggle, 1);
        grid.Children.Add(infoPanel);
        grid.Children.Add(toggle);

        return new Border
        {
            Background = Avalonia.Media.Brushes.Transparent,
            BorderBrush = SolidColorBrush.Parse("#30808080"),
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(16, 12),
            Child = grid
        };
    }

    // ============================================================
    // ★ 核心修复：防止回滚触发二次调用
    // ============================================================
    private async Task OnToggleChangedAsync(string id, ToggleSwitch toggle)
    {
        if (!_items.TryGetValue(id, out var item)) return;

        // ★ 如果正在程序化更新状态，忽略
        if (_isUpdatingFromCode) return;

        bool enable = toggle.IsChecked == true;

        if (item.Risk == RiskLevel.Advanced && enable)
        {
            var result = await ConfirmAsync($"「{item.Name}」是高级操作，可能无法恢复。\n确定继续吗？");
            if (!result)
            {
                _isUpdatingFromCode = true;
                toggle.IsChecked = false;
                _isUpdatingFromCode = false;
                return;
            }
        }

        AppendLog($"{(enable ? "应用" : "还原")}: {item.Name}");

        toggle.IsEnabled = false;

        (bool Success, string Message) result2;
        try
        {
            result2 = enable
                ? await SystemOptimizer.ApplyAsync(id)
                : await SystemOptimizer.RevertAsync(id);
        }
        catch (Exception ex)
        {
            result2 = (false, ex.Message);
        }

        toggle.IsEnabled = true;

        if (result2.Success)
        {
            AppendLog($"✓ {item.Name} {(enable ? "已应用" : "已还原")}");
        }
        else
        {
            AppendLog($"✗ {item.Name} 失败: {result2.Message}");

            // ★ 回滚 toggle，且用标志位防止重触发
            _isUpdatingFromCode = true;
            toggle.IsChecked = !enable;
            _isUpdatingFromCode = false;
        }

        UpdateStatusText();
    }

    // ============================================================
    // 刷新所有状态
    // ============================================================
    private async Task RefreshAllStatusAsync()
    {
        AppendLog("正在检测系统优化状态...");

        _isUpdatingFromCode = true;
        foreach (var (id, toggle) in _toggles)
        {
            try
            {
                var applied = await SystemOptimizer.CheckStatusAsync(id);
                toggle.IsChecked = applied;
            }
            catch { }
        }
        _isUpdatingFromCode = false;

        AppendLog("状态检测完成");
        UpdateStatusText();
    }

    // ============================================================
    // 一键应用推荐项
    // ============================================================
    private async void ApplyRecommendedButton_Click(object? sender, RoutedEventArgs e)
    {
        var safeItems = SystemOptimizer.AllItems
            .Where(i => i.Risk == RiskLevel.Safe)
            .Where(i => !i.Id.StartsWith("clean_"))
            .ToList();

        var confirm = await ConfirmAsync(
            $"将应用 {safeItems.Count} 项安全优化，每项都可随时还原。\n确定继续吗？");

        if (!confirm) return;

        AppendLog($"===== 开始应用 {safeItems.Count} 项推荐优化 =====");

        foreach (var item in safeItems)
        {
            if (!_toggles.TryGetValue(item.Id, out var toggle)) continue;
            if (toggle.IsChecked == true) continue;

            _isUpdatingFromCode = true;
            toggle.IsChecked = true;
            _isUpdatingFromCode = false;

            var result = await SystemOptimizer.ApplyAsync(item.Id);
            AppendLog($"{(result.Success ? "✓" : "✗")} {item.Name}" +
                     (result.Success ? "" : $" - {result.Message}"));
        }

        AppendLog("===== 推荐优化应用完成 =====");
        UpdateStatusText();
    }

    private async void RefreshStatusButton_Click(object? sender, RoutedEventArgs e)
    {
        await RefreshAllStatusAsync();
    }

    // ============================================================
    // 辅助
    // ============================================================
    private void UpdateStatusText()
    {
        int applied = _toggles.Count(t => t.Value.IsChecked == true);
        int total = _toggles.Count;
        var text = this.FindControl<TextBlock>("OptimizationStatusText");
        if (text != null)
            text.Text = $"共 {applied} 项已启用 / {total} 项可选";
    }

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var panel = this.FindControl<StackPanel>("StatusLogPanel");
            if (panel == null) return;

            var line = new TextBlock
            {
                Text = $"{DateTime.Now:HH:mm:ss}  {message}",
                FontFamily = new FontFamily("Consolas,Menlo,monospace"),
                FontSize = 11,
                Foreground = SolidColorBrush.Parse("#D4D4D4"),
                TextWrapping = Avalonia.Media.TextWrapping.NoWrap
            };
            panel.Children.Add(line);

            if (panel.Children.Count > 200)
                panel.Children.RemoveAt(0);

            var sv = this.FindControl<ScrollViewer>("StatusScrollViewer");
            sv?.ScrollToEnd();
        });
    }

    private async Task<bool> ConfirmAsync(string message)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not Window parent) return false;

        var dialog = new Window
        {
            Title = "确认操作",
            Width = 400,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false
        };

        bool result = false;

        var yesButton = new Button { Content = "确定", Padding = new Avalonia.Thickness(20, 8) };
        var noButton = new Button { Content = "取消", Padding = new Avalonia.Thickness(20, 8) };

        yesButton.Click += (_, _) => { result = true; dialog.Close(); };
        noButton.Click += (_, _) => { result = false; dialog.Close(); };

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(24),
            Spacing = 20,
            Children =
            {
                new TextBlock
                {
                    Text = message,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    FontSize = 14
                },
                new StackPanel
                {
                    Orientation = Avalonia.Layout.Orientation.Horizontal,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Spacing = 10,
                    Children = { noButton, yesButton }
                }
            }
        };

        await dialog.ShowDialog(parent);
        return result;
    }
}