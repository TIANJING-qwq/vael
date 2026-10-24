using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform;
using Microsoft.Web.WebView2.Core;
using SBtools.Services;
using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SBtools.Views;

/// <summary>
/// 全屏播放器：用 Edge WebView2 打开央视官方直播网页（tv.cctv.com），
/// 复用官方播放器与官方 CDN 流，避免第三方 IPTV 源不稳定。
/// 网页加载完成后自动注入 JS 播放视频并铺满全屏。
/// 关闭方式：右上角悬浮 ✕ / Esc / Alt+F4 / 托盘「关闭播放」。
/// </summary>
public partial class CctvWebPlayerWindow : Window
{
    private readonly string _url;
    private CoreWebView2Controller? _controller;
    private CoreWebView2Environment? _environment;
    private Window? _closeBtn;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(IntPtr hWnd, out RECT lpRect);

    public CctvWebPlayerWindow() : this("") { }

    public CctvWebPlayerWindow(string url)
    {
        _url = url;
        InitializeComponent();

        KeyDown += OnKeyDown;
        Closing += OnClosing;

        if (!PlatformInfo.CanUseCctv)
        {
            // 非 Windows 平台：不支持 WebView2，直接提示并关闭
            Opened += (_, _) =>
            {
                LogService.Log("当前平台不支持 WebView2，无法播放 CCTV 直播", "播放");
                Close();
            };
            return;
        }

        Opened += async (_, _) => await InitWebViewAsync();
    }

    /// <summary>从频道名解析央视官方直播页地址（如 CCTV-13 新闻 → https://tv.cctv.com/live/cctv13/）</summary>
    public static string? GetCctvLiveUrl(string channelName)
    {
        var m = Regex.Match(channelName ?? "", @"CCTV[- ]?(\d+)");
        if (!m.Success) return null;
        return $"https://tv.cctv.com/live/cctv{m.Groups[1].Value}/";
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        CleanupWebView();
        try { _closeBtn?.Close(); } catch { }
        _closeBtn = null;
    }

    private async Task InitWebViewAsync()
    {
        try
        {
            var handle = TryGetPlatformHandle();
            if (handle == null)
            {
                LogService.Log("WebView2 初始化失败：无法获取窗口句柄", "播放");
                return;
            }

            _environment = await CoreWebView2Environment.CreateAsync(
                null, null, new CoreWebView2EnvironmentOptions());

            _controller = await _environment.CreateCoreWebView2ControllerAsync(handle.Handle);

            ApplyBounds();

            var settings = _controller.CoreWebView2.Settings;
            settings.AreDefaultContextMenusEnabled = false;
            settings.AreDevToolsEnabled = false;
            settings.IsStatusBarEnabled = false;
            settings.IsZoomControlEnabled = false;

            // ★ 网页加载完成后自动播放 + 视频铺满全屏
            _controller.CoreWebView2.NavigationCompleted += (_, _) => InjectAutoPlayScript();

            _controller.IsVisible = true;

            if (!string.IsNullOrEmpty(_url))
                _controller.CoreWebView2.Navigate(_url);

            LogService.Log($"WebView2 播放器已启动：{_url}", "播放");
        }
        catch (Exception ex)
        {
            LogService.Log($"WebView2 初始化失败: {ex.Message}", "播放");
        }

        ShowFloatingCloseButton();
    }

    /// <summary>注入 JS：隐藏央视页面 UI，找到 video 元素铺满全屏并自动播放（带重试）。</summary>
    private void InjectAutoPlayScript()
    {
        try
        {
            if (_controller?.CoreWebView2 == null) return;

            const string script = @"
(function(){
    var HIDE = '.floatNav,.nav_wrapper_bg,.header_nav,.header,.top_header,header,.nav,nav,.navbar,.menu,.sideNav,.side_nav,.floatBar,.footer,footer,.control_bar,.controlBar,.vjs-control-bar,.player_control,.play_control,.ctrl_bar,.topbar,.top_bar,.logo,.logoBar,.mask,.tip';
    function force(){
        var v = document.querySelector('video');
        if (!v) return false;

        // 1. 隐藏央视页面 / 播放器的 UI 控件
        var els = document.querySelectorAll(HIDE);
        for (var i=0;i<els.length;i++){ if(els[i]!==v) els[i].style.display='none'; }

        // 2. 视频自身铺满全屏并置顶
        v.muted = false;
        v.setAttribute('playsinline','false');
        v.style.position = 'fixed';
        v.style.top = '0'; v.style.left = '0';
        v.style.width = '100vw'; v.style.height = '100vh';
        v.style.objectFit = 'contain';
        v.style.zIndex = '2147483647';
        v.style.backgroundColor = 'black';
        document.body.style.overflow = 'hidden';
        document.documentElement.style.overflow = 'hidden';

        // 3. 消除父级 transform/filter 导致 fixed 失效的问题
        var p = v.parentElement;
        while (p && p !== document.body && p !== document.documentElement) {
            p.style.transform = 'none';
            p.style.filter = 'none';
            p.style.overflow = 'visible';
            p = p.parentElement;
        }

        try { var pr = v.play(); if (pr && pr.catch) pr.catch(function(){}); } catch(e){}
        return true;
    }
    var n = 0;
    var t = setInterval(function(){ n++; if(force() || n > 60) clearInterval(t); }, 500);
})();
";

            _controller.CoreWebView2.ExecuteScriptAsync(script);
        }
        catch (Exception ex)
        {
            LogService.Log($"自动播放脚本注入失败: {ex.Message}", "播放");
        }
    }

    private void ApplyBounds()
    {
        try
        {
            if (_controller == null) return;
            var handle = TryGetPlatformHandle();
            if (handle == null) return;

            _controller.BoundsMode = CoreWebView2BoundsMode.UseRawPixels;
            GetClientRect(handle.Handle, out var rect);
            _controller.Bounds = new System.Drawing.Rectangle(
                0, 0, rect.Right - rect.Left, rect.Bottom - rect.Top);
        }
        catch (Exception ex)
        {
            LogService.Log($"WebView2 尺寸调整失败: {ex.Message}", "播放");
        }
    }

    private void ShowFloatingCloseButton()
    {
        try
        {
            var screen = Screens.Primary;
            int right = screen != null ? screen.Bounds.X + screen.Bounds.Width : 1920;
            int top = screen != null ? screen.Bounds.Y : 0;

            _closeBtn = new Window
            {
                Width = 52,
                Height = 52,
                SystemDecorations = SystemDecorations.None,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Background = null,
                TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent },
            };

            var btn = new Button
            {
                Content = "✕",
                FontSize = 20,
                Width = 46,
                Height = 46,
                CornerRadius = new CornerRadius(23),
                Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x18, 0x18, 0x18)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            btn.Click += (_, _) => Close();

            _closeBtn.Content = btn;
            _closeBtn.Position = new PixelPoint(right - 64, top + 8);
            _closeBtn.Show();
        }
        catch (Exception ex)
        {
            LogService.Log($"悬浮关闭按钮创建失败: {ex.Message}", "播放");
        }
    }

    private void CleanupWebView()
    {
        try { _controller?.Close(); } catch { }
        _controller = null;
        _environment = null;
    }
}
