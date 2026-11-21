using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Microsoft.Win32;
using SkiaSharp;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Controls;

/// <summary>
/// 真液态玻璃（LiquidGlassUI 路数）：采样桌面壁纸 + SKSL 运行时着色器，
/// 玻璃边缘做真实折射（法向位移 + RGB 色散 + 顶缘镜面 + 底缘内阴影）。
///
/// 两条教训（上一版崩过两次）：
///  1) ICustomDrawOperation.Render 在【合成器线程】执行 —— 所有 Avalonia 属性
///     必须在 UI 线程的 Render(DrawingContext) 里捕获成纯值传进来；
///  2) SKSL 方言没有 smoothstep/step —— 一律用 clamp 手工实现，
///     且着色器编译失败只降级不抛异常（渲染循环抛异常会连环崩）。
/// </summary>
public class LiquidGlass : Decorator
{
    // ============================================================
    // 壁纸（被采样的背景层）
    // ============================================================
    private static SKBitmap? _wall;          // 缩放后的壁纸
    private static SKBitmap? _wallBlurred;   // 磨砂副本（真模糊采样源）
    private static WallMap _map = WallMap.Fill;
    private static bool _loading;
    private static readonly object _lock = new();

    /// <summary>壁纸对齐方式（读注册表 WallpaperStyle）</summary>
    private struct WallMap
    {
        public bool Tile;
        public float Sx, Sy;      // 屏幕像素 → 壁纸像素的除法系数
        public float OffX, OffY;  // 屏幕坐标下的偏移（px）

        public static WallMap Fill => new() { Sx = 1, Sy = 1 };
    }

    public static event Action? WallpaperReady;

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint size, string param, uint ini);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index); // 0=SM_CXSCREEN 1=SM_CYSCREEN

    private const uint SPI_GETDESKWALLPAPER = 0x0073;

    public static void EnsureWallpaper()
    {
        lock (_lock) { if (_loading || _wallBlurred != null) return; _loading = true; }
        Task.Run(() =>
        {
            try
            {
                var buf = new string('\0', 512);
                if (!SystemParametersInfo(SPI_GETDESKWALLPAPER, 512, buf, 0)) return;
                var path = buf.TrimEnd('\0');
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                using var raw = SKBitmap.Decode(path);
                if (raw == null) return;

                // 缩到 ≤1920 宽：模糊更快，折射精度足够
                var k = Math.Min(1f, 1920f / raw.Width);
                int w = Math.Max(1, (int)(raw.Width * k)), h = Math.Max(1, (int)(raw.Height * k));
                var wall = new SKBitmap(w, h);
                using (var c = new SKCanvas(wall))
                    c.DrawBitmap(raw, new SKRect(0, 0, w, h), new SKPaint { FilterQuality = SKFilterQuality.Medium });

                var blur = new SKBitmap(w, h);
                using (var c = new SKCanvas(blur))
                    c.DrawBitmap(wall, 0, 0, new SKPaint
                    {
                        FilterQuality = SKFilterQuality.Medium,
                        ImageFilter = SKImageFilter.CreateBlur(9, 9),
                    });

                _wall = wall;
                _wallBlurred = blur;
                _map = ReadWallpaperStyle(raw.Width, raw.Height);
                Logger.TryGet(LogEventLevel.Information, "LiquidGlass")?
                    .Log("LiquidGlass", null, $"壁纸已就绪 {w}x{h}，对齐=Sx {_map.Sx:0.##} Sy {_map.Sy:0.##} Off {_map.OffX:0},{_map.OffY:0}");
                WallpaperReady?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.TryGet(LogEventLevel.Warning, "LiquidGlass")?
                    .Log("LiquidGlass", null, "壁纸加载失败（回退磨砂）: " + ex.Message);
            }
            finally { lock (_lock) _loading = false; }
        });
    }

    /// <summary>读取 Windows 壁纸填充方式（10/22=填充裁切，6=适应，2=拉伸，1/0=居中/平铺）</summary>
    private static WallMap ReadWallpaperStyle(int wallW, int wallH)
    {
        var m = WallMap.Fill;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
            var style = key?.GetValue("WallpaperStyle")?.ToString() ?? "10";
            var tile = key?.GetValue("TileWallpaper")?.ToString() ?? "0";
            float sw = Math.Max(1, GetSystemMetrics(0)), sh = Math.Max(1, GetSystemMetrics(1));

            if (tile == "1") { m.Tile = true; return m; }
            switch (style)
            {
                case "10": // 填充：等比放大铺满，居中裁切
                case "22":
                {
                    var s = Math.Max(sw / wallW, sh / wallH);
                    m.Sx = m.Sy = s;
                    m.OffX = (sw - wallW * s) / 2f;
                    m.OffY = (sh - wallH * s) / 2f;
                    return m;
                }
                case "2": // 拉伸：非等比
                    m.Sx = sw / wallW;
                    m.Sy = sh / wallH;
                    m.OffX = 0; m.OffY = 0;
                    return m;
                case "6": // 适应：等比缩到完整可见
                {
                    var s = Math.Min(sw / wallW, sh / wallH);
                    m.Sx = m.Sy = s;
                    m.OffX = (sw - wallW * s) / 2f;
                    m.OffY = (sh - wallH * s) / 2f;
                    return m;
                }
                default: // 居中/平铺：1:1 居中
                    m.Sx = m.Sy = 1;
                    m.OffX = (sw - wallW) / 2f;
                    m.OffY = (sh - wallH) / 2f;
                    return m;
            }
        }
        catch { return m; }
    }

    // ============================================================
    // 折射着色器（SKSL · 无 smoothstep）
    // ============================================================
    private const string Sksl = @"
