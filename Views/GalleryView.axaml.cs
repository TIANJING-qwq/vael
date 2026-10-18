using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using SBtools.Models;
using SBtools.Services;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;

namespace SBtools.Views;

public partial class GalleryView : UserControl
{
    private const int PageSize = 60;

    // ★ 最多保留在内存中的项数。超出后自动淘汰最早加载的。
    private const int MaxLoadedItems = 180;

    private readonly ObservableCollection<GalleryItem> _items = new();
    private List<GalleryItem> _allItems = new();
    private int _nextIndex;
    private bool _loaded;
    private bool _loading;

    public GalleryView()
    {
        InitializeComponent();

        var list = this.FindControl<ItemsControl>("ImageList");
        if (list != null) list.ItemsSource = _items;

        AttachedToVisualTree += (_, _) =>
        {
            if (!_loaded)
            {
                _loaded = true;
                UpdateUiState();
                if (GalleryService.HasCache)
                    LoadAllMetadata();
            }
        };

        DetachedFromVisualTree += (_, _) =>
        {
            ReleaseAll();
            GalleryService.CloseCache();
            LogService.Log("图库页面已关闭", "图库");
        };
    }

    private void ReleaseAll()
    {
        foreach (var item in _items) item.DisposeAll();
        _items.Clear();
        _allItems.Clear();
        _nextIndex = 0;
    }

    // ============================================================
    // 扫描元数据（不读图片字节）
    // ============================================================
    private void LoadAllMetadata()
    {
        try
        {
            ReleaseAll();
            _allItems = GalleryService.LoadAll();

            LogService.Log($"扫描完成，共 {_allItems.Count} 张", "图库");

            if (_allItems.Count == 0)
            {
                ShowEmptyState("图库资源为空或损坏", "请点「更新资源」重新下载");
                return;
            }

            LoadNextPage();
        }
        catch (Exception ex)
        {
            LogService.Log($"加载图库失败: {ex.Message}", "图库");
        }
    }

    // ============================================================
    // 加载下一页 + 淘汰老项
    // ============================================================
    private void LoadNextPage()
    {
        if (_loading) return;
        if (_nextIndex >= _allItems.Count) return;

        _loading = true;

        try
        {
            var batch = _allItems.Skip(_nextIndex).Take(PageSize).ToList();

            foreach (var item in batch)
                _items.Add(item);

            _nextIndex += batch.Count;

            // ★ 淘汰超出限额的最老项（从前面移除）
            while (_items.Count > MaxLoadedItems)
            {
                var oldest = _items[0];
                try
                {
                    oldest.Unload();          // 释放 Bitmap + Data
                    oldest.DisposeAll();      // 彻底释放
                }
                catch { }
                _items.RemoveAt(0);
            }

            UpdateLoadMoreButton();

            LogService.Log($"已加载 {_items.Count} / {_allItems.Count}（累计扫描 {_nextIndex}）", "图库");
        }
        finally
        {
            _loading = false;
        }
    }

    private void UpdateLoadMoreButton()
    {
        var btn = this.FindControl<Button>("LoadMoreButton");
        if (btn != null)
            btn.IsVisible = _nextIndex < _allItems.Count;
    }

    // ============================================================
    // 滚动到底部自动加载
    // ============================================================
    private void GalleryScroll_ScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        var sv = sender as ScrollViewer;
        if (sv == null) return;

