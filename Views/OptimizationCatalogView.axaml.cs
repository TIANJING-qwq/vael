using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class OptimizationCatalogView : UserControl
{
    private readonly ObservableCollection<OptimizationItem> _filtered = new();
    private readonly List<Button> _catCards = new();
    private string? _selectedCat;
    private bool _busy;

    public OptimizationCatalogView()
    {
        InitializeComponent();

        BuildCategoryCards();
        ItemList.ItemsSource = _filtered;
        ApplyFilter();
    }

    // ============================================================
    // 分类方块卡片
    // ============================================================
    private void BuildCategoryCards()
    {
        _catCards.Clear();
        CategoryCardsHost.Children.Clear();

        AddCategoryCard(null, "全部");
        foreach (var c in SystemOptimizer.AllItems.Select(i => i.Category).Distinct())
        {
            var count = SystemOptimizer.AllItems.Count(i => i.Category == c);
            AddCategoryCard(c, $"{c}  {count}");
        }
        UpdateCardStyles();
    }

    private void AddCategoryCard(string? cat, string label)
    {
        var card = new Button
        {
            Content = new TextBlock { Text = label, FontSize = 12.5 },
            Tag = cat,
            Padding = new Avalonia.Thickness(14, 8),
            Margin = new Avalonia.Thickness(0, 0, 10, 10),
            MinWidth = 110,
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            CornerRadius = new Avalonia.CornerRadius(8),
            Background = Avalonia.Media.Brush.Parse("#22FFFFFF"),
            BorderThickness = new Avalonia.Thickness(1),
            BorderBrush = Avalonia.Media.Brush.Parse("#33FFFFFF"),
            Foreground = Avalonia.Media.Brushes.White,
        };
        card.Click += CategoryCard_Click;
        _catCards.Add(card);
        CategoryCardsHost.Children.Add(card);
    }

    private void CategoryCard_Click(object? sender, RoutedEventArgs e)
    {
        _selectedCat = (sender as Button)?.Tag as string;
        UpdateCardStyles();
        ApplyFilter();
    }

    private void UpdateCardStyles()
    {
        // 按当前主题取中性底色（浅色下用半透明黑，深色下用半透明白），两种模式都可见
        bool dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        foreach (var card in _catCards)
        {
            bool selected = (card.Tag as string) == _selectedCat;
            card.Background = selected
                ? Avalonia.Media.Brush.Parse("#0078D4")
                : Avalonia.Media.Brush.Parse(dark ? "#22FFFFFF" : "#14000000");
            card.BorderBrush = selected
                ? Avalonia.Media.Brush.Parse("#0078D4")
                : Avalonia.Media.Brush.Parse(dark ? "#33FFFFFF" : "#29000000");
        }
    }

    private void SearchBox_TextChanged(object? sender, TextChangedEventArgs e) => ApplyFilter();

    private void ApplyFilter()
    {
        var kw = (SearchBox.Text ?? "").Trim();

        var list = SystemOptimizer.AllItems.Where(i =>
            (_selectedCat == null || i.Category == _selectedCat) &&
            (kw.Length == 0 ||
             i.Name.Contains(kw, StringComparison.OrdinalIgnoreCase) ||
             i.Description.Contains(kw, StringComparison.OrdinalIgnoreCase)));

        _filtered.Clear();
        foreach (var i in list)
            _filtered.Add(i);

        CountText.Text = _selectedCat == null
            ? $"共 {_filtered.Count} 项"
            : $"{_selectedCat} · {_filtered.Count} 项";
        EmptyHint.IsVisible = _filtered.Count == 0;
    }

    private async void ApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OptimizationItem item })
            await RunItemAsync(item, apply: true);
    }

    private async void RevertButton_Click(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: OptimizationItem item })
            await RunItemAsync(item, apply: false);
    }

    private async Task RunItemAsync(OptimizationItem item, bool apply)
    {
        if (_busy) { Log("⏳ 有操作正在执行，请稍候"); return; }
        if (item.Risk == RiskLevel.Advanced && apply)
        {
            Log($"⚠ 「{item.Name}」为高级操作且可能不可逆，已跳过；请在系统优化分类页单独确认使用");
            return;
        }

        _busy = true;
        Log($"{(apply ? "应用" : "还原")}: {item.Name} …");
        try
        {
            var r = apply
                ? await SystemOptimizer.ApplyAsync(item.Id)
                : await SystemOptimizer.RevertAsync(item.Id);
            Log(r.Success
                ? $"✓ {item.Name} {(apply ? "已应用" : "已还原")}{(r.Success && item.Restart ? "（重启后生效）" : "")}"
                : $"✗ {item.Name} 失败: {r.Message}");
        }
        catch (Exception ex)
        {
            Log($"✗ {item.Name} 异常: {ex.Message}");
        }
        finally { _busy = false; }
    }

    private async void ApplySafeButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_busy) return;

        var safeItems = SystemOptimizer.AllItems
            .Where(i => i.Risk == RiskLevel.Safe && i.Apply != null)
            .ToList();

        _busy = true;
        Log($"===== 开始批量应用 {safeItems.Count} 项安全优化 =====");
        int ok = 0;
        foreach (var item in safeItems)
        {
            try
            {
                var r = await SystemOptimizer.ApplyAsync(item.Id);
                if (r.Success) ok++;
                Log($"{(r.Success ? "✓" : "✗")} {item.Name}{(r.Success ? "" : " - " + r.Message)}");
            }
            catch (Exception ex) { Log($"✗ {item.Name} - {ex.Message}"); }
        }
        Log($"===== 批量完成：成功 {ok}/{safeItems.Count} =====");
        _busy = false;
    }

    private void Log(string msg)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogPanel.Children.Add(new TextBlock
            {
                Text = $"[{DateTime.Now:HH:mm:ss}] {msg}",
                FontSize = 11,
                Foreground = Brushes.LightGray,
                TextWrapping = TextWrapping.Wrap,
            });
            while (LogPanel.Children.Count > 150)
                LogPanel.Children.RemoveAt(0);
        });
    }
}
