using System.Runtime.InteropServices;
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

    private static readonly SKSamplingOptions Sampling = new(
        SKFilterMode.Linear,
        SKMipmapMode.None);

    private readonly SKPaint? _blurPaint;
    private readonly SKRuntimeEffectChildren? _children;
    private readonly string? _colorRange;
    private readonly string? _colorSpace;
    private readonly Lock _frameLock = new();
    private readonly int _frameSize;
    private readonly SKPaint? _paint;
    private readonly SKRuntimeEffectUniforms? _uniforms;

    private GRContext? _blurContext;
    private SKImageFilter? _blurFilter;
    private float _blurSigma;
    private SKSurface? _blurSurface;
    private int _blurSurfaceHeight;
    private int _blurSurfaceWidth;
    private VideoFrameBuffer? _frame;
    private GCHandle _frameHandle;
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

        if (RuntimeEffect is null) return;

        _uniforms = new SKRuntimeEffectUniforms(RuntimeEffect);
        var color = YuvColorConversion.Create(_colorRange, _colorSpace);
        _uniforms["rowR"] = color.RowR;
        _uniforms["rowG"] = color.RowG;
        _uniforms["rowB"] = color.RowB;
        _uniforms["offset"] = color.Offset;

        _children = new SKRuntimeEffectChildren(RuntimeEffect);
        _paint = new SKPaint
        {
            IsAntialias = false
        };
        _blurPaint = new SKPaint
        {
            IsAntialias = false
        };
    }

    public override FfmpegPixelFormat PixelFormat => FfmpegPixelFormat.Yuv420p;

    public override bool SupportsShaderBlur => true;

    public override void Present(VideoFrameBuffer frame)
    {
        if (frame.Length < _frameSize)
        {
            frame.Dispose();
            return;
        }

        lock (_frameLock)
        {
            if (IsDisposed)
            {
                frame.Dispose();
                return;
            }

            ReleaseFrameLocked();
            _frame = frame;
            try
            {
                _frameHandle = GCHandle.Alloc(frame.Data, GCHandleType.Pinned);
            }
            catch
            {
                _frame = null;
                frame.Dispose();
                throw;
            }

            _hasFrame = true;
        }

        try
        {
            InvalidateVisual();
        }
        catch
        {
            lock (_frameLock)
            {
                ReleaseFrameLocked();
            }

            throw;
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (IsDisposed) return;

        lock (_frameLock)
        {
            if (!_hasFrame) return;
        }

        context.Custom(new DrawOperation(this, new Rect(Bounds.Size)));
    }

    protected override void DisposeCore()
    {
        lock (_frameLock)
        {
            ReleaseFrameLocked();

            _blurSurface?.Dispose();
            _blurSurface = null;
            _blurFilter?.Dispose();
            _blurFilter = null;
            _blurPaint?.Dispose();
            _paint?.Dispose();
            _children?.Dispose();
            _uniforms?.Dispose();
        }
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
        if (IsDisposed) return;
        var uniforms = _uniforms;
        var children = _children;
        var paint = _paint;
        var blurPaint = _blurPaint;
        if (RuntimeEffect is null
            || uniforms is null
            || children is null
            || paint is null
            || blurPaint is null)
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
                try
                {
                    if (IsDisposed
                        || !_hasFrame
                        || _frame is null
                        || !_frameHandle.IsAllocated)
                        return;

                    var baseAddress = _frameHandle.AddrOfPinnedObject();
                    yImage = SKImage.FromPixels(
                        new SKImageInfo(
                            FrameWidth,
                            FrameHeight,
                            SKColorType.Gray8,
                            SKAlphaType.Opaque),
                        baseAddress);
                    uImage = SKImage.FromPixels(
                        new SKImageInfo(
                            uvWidth,
                            uvHeight,
                            SKColorType.Gray8,
                            SKAlphaType.Opaque),
                        IntPtr.Add(baseAddress, ySize));
                    vImage = SKImage.FromPixels(
                        new SKImageInfo(
                            uvWidth,
                            uvHeight,
                            SKColorType.Gray8,
                            SKAlphaType.Opaque),
                        IntPtr.Add(baseAddress, ySize + uvSize));

                    using var yShader = SKShader.CreateImage(
                        yImage,
                        SKShaderTileMode.Clamp,
                        SKShaderTileMode.Clamp,
                        Sampling);
                    using var uShader = SKShader.CreateImage(
                        uImage,
                        SKShaderTileMode.Clamp,
                        SKShaderTileMode.Clamp,
                        Sampling);
                    using var vShader = SKShader.CreateImage(
                        vImage,
                        SKShaderTileMode.Clamp,
                        SKShaderTileMode.Clamp,
                        Sampling);

                    children["yPlane"] = yShader;
                    children["uPlane"] = uShader;
                    children["vPlane"] = vShader;

                    using var shader = RuntimeEffect.ToShader(uniforms, children);
                    paint.Shader = shader;

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
                    if (_blurFilter is null
                        || Math.Abs(_blurSigma - sigma) > 0.01f)
                    {
                        _blurFilter?.Dispose();
                        _blurFilter = SKImageFilter.CreateBlur(sigma, sigma);
                        _blurSigma = sigma;
                        blurPaint.ImageFilter = _blurFilter;
                    }

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
                finally
                {
                    yImage?.Dispose();
                    uImage?.Dispose();
                    vImage?.Dispose();
                    paint.Shader = null;
                    children["yPlane"] = null;
                    children["uPlane"] = null;
                    children["vPlane"] = null;
                }
            }
        }
        catch (Exception)
        {
            NotifyRendererUnavailable();
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

    private void ReleaseFrameLocked()
    {
        if (_frameHandle.IsAllocated) _frameHandle.Free();
        _frame?.Dispose();
        _frame = null;
        _hasFrame = false;
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
            if (owner.IsDisposed) return;

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
        var isBt2020 = normalizedSpace is "bt2020" or "bt2020nc";
        var isBt709 = normalizedSpace is "bt709";

        if (limited)
        {
            if (isBt601) return _bt601Limited;
            if (isBt2020) return _bt2020Limited;
            if (isBt709) return _bt709Limited;
            return _bt601Limited;
        }

        if (isBt601) return _bt601Full;
        if (isBt2020) return _bt2020Full;
        if (isBt709) return _bt709Full;
        return _bt601Full;
    }
}