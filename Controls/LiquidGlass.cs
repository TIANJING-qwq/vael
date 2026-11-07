using Avalonia;
using Avalonia.Controls;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SBtools.Controls;

/// <summary>
/// 真液态玻璃（LiquidGlassUI 路数）：采样背景（桌面壁纸，按窗口位置对齐），
/// SKSL 运行时着色器在玻璃边缘做真实折射 —— 法向位移 + RGB 色散 + 顶缘高光 + 内阴影。
/// 不是"渐变叠加"的假玻璃：背景像素在边缘真实弯曲。
/// </summary>
public class LiquidGlass : Decorator
{
    // ============================================================
    // 壁纸（被采样的背景层）：物理像素坐标对齐窗口屏幕位置
    // ============================================================
    private static SKBitmap? _wall;
    private static SKBitmap? _wallBlurred;
    private static readonly SemaphoreSlim _wallLock = new(1, 1);

    [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
    private static extern bool SystemParametersInfo(uint action, uint size, string param, uint ini);

    private const uint SPI_GETDESKWALLPAPER = 0x0073;

    /// <summary>加载壁纸 + 生成磨砂副本（后台线程，一次）</summary>
    public static void EnsureWallpaper()
    {
        if (_wallBlurred != null) return;
        Task.Run(async () =>
        {
            await _wallLock.WaitAsync();
            try
            {
                if (_wallBlurred != null) return;
                var path = new string('\0', 260);
                if (!SystemParametersInfo(SPI_GETDESKWALLPAPER, 260, path, 0)) return;
                path = path.TrimEnd('\0');
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return;

                using var raw = SKBitmap.Decode(path);
                if (raw == null) return;

                // 限制到 2560 宽（性能）
                var scale = Math.Min(1f, 2560f / raw.Width);
                var w = (int)(raw.Width * scale);
                var h = (int)(raw.Height * scale);
                var wall = new SKBitmap(w, h);
                using (var c = new SKCanvas(wall))
                {
                    c.DrawBitmap(raw, new SKRect(0, 0, w, h),
                        new SKPaint { FilterQuality = SKFilterQuality.Medium });
                }

                // 磨砂副本（σ 随尺寸）：真模糊采样源
                var blur = new SKBitmap(w, h);
                using (var c = new SKCanvas(blur))
                {
                    c.DrawBitmap(wall, 0, 0, new SKPaint
                    {
                        FilterQuality = SKFilterQuality.Medium,
                        ImageFilter = SKImageFilter.CreateBlur(10, 10),
                    });
                }

                _wall = wall;
                _wallBlurred = blur;
                Logger.TryGet(LogEventLevel.Information, "LiquidGlass")?
                    .Log("LiquidGlass", null, "壁纸已加载，液态玻璃采样就绪");
            }
            catch { }
            finally { _wallLock.Release(); }
        });
    }

    // ============================================================
    // 折射着色器（SKSL）
    // ============================================================
    private const string Sksl = @"
uniform shader content;   // 磨砂壁纸（localMatrix 对齐到窗口屏幕位置）
uniform float2 size;      // 玻璃尺寸（px）
uniform float radius;     // 圆角
uniform float strength;   // 折射强度（边缘位移 px）
uniform float band;       // 边缘带宽 px
uniform float4 tint;      // 轻染色

float sdEdge(float dist, float band) {
    // SKSL 无 smoothstep：手工等价实现 t*t*(3-2t)
    float t = clamp(dist / band, 0.0, 1.0);
    return 1.0 - (t * t * (3.0 - 2.0 * t));
}

float4 main(float2 p) {
    float2 half_ = size * 0.5;
    // 圆角矩形最近点 → 外向法线
    float2 c = clamp(p, float2(radius), size - float2(radius));
    float2 diff = p - c;
    float dist = length(diff);
    float2 n = dist > 0.0001 ? diff / dist : float2(0.0);
    float edge = sdEdge(dist, band);  // 1=贴边

    // ★ 真折射：边缘处采样点向内拉（背景在玻璃边缘真实弯曲）
    float2 sp = p - n * strength * edge;
    // RGB 色散：三通道不同位移（真玻璃的 dispersion）
    float ca = strength * 0.12;
    float4 col;
    col.r = sample(content, sp + n * ca).r;
    col.g = sample(content, sp).g;
    col.b = sample(content, sp - n * ca).b;
    col.a = 1.0;

    // 顶缘镜面高光（法线朝上-left 更亮）
    float lit = clamp(dot(n, normalize(float2(-0.45, -0.9))), 0.0, 1.0);
    col.rgb += edge * edge * (0.14 + 0.34 * lit);
    // 底缘内阴影
    col.rgb *= 1.0 - edge * clamp(n.y, 0.0, 1.0) * 0.22;
    // 淡染色
    col.rgb = col.rgb * (1.0 - tint.a) + tint.rgb * tint.a;
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
            var eff = SKRuntimeEffect.Create(Sksl, out var err);
            if (eff == null)
                Logger.TryGet(LogEventLevel.Warning, "LiquidGlass")?
                    .Log("LiquidGlass", null, "SKSL 编译失败（回退磨砂）: " + err);
            return _effect = eff;
        }
    }

    public static readonly StyledProperty<float> CornerRadiusProperty =
        AvaloniaProperty.Register<LiquidGlass, float>(nameof(CornerRadius), 16f);
    public float CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    /// <summary>折射强度（px）</summary>
    public static readonly StyledProperty<float> StrengthProperty =
        AvaloniaProperty.Register<LiquidGlass, float>(nameof(Strength), 13f);
    public float Strength
    {
        get => GetValue(StrengthProperty);
        set => SetValue(StrengthProperty, value);
    }

    // ============================================================
    // 渲染
    // ============================================================
    private PixelPoint _lastPos;
    private readonly DispatcherTimer _sync = new() { Interval = TimeSpan.FromMilliseconds(200) };

    public LiquidGlass()
    {
        EnsureWallpaper();
        // 窗口移动后重采样（localMatrix 跟随屏幕位置）
        _sync.Tick += (_, _) =>
        {
            var pos = (VisualRoot as Window)?.Position ?? default;
            if (pos != _lastPos) { _lastPos = pos; InvalidateVisual(); }
        };
        _sync.Start();
        AttachedToVisualTree += (_, _) => { _lastPos = (VisualRoot as Window)?.Position ?? default; InvalidateVisual(); };
    }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width > 4 && Bounds.Height > 4 && _wallBlurred != null)
            context.Custom(new GlassOp(this));
        else
        {
            // 壁纸未就绪：轻磨砂底（降级，不静默失败）
            context.FillRectangle(new SolidColorBrush(Color.Parse("#38282828")),
                new Rect(Bounds.Size), CornerRadius);
        }
        base.Render(context);
    }

    private sealed class GlassOp : ICustomDrawOperation
    {
        private readonly LiquidGlass _owner;
        public GlassOp(LiquidGlass owner) => _owner = owner;

        public Rect Bounds => _owner.Bounds;

        public void Dispose() { }

        public bool Equals(ICustomDrawOperation? other) => false;
        public bool HitTest(Point p) => true;

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature(typeof(ISkiaSharpApiLeaseFeature)) as ISkiaSharpApiLeaseFeature;
            if (leaseFeature == null) return;
            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            var win = _owner.VisualRoot as Window;
            var scale = (float)(win?.RenderScaling ?? 1.0);
            var pos = win?.Position ?? default;
            var screen = win?.Screens.Primary?.Bounds;
            var wall = _wallBlurred!;

            // local px → 壁纸像素：壁纸铺满主屏（物理像素）
            float sx = screen is { } s && s.Width > 0 ? wall.Width / (float)s.Width * scale : scale;
            float sy = screen is { } s2 && s2.Height > 0 ? wall.Height / (float)s2.Height * scale : scale;
            float tx = pos.X - (screen?.X ?? 0) * (sx / scale);
            float ty = pos.Y - (screen?.Y ?? 0) * (sy / scale);

            var size = _owner.Bounds.Size;
            var local = SKMatrix.CreateScaleTranslation(sx, sy, tx, ty);
            using var contentShader = _wallBlurred!.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, local);

            if (Effect == null)
            {
                // 着色器不可用：回退为直接磨砂底（三级降级第二级），不在渲染循环抛异常
                using var p2 = new SKPaint { Shader = contentShader, IsAntialias = true };
                canvas.DrawRoundRect(0, 0, (float)size.Width, (float)size.Height,
                    _owner.CornerRadius, _owner.CornerRadius, p2);
                return;
            }

            var uniforms = new SKRuntimeEffectUniforms(Effect)
            {
                ["size"] = new[] { (float)size.Width, (float)size.Height },
                ["radius"] = _owner.CornerRadius,
                ["strength"] = _owner.Strength,
                ["band"] = 20f,
                ["tint"] = new[] { 0.16f, 0.17f, 0.2f, 0.10f },
            };
            var children = new SKRuntimeEffectChildren(Effect)
            {
                ["content"] = contentShader,
            };
            using var shader = Effect.ToShader(false, uniforms, children);
            using var paint = new SKPaint { Shader = shader, IsAntialias = true };
            canvas.DrawRect(0, 0, (float)size.Width, (float)size.Height, paint);
        }
    }
}
