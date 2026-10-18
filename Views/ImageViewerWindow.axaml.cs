using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using SBtools.Models;
using SBtools.Services;
using System;
using System.IO;

namespace SBtools.Views;

public partial class ImageViewerWindow : Window
{
    private Bitmap? _fullImage;
    private MemoryStream? _stream;

    // 缩放 / 平移
    private readonly ScaleTransform _scaleTransform = new();
    private readonly TranslateTransform _translateTransform = new();
    private double _scale = 1.0;
    private bool _dragging;
    private Point _lastPoint;

    public ImageViewerWindow()
    {
        InitializeComponent();

        var tg = new TransformGroup();
        tg.Children.Add(_scaleTransform);
        tg.Children.Add(_translateTransform);
        MainImage.RenderTransform = tg;
    }

    public ImageViewerWindow(GalleryItem item) : this()
    {
        if (item == null) return;

        NameText.Text = item.Name;

        try
        {
            // ★ 从原始数据重新解码原图（不受缩略图影响）
            var data = item.GetData();
            if (data == null || data.Length == 0)
            {
                NameText.Text = $"{item.Name} (数据为空)";
                return;
            }

            _stream = new MemoryStream(data);
            _fullImage = new Bitmap(_stream);
            MainImage.Source = _fullImage;
        }
        catch (Exception ex)
        {
            NameText.Text = $"{item.Name} (加载失败: {ex.Message})";
            LogService.Log($"打开大图失败: {ex.Message}", "图库");
        }
    }

    private void Close_Click(object? sender, RoutedEventArgs e) => Close();

    // ============================================================
    // 缩放 / 平移
    // ============================================================
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        try
        {
            var factor = e.Delta.Y > 0 ? 1.15 : 1 / 1.15;
            _scale = Math.Clamp(_scale * factor, 0.2, 8.0);
            _scaleTransform.ScaleX = _scale;
            _scaleTransform.ScaleY = _scale;
            e.Handled = true;
        }
        catch { }
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        try
        {
            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _dragging = true;
                _lastPoint = e.GetPosition(this);
            }
        }
        catch { }
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        try
        {
            if (!_dragging) return;
            var pos = e.GetPosition(this);
            _translateTransform.X += pos.X - _lastPoint.X;
            _translateTransform.Y += pos.Y - _lastPoint.Y;
            _lastPoint = pos;
        }
        catch { }
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _dragging = false;
    }

    private void Reset_Click(object? sender, RoutedEventArgs e)
    {
        _scale = 1.0;
        _scaleTransform.ScaleX = 1.0;
        _scaleTransform.ScaleY = 1.0;
        _translateTransform.X = 0;
        _translateTransform.Y = 0;
    }

    protected override void OnClosed(EventArgs e)
    {
        try
        {
            MainImage.Source = null;
            _fullImage?.Dispose();
            _stream?.Dispose();
        }
        catch { }

        _fullImage = null;
        _stream = null;

        base.OnClosed(e);
    }
}