using Avalonia;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using PCL.Core.Media;
using SkiaSharp;

namespace PCL.Core.UI.Media;

/// <summary>通过 Skia 直接绘制 YUV420p 视频帧，并在视频层完成背景模糊。</summary>
public sealed class YuvVideoFrameView : VideoSurface
{
    private const string ShaderSource =
        // 实际上是 SkSL 不过 Rider 不会给高亮
        // language=GLSL
        """
        uniform float3 rowR;
        uniform float3 rowG;
        uniform float3 rowB;
        uniform float3 offset;
        uniform shader yPlane;
        uniform shader uPlane;
        uniform shader vPlane;

        half4 main(float2 p) {
            float y = yPlane.eval(p).r;
            float u = uPlane.eval(p * 0.5).r - 0.5;
            float v = vPlane.eval(p * 0.5).r - 0.5;
            float3 yuv = float3(y, u, v);
            float3 rgb = float3(
                dot(rowR, yuv),
                dot(rowG, yuv),
                dot(rowB, yuv)) + offset;
            return half4(clamp(rgb, 0.0, 1.0), 1.0);
        }
        """;

    private static readonly SKRuntimeEffect? RuntimeEffect = _CreateEffect(out _);

    private readonly string? _colorRange;
    private readonly string? _colorSpace;
    private readonly byte[] _frame;
    private readonly Lock _frameLock = new();
    private readonly int _frameSize;
    private GRContext? _blurContext;
    private SKSurface? _blurSurface;
    private int _blurSurfaceHeight;
    private int _blurSurfaceWidth;
    private bool _hasFrame;

    public YuvVideoFrameView(
        int width,
        int height,
        VideoSuitMode suitMode,
        string? colorRange,
        string? colorSpace)
        : base(width, height, suitMode)
    {
        _colorRange = colorRange;
        _colorSpace = colorSpace;
        _frameSize = FrameWidth * FrameHeight * 3 / 2;
        _frame = new byte[_frameSize];
    }

    public override FfmpegPixelFormat PixelFormat => FfmpegPixelFormat.Yuv420p;

    public override bool SupportsShaderBlur => true;

    public override void Present(byte[] frame)
    {
        if (IsDisposed || frame.Length < _frameSize) return;

        lock (_frameLock)
        {
            Buffer.BlockCopy(frame, 0, _frame, 0, _frameSize);
            _hasFrame = true;
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsDisposed || !_hasFrame) return;

        context.Custom(new DrawOperation(this, new Rect(Bounds.Size)));
    }

    protected override void DisposeCore()
    {
        _blurSurface?.Dispose();
        _blurSurface = null;
    }

    private static SKRuntimeEffect? _CreateEffect(out string? error)
    {
        try
        {
            var effect = SKRuntimeEffect.CreateShader(ShaderSource, out error);
            return effect;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return null;
        }
    }