        var distanceToBottom = sv.Extent.Height - sv.Offset.Y - sv.Viewport.Height;
        if (distanceToBottom < 800)
        {
            LoadNextPage();
        }
    }

    private void LoadMore_Click(object? sender, RoutedEventArgs e)
    {
        LoadNextPage();
    }

    // ============================================================
    // UI 状态
    // ============================================================
    private void UpdateUiState()
    {
        var hasCache = GalleryService.HasCache;
        var emptyPanel = this.FindControl<StackPanel>("EmptyPanel");
        var emptyTitle = this.FindControl<TextBlock>("EmptyTitle");
        var emptyHint = this.FindControl<TextBlock>("EmptyHint");
        var downloadBtn = this.FindControl<Button>("DownloadButton");
        var updateBtn = this.FindControl<Button>("UpdateButton");
        var refreshBtn = this.FindControl<Button>("RefreshButton");
        var scroll = this.FindControl<ScrollViewer>("GalleryScroll");
        var countText = this.FindControl<TextBlock>("CountText");

        if (hasCache)
        {
            if (emptyPanel != null) emptyPanel.IsVisible = false;
            if (scroll != null) scroll.IsVisible = true;
            if (updateBtn != null) updateBtn.IsEnabled = true;
            if (refreshBtn != null) refreshBtn.IsEnabled = true;

            var size = GalleryService.GetCacheSize() / 1024.0 / 1024.0;
            var time = GalleryService.GetCacheTime();
            if (countText != null)
                countText.Text = $"（{size:F1} MB，{time:yyyy-MM-dd HH:mm}）";
        }
        else
        {
            if (emptyPanel != null) emptyPanel.IsVisible = true;
            if (scroll != null) scroll.IsVisible = false;
            if (updateBtn != null) updateBtn.IsEnabled = false;
            if (refreshBtn != null) refreshBtn.IsEnabled = false;
            if (countText != null) countText.Text = "";

            if (emptyTitle != null) emptyTitle.Text = "还没有图库资源";
            if (emptyHint != null)
                emptyHint.Text =
                    "点击「下载图库」从 GitHub 下载图片资源\n" +
                    "下载完成后会自动加载并缓存到本地";
            if (downloadBtn != null) downloadBtn.Content = "下载图库";
        }
    }

    private void ShowEmptyState(string title, string hint)
    {
        var emptyPanel = this.FindControl<StackPanel>("EmptyPanel");
        var emptyTitle = this.FindControl<TextBlock>("EmptyTitle");
        var emptyHint = this.FindControl<TextBlock>("EmptyHint");
        var scroll = this.FindControl<ScrollViewer>("GalleryScroll");

        if (emptyPanel != null) emptyPanel.IsVisible = true;
        if (scroll != null) scroll.IsVisible = false;
        if (emptyTitle != null) emptyTitle.Text = title;
        if (emptyHint != null) emptyHint.Text = hint;
    }

    // ============================================================
    // 下载 / 更新
    // ============================================================
    private async void Download_Click(object? sender, RoutedEventArgs e)
    {
        await DownloadFlowAsync();
    }

    private async void Update_Click(object? sender, RoutedEventArgs e)
    {
        await DownloadFlowAsync();
    }

    private async Task DownloadFlowAsync()
    {
        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner == null) return;

        var dialog = new GalleryDownloadDialog();
        dialog.Show(owner);

        var progress = new Progress<(int percent, string speed)>(p =>
        {
            dialog.UpdateProgress(p.percent, p.speed);
        });

        bool success;
        try
        {
            success = await GalleryService.DownloadAsync(progress, dialog.Token);
        }
        catch (Exception ex)
        {
            LogService.Log($"下载流程异常: {ex.Message}", "图库");
            success = false;
        }

        try { await dialog.CloseWithAnimationAsync(); } catch { }

        if (!success)
        {
            MainWindow.PushToast("下载失败",
                "请检查网络/代理设置，或前往 GitHub 手动下载");
            return;
        }

        MainWindow.PushToast("下载完成", "图库资源已就绪");
        UpdateUiState();
        LoadAllMetadata();
    }

    private void Refresh_Click(object? sender, RoutedEventArgs e)
    {
        if (!GalleryService.HasCache)
        {
            UpdateUiState();
            return;
        }

        LoadAllMetadata();
        MainWindow.PushToast("图库", $"已刷新，共 {_allItems.Count} 张");
    }

    private void ImageItem_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // ★ 双击（触摸为双击/双击手势）才打开大图，单击只做选中反馈
        if (e.ClickCount < 2) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        if (sender is Control c && c.DataContext is GalleryItem item)
        {
            try
            {
                var owner = TopLevel.GetTopLevel(this) as Window;
                if (owner == null) return;

                var win = new ImageViewerWindow(item);
                win.ShowDialog(owner);
                e.Handled = true;
            }
            catch (Exception ex)
            {
                LogService.Log($"打开图片失败: {ex.Message}", "图库");
            }
        }
    }
}