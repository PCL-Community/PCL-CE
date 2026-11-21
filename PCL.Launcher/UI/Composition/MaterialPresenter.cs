using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace PCL.UI.Composition;

public class MaterialPresenter : Decorator
{
    public static readonly StyledProperty<MaterialBase?> MaterialProperty =
        AvaloniaProperty.Register<MaterialPresenter, MaterialBase?>(nameof(Material));

    public static readonly StyledProperty<IBrush?> FallbackBrushProperty =
        AvaloniaProperty.Register<MaterialPresenter, IBrush?>(nameof(FallbackBrush));

    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty =
        Border.CornerRadiusProperty.AddOwner<MaterialPresenter>();

    private static readonly DirectProperty<MaterialPresenter, MaterialRenderMode> ActualModePropertyKey =
        AvaloniaProperty.RegisterDirect<MaterialPresenter, MaterialRenderMode>(
            nameof(ActualMode), presenter => presenter._actualMode);

    private static readonly DirectProperty<MaterialPresenter, bool> IsNativeMaterialActivePropertyKey =
        AvaloniaProperty.RegisterDirect<MaterialPresenter, bool>(
            nameof(IsNativeMaterialActive), presenter => presenter._isNativeActive);

    public static readonly DirectProperty<MaterialPresenter, MaterialRenderMode> ActualModeProperty =
        ActualModePropertyKey;

    public static readonly DirectProperty<MaterialPresenter, bool> IsNativeMaterialActiveProperty =
        IsNativeMaterialActivePropertyKey;

    private IMaterialAttachment? _attachment;
    private MaterialWatcher? _watcher;
    private MaterialRenderMode _actualMode;
    private bool _isNativeActive;
    private RectangleGeometry? _roundedClip;
    private int _attachAttempt;
    private bool _attachFramePending;
    private DispatcherTimer? _attachTimer;

    static MaterialPresenter()
    {
        AffectsRender<MaterialPresenter>(MaterialProperty, FallbackBrushProperty, CornerRadiusProperty);
    }

    public MaterialPresenter()
    {
        ClipToBounds = true;
    }

    public MaterialBase? Material
    {
        get => GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    public IBrush? FallbackBrush
    {
        get => GetValue(FallbackBrushProperty);
        set => SetValue(FallbackBrushProperty, value);
    }

    public CornerRadius CornerRadius
    {
        get => GetValue(CornerRadiusProperty);
        set => SetValue(CornerRadiusProperty, value);
    }

    public MaterialRenderMode ActualMode => _actualMode;
    public bool IsNativeMaterialActive => _isNativeActive;

    public event EventHandler? NativeMaterialActiveChanged;

    public override void Render(DrawingContext context)
    {
        var rect = new Rect(Bounds.Size);
        if (!_isNativeActive && FallbackBrush is { } fallback)
            context.DrawRectangle(fallback, null, new RoundedRect(rect, CornerRadius));

        if (_isNativeActive)
            MaterialTintRenderer.Render(context, rect, CornerRadius, Material);

        base.Render(context);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        Watch(Material);
        UpdateRoundedClip();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _attachAttempt = 0;
        TryAttach();
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        _attachFramePending = false;
        _attachTimer?.Stop();
        _attachment?.Dispose();
        _attachment = null;
        SetRenderMode(MaterialRenderMode.Fallback);
        base.OnUnloaded(e);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _attachment?.Dispose();
        _attachment = null;
        _attachFramePending = false;
        _attachTimer?.Stop();
        Watch(null);
        SetRenderMode(MaterialRenderMode.Fallback);
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == MaterialProperty)
        {
            Watch(change.GetNewValue<MaterialBase?>());
            _attachment?.MaterialChanged();
        }
        else if (change.Property == BoundsProperty || change.Property == CornerRadiusProperty)
        {
            UpdateRoundedClip();
            _attachment?.GeometryChanged();
        }
        else if (change.Property == IsVisibleProperty || change.Property == OpacityProperty)
        {
            _attachment?.VisualStateChanged();
        }
        else if (change.Property == ClipToBoundsProperty)
        {
            UpdateRoundedClip();
            _attachment?.VisualStateChanged();
        }
    }

    internal void SetRenderMode(MaterialRenderMode mode)
    {
        if (_actualMode != mode)
            SetAndRaise(ActualModePropertyKey, ref _actualMode, mode);

        var active = mode == MaterialRenderMode.Native;
        if (_isNativeActive != active)
        {
            SetAndRaise(IsNativeMaterialActivePropertyKey, ref _isNativeActive, active);
            InvalidateVisual();
            NativeMaterialActiveChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Watch(MaterialBase? material)
    {
        _watcher?.Dispose();
        _watcher = material is null ? null : new MaterialWatcher(material, OnMaterialPropertyChanged);
    }

    private void OnMaterialPropertyChanged(AvaloniaObject? source, AvaloniaProperty? property)
    {
        if (source is MicaMaterial && (property == MicaMaterial.ModeProperty || property == MicaMaterial.VariantProperty))
            _attachment?.MaterialChanged();
        else
            InvalidateVisual();
    }

    private void UpdateRoundedClip()
    {
        if (!ClipToBounds)
        {
            if (ReferenceEquals(Clip, _roundedClip))
                Clip = null;
            return;
        }

        var maxRadius = Math.Max(Math.Max(CornerRadius.TopLeft, CornerRadius.TopRight),
            Math.Max(CornerRadius.BottomRight, CornerRadius.BottomLeft));
        _roundedClip ??= new RectangleGeometry();
        _roundedClip.Rect = new Rect(Bounds.Size);
        _roundedClip.RadiusX = maxRadius;
        _roundedClip.RadiusY = maxRadius;
        if (!ReferenceEquals(Clip, _roundedClip))
            Clip = _roundedClip;
    }

    private void TryAttach()
    {
        if (!IsLoaded || _attachment is not null)
            return;

        _attachment = TopLevelMaterialScene.TryAttach(this);
        if (_attachment is not null)
        {
            _attachTimer?.Stop();
            return;
        }

        // 前 12 次逐帧重试以覆盖合成后端预热；此后改用低频定时器持续重试，
        // 只要控件保持加载状态就不断尝试，直到附加成功或卸载。
        if (++_attachAttempt >= 12)
        {
            if (_attachTimer is null)
                _attachTimer = new DispatcherTimer(TimeSpan.FromSeconds(2), DispatcherPriority.Background,
                    (_, _) => TryAttach());
            _attachTimer.Start();
            return;
        }

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null || _attachFramePending)
            return;
        _attachFramePending = true;
        topLevel.RequestAnimationFrame(_ =>
        {
            _attachFramePending = false;
            TryAttach();
        });
    }

    private sealed class MaterialWatcher : IDisposable
    {
        private readonly MaterialBase _material;
        private readonly Action<AvaloniaObject?, AvaloniaProperty?> _changed;
        private bool _disposed;

        public MaterialWatcher(MaterialBase material, Action<AvaloniaObject?, AvaloniaProperty?> changed)
        {
            _material = material;
            _changed = changed;
            material.PropertyChanged += OnChanged;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            _material.PropertyChanged -= OnChanged;
        }

        private void OnChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
            => _changed(sender as AvaloniaObject, e.Property);
    }
}