uniform shader content;
uniform float2 size;
uniform float radius;
uniform float strength;
uniform float band;
uniform float3 tint;
uniform float tintA;

float4 main(float2 p) {
    // 圆角矩形最近点 → 外向法线
    float2 c = clamp(p, float2(radius, radius), size - float2(radius, radius));
    float2 diff = p - c;
    float dist = length(diff);
    float2 n = dist > 0.0001 ? (diff / dist) : float2(0.0);

    // 边缘带权重：clamp 版 smoothstep（SKSL 无 smoothstep）
    float t = clamp(dist / band, 0.0, 1.0);
    float edge = 1.0 - t * t * (3.0 - 2.0 * t);

    // ★ 真折射：边缘处采样点向玻璃内侧收（背景被玻璃边缘弯折）
    float2 sp = p - n * (strength * edge);

    // 色散：R/B 通道沿法线再错开一点（真玻璃的彩虹边）
    float ca = strength * 0.13 * edge;
    float4 col = sample(content, sp);
    col.r = sample(content, sp - n * ca).r;
    col.b = sample(content, sp + n * ca).b;

    // 顶缘镜面高光（光来自左上）
    float lit = clamp(dot(n, normalize(float2(-0.5, -0.86))), 0.0, 1.0);
    col.rgb += edge * edge * (0.10 + 0.32 * lit);
    // 底缘内阴影
    col.rgb *= 1.0 - edge * clamp(n.y, 0.0, 1.0) * 0.20;
    // 轻染色
    col.rgb = col.rgb * (1.0 - tintA) + tint * tintA;
    col.a = 1.0;
    return col;
}
";

    private static SKRuntimeEffect? _effect;
    private static bool _effectTried;
    private static SKRuntimeEffect? Effect
    {
        get
        {
            if (_effectTried) return _effect;
            _effectTried = true;
            try
            {
                var eff = SKRuntimeEffect.Create(Sksl, out var err);
                if (eff == null)
                    Logger.TryGet(LogEventLevel.Warning, "LiquidGlass")?
                        .Log("LiquidGlass", null, "SKSL 编译失败（回退磨砂）: " + err);
                return _effect = eff;
            }
            catch { return null; }
        }
    }

    // ============================================================
    // 属性
    // ============================================================
    public static readonly StyledProperty<float> CornerRadiusProperty =
        AvaloniaProperty.Register<LiquidGlass, float>(nameof(CornerRadius), 16f);
    public float CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public static readonly StyledProperty<float> StrengthProperty =
        AvaloniaProperty.Register<LiquidGlass, float>(nameof(Strength), 12f);
    /// <summary>折射强度（px）。留 0 使用自动值（按控件短边换算）</summary>
    public float Strength
    {
        get => GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    public static readonly StyledProperty<Color> TintProperty =
        AvaloniaProperty.Register<LiquidGlass, Color>(nameof(Tint), Color.Parse("#282828"));
    /// <summary>玻璃染色</summary>
    public Color Tint
    {
        get => GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public static readonly StyledProperty<double> TintOpacityProperty =
        AvaloniaProperty.Register<LiquidGlass, double>(nameof(TintOpacity), 0.10);
    public double TintOpacity
    {
        get => GetValue(TintOpacityProperty);
        set => SetValue(TintOpacityProperty, value);
    }

    // ============================================================
    // 渲染
    // ============================================================
    private readonly DispatcherTimer _sync = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private Window? _win;
    private bool _sawWallpaper;
    private PixelPoint _lastPos;

    public LiquidGlass()
    {
        EnsureWallpaper();
        WallpaperReady += OnWallpaperReady;
        _sync.Tick += (_, _) =>
        {
            // 仅在"壁纸刚就绪"或"窗口移动"时重绘，避免常驻 4fps 空转
            bool readyNow = _wallBlurred != null;
            var pos = _win?.Position ?? default;
            if ((readyNow && !_sawWallpaper) || pos != _lastPos)
            {
                _sawWallpaper = readyNow;
                _lastPos = pos;
                InvalidateVisual();
            }
        };
        AttachedToVisualTree += (_, _) =>
        {
            _win = VisualRoot as Window;
            _lastPos = _win?.Position ?? default;
            _sawWallpaper = _wallBlurred != null;
            _sync.Start();
            InvalidateVisual();
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _sync.Stop();
            WallpaperReady -= OnWallpaperReady;
        };
    }

    private void OnWallpaperReady()
        => Dispatcher.UIThread.Post(InvalidateVisual, DispatcherPriority.Render);

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (rect.Width > 6 && rect.Height > 6 && _wallBlurred != null)
        {
            // ★ 全部在 UI 线程捕获成纯值（合成器线程不可访问 Avalonia 属性）
            var win = VisualRoot as Window ?? _win;
            var screen = win != null ? GetScreenBounds(win) : default;
            context.Custom(new GlassOp(
                rect,
                CornerRadius,
                Strength > 0 ? Strength : AutoStrength(rect),
                Band(rect),
                Tint,
                (float)TintOpacity,
                win?.Position ?? default,
                (float)(win?.RenderScaling ?? 1.0),
                screen));
        }
        else
        {
            // 壁纸未就绪：磨砂实心底（三级降级第二级）
            context.FillRectangle(new SolidColorBrush(Color.Parse("#3A282828")), rect, CornerRadius);
        }
        base.Render(context);
    }

    private static float AutoStrength(Rect r) => Math.Clamp((float)Math.Min(r.Width, r.Height) * 0.20f, 6f, 22f);
    private static float Band(Rect r) => Math.Clamp((float)Math.Min(r.Width, r.Height) * 0.42f, 10f, 34f);

    private static PixelRect GetScreenBounds(Window win)
    {
        try { return win.Screens.ScreenFromWindow(win)?.Bounds ?? win.Screens.Primary?.Bounds ?? default; }
        catch { return default; }
    }

    private sealed class GlassOp : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly float _radius, _strength, _band, _tintA, _scale;
        private readonly Color _tint;
        private readonly PixelPoint _pos;
        private readonly PixelRect _screen;

        public GlassOp(Rect bounds, float radius, float strength, float band,
                       Color tint, float tintA, PixelPoint pos, float scale, PixelRect screen)
        {
            _bounds = bounds; _radius = radius; _strength = strength; _band = band;
            _tint = tint; _tintA = tintA; _pos = pos; _scale = scale; _screen = screen;
        }

        public Rect Bounds => _bounds;
        public void Dispose() { }
        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => true;

        public void Render(ImmediateDrawingContext context)
        {
            // 渲染线程：只做纯 Skia 操作；任何异常都吞掉（否则崩渲染循环）
            try
            {
                if (context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) is not ISkiaSharpApiLeaseFeature feat) return;
                using var lease = feat.Lease();
                var canvas = lease.SkCanvas;
                var wall = _wallBlurred!;
                var size = _bounds.Size;

                // 屏幕坐标 → 壁纸像素：按填充方式反算（支持拉伸的非等比）
                float sx = Math.Max(0.0001f, _map.Sx), sy = Math.Max(0.0001f, _map.Sy);
                float tx = (_pos.X - _screen.X - _map.OffX) / sx;
                float ty = (_pos.Y - _screen.Y - _map.OffY) / sy;
                var local = SKMatrix.CreateScaleTranslation(_scale / sx, _scale / sy, tx, ty);
                using var content = wall.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, local);

                float r = Math.Min(_radius, (float)size.Width / 2);
                float w = (float)size.Width, h = (float)size.Height;

                var eff = Effect;
                if (eff == null)
                {
                    // 着色器不可用：磨砂壁纸底（不抛异常）
                    using var p2 = new SKPaint { Shader = content, IsAntialias = true };
                    canvas.DrawRoundRect(0, 0, w, h, r, r, p2);
                    return;
                }

                var uniforms = new SKRuntimeEffectUniforms(eff)
                {
                    ["size"] = new[] { w, h },
                    ["radius"] = r,
                    ["strength"] = _strength,
                    ["band"] = Math.Min(_band, Math.Min(w, h) / 2),
                    ["tint"] = new[] { _tint.R / 255f, _tint.G / 255f, _tint.B / 255f },
                    ["tintA"] = _tintA,
                };
                var children = new SKRuntimeEffectChildren(eff) { ["content"] = content };
                using var shader = eff.ToShader(false, uniforms, children);
                using var paint = new SKPaint { Shader = shader, IsAntialias = true };
                canvas.DrawRect(0, 0, w, h, paint);
            }
            catch { /* 渲染线程静默降级 */ }
        }
    }
}