    private void Draw(ISkiaSharpApiLease lease, Rect bounds)
    {
        if (RuntimeEffect is null)
        {
            NotifyRendererUnavailable();
            return;
        }

        var ySize = FrameWidth * FrameHeight;
        var uvWidth = FrameWidth / 2;
        var uvHeight = FrameHeight / 2;
        var uvSize = uvWidth * uvHeight;

        SKImage? yImage = null;
        SKImage? uImage = null;
        SKImage? vImage = null;
        try
        {
            lock (_frameLock)
            {
                if (!_hasFrame) return;
                yImage = SKImage.FromPixelCopy(
                    new SKImageInfo(FrameWidth, FrameHeight, SKColorType.Gray8, SKAlphaType.Opaque),
                    new ReadOnlySpan<byte>(_frame, 0, ySize));
                uImage = SKImage.FromPixelCopy(
                    new SKImageInfo(uvWidth, uvHeight, SKColorType.Gray8, SKAlphaType.Opaque),
                    new ReadOnlySpan<byte>(_frame, ySize, uvSize));
                vImage = SKImage.FromPixelCopy(
                    new SKImageInfo(uvWidth, uvHeight, SKColorType.Gray8, SKAlphaType.Opaque),
                    new ReadOnlySpan<byte>(_frame, ySize + uvSize, uvSize));
            }

            var sampling = new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None);
            using var yShader = SKShader.CreateImage(
                yImage!,
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                sampling);
            using var uShader = SKShader.CreateImage(
                uImage!,
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                sampling);
            using var vShader = SKShader.CreateImage(
                vImage!,
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                sampling);

            using var uniforms = new SKRuntimeEffectUniforms(RuntimeEffect);
            var color = YuvColorConversion.Create(_colorRange, _colorSpace);
            uniforms["rowR"] = color.RowR;
            uniforms["rowG"] = color.RowG;
            uniforms["rowB"] = color.RowB;
            uniforms["offset"] = color.Offset;

            using var children = new SKRuntimeEffectChildren(RuntimeEffect);
            children["yPlane"] = yShader;
            children["uPlane"] = uShader;
            children["vPlane"] = vShader;

            using var shader = RuntimeEffect.ToShader(uniforms, children);
            using var paint = new SKPaint();
            paint.Shader = shader;
            paint.IsAntialias = false;

            var operations = GetDrawOperations(bounds);
            if (operations.Count == 0) return;

            if (BlurRadius <= 0d)
            {
                foreach (var operation in operations)
                    _DrawShader(lease.SkCanvas, paint, operation.Destination);
                return;
            }

            var surface = _GetBlurSurface(lease.GrContext);
            if (surface is null)
            {
                foreach (var operation in operations)
                    _DrawShader(lease.SkCanvas, paint, operation.Destination);
                return;
            }

            var scaleX = _blurSurfaceWidth / (float)FrameWidth;
            var scaleY = _blurSurfaceHeight / (float)FrameHeight;
            surface.Canvas.Clear(SKColors.Transparent);
            surface.Canvas.Save();
            surface.Canvas.Scale(scaleX, scaleY);
            surface.Canvas.DrawRect(0f, 0f, FrameWidth, FrameHeight, paint);
            surface.Canvas.Restore();

            using var snapshot = surface.Snapshot();
            var sigma = (float)(0.288675d * (BlurRadius + 1d) * 0.5d);
            using var filter = SKImageFilter.CreateBlur(sigma, sigma);
            using var blurPaint = new SKPaint();
            blurPaint.ImageFilter = filter;
            blurPaint.IsAntialias = false;
            foreach (var (_, destination) in operations)
            {
                if (destination.Width <= 0d || destination.Height <= 0d)
                    continue;

                var overscan = destination.Inflate(sigma);
                lease.SkCanvas.Save();
                lease.SkCanvas.ClipRect(destination.ToSKRect());
                lease.SkCanvas.DrawImage(
                    snapshot,
                    overscan.ToSKRect(),
                    blurPaint);
                lease.SkCanvas.Restore();
            }
        }
        catch (Exception)
        {
            NotifyRendererUnavailable();
        }
        finally
        {
            yImage?.Dispose();
            uImage?.Dispose();
            vImage?.Dispose();
        }
    }

    private void _DrawShader(
        SKCanvas canvas,
        SKPaint paint,
        Rect destination)
    {
        if (destination.Width <= 0d || destination.Height <= 0d) return;

        canvas.Save();
        canvas.ClipRect(destination.ToSKRect());
        canvas.Translate((float)destination.X, (float)destination.Y);
        canvas.Scale(
            (float)(destination.Width / FrameWidth),
            (float)(destination.Height / FrameHeight));
        canvas.DrawRect(0f, 0f, FrameWidth, FrameHeight, paint);
        canvas.Restore();
    }

    private SKSurface? _GetBlurSurface(GRContext? context)
    {
        var width = Math.Max(2, FrameWidth / 2);
        var height = Math.Max(2, FrameHeight / 2);
        if (_blurSurface is not null
            && _blurSurfaceWidth == width
            && _blurSurfaceHeight == height
            && ReferenceEquals(_blurContext, context))
            return _blurSurface;

        _blurSurface?.Dispose();
        _blurSurface = null;
        _blurContext = context;
        _blurSurfaceWidth = width;
        _blurSurfaceHeight = height;

        var info = new SKImageInfo(
            width,
            height,
            SKColorType.Rgba8888,
            SKAlphaType.Premul);
        try
        {
            _blurSurface = context is null
                ? SKSurface.Create(info)
                : SKSurface.Create(context, false, info);
        }
        catch
        {
            _blurSurface = null;
        }

        return _blurSurface;
    }

    private sealed class DrawOperation(
        YuvVideoFrameView owner,
        Rect bounds)
        : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;

        public bool HitTest(Point p)
        {
            return false;
        }

        public bool Equals(ICustomDrawOperation? other)
        {
            return false;
        }

        public void Render(ImmediateDrawingContext context)
        {
            var feature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (feature is null)
            {
                owner.NotifyRendererUnavailable();
                return;
            }

            using var lease = feature.Lease();
            owner.Draw(lease, Bounds);
        }

        public void Dispose()
        {
        }
    }
}

/// <summary>YUV420p 到 RGB 的系数。</summary>
internal readonly record struct YuvColorConversion(
    float[] RowR,
    float[] RowG,
    float[] RowB,
    float[] Offset)
{
    private static readonly YuvColorConversion _bt709Limited = new(
        [1.16438356f, 0f, 1.79274107f],
        [1.16438356f, -0.21324861f, -0.53290933f],
        [1.16438356f, 2.11240179f, 0f],
        [-0.07305936f, -0.07305936f, -0.07305936f]);

    private static readonly YuvColorConversion _bt601Limited = new(
        [1.16438356f, 0f, 1.59602678f],
        [1.16438356f, -0.39176229f, -0.81296764f],
        [1.16438356f, 2.01723214f, 0f],
        [-0.07305936f, -0.07305936f, -0.07305936f]);

    private static readonly YuvColorConversion _bt2020Limited = new(
        [1.16438356f, 0f, 1.67867411f],
        [1.16438356f, -0.18732610f, -0.65042432f],
        [1.16438356f, 2.14177232f, 0f],
        [-0.07305936f, -0.07305936f, -0.07305936f]);

    private static readonly YuvColorConversion _bt709Full = new(
        [1f, 0f, 1.5748f],
        [1f, -0.18732427f, -0.46812427f],
        [1f, 1.8556f, 0f],
        [0f, 0f, 0f]);

    private static readonly YuvColorConversion _bt601Full = new(
        [1f, 0f, 1.402f],
        [1f, -0.34413629f, -0.71413629f],
        [1f, 1.772f, 0f],
        [0f, 0f, 0f]);

    private static readonly YuvColorConversion _bt2020Full = new(
        [1f, 0f, 1.4746f],
        [1f, -0.16455313f, -0.57135313f],
        [1f, 1.8814f, 0f],
        [0f, 0f, 0f]);

    public static YuvColorConversion Create(string? range, string? space)
    {
        var limited = !string.Equals(range, "pc", StringComparison.OrdinalIgnoreCase);
        var normalizedSpace = space?.ToLowerInvariant();
        var isBt601 = normalizedSpace is "bt601" or "smpte170m" or "bt470bg";
        var isBt2020 = normalizedSpace is "bt2020";

        if (limited)
        {
            if (isBt601) return _bt601Limited;
            if (isBt2020) return _bt2020Limited;
            return _bt709Limited;
        }

        if (isBt601) return _bt601Full;
        if (isBt2020) return _bt2020Full;
        return _bt709Full;
    }
}