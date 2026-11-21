using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;

namespace PCL.UI.Composition;

internal sealed class TopLevelMaterialScene : IDisposable
{
    private static readonly ConditionalWeakTable<TopLevel, TopLevelMaterialScene> Scenes = new();
    private static readonly CompositionInterop? Abi = CompositionInterop.TryResolve();

    private readonly TopLevel _topLevel;
    private readonly WindowCompositionContext _native;
    private readonly Compositor _compositor;
    private readonly List<Attachment> _attachments = [];
    private readonly Action _afterCommit;
    private readonly MethodInfo _removeAfterCommit;
    private bool _framePending;
    private bool _disposed;
    private volatile bool _serverNeedsFrames;
    private int _stableFrames;
    private double _renderScaling = 1;

    private TopLevelMaterialScene(TopLevel topLevel, WindowCompositionContext native, Compositor compositor)
    {
        _topLevel = topLevel;
        _native = native;
        _compositor = compositor;
        _afterCommit = OnAfterCommit;
        var afterCommit = typeof(Compositor).GetEvent("AfterCommit", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                          ?? throw new MissingMemberException(typeof(Compositor).FullName, "AfterCommit");
        afterCommit.GetAddMethod(true)!.Invoke(_compositor, [_afterCommit]);
        _removeAfterCommit = afterCommit.GetRemoveMethod(true)!;
        _topLevel.Closed += SceneClosed;
        _topLevel.ScalingChanged += ScalingChanged;
        _topLevel.ActualThemeVariantChanged += ThemeVariantChanged;
    }

    public static IMaterialAttachment? TryAttach(MaterialPresenter presenter)
    {
        if (!OperatingSystem.IsWindows())
        {
            presenter.SetRenderMode(MaterialRenderMode.Fallback);
            return null;
        }

        if (Abi is null)
        {
            presenter.SetRenderMode(MaterialRenderMode.Fallback);
            return null;
        }

        var topLevel = TopLevel.GetTopLevel(presenter);
        var elementVisual = ElementComposition.GetElementVisual(presenter);
        if (topLevel is null || elementVisual is null)
        {
            presenter.SetRenderMode(MaterialRenderMode.Fallback);
            return null;
        }

        try
        {
            var scene = Scenes.GetValue(topLevel, key =>
            {
                var native = WindowCompositionContext.TryCreate(key, Abi)
                             ?? throw new InvalidOperationException("The active renderer is not WinUIComposition.");
                return new TopLevelMaterialScene(key, native, elementVisual.Compositor);
            });
            if (scene._disposed)
                return null;
            return scene.Attach(presenter, elementVisual);
        }
        catch
        {
            presenter.SetRenderMode(MaterialRenderMode.Fallback);
            return null;
        }
    }

    private Attachment Attach(MaterialPresenter presenter, CompositionVisual compositionVisual)
    {
        var serverVisual = _native.Abi.ServerVisualOf(compositionVisual)
                           ?? throw new InvalidOperationException("Avalonia server visual is unavailable.");
        var attachment = new Attachment(this, presenter, compositionVisual, serverVisual, _native.CreateSprite());
        lock (_attachments)
            _attachments.Add(attachment);
        attachment.RefreshMaterial();
        attachment.RefreshGeometry();
        Wake();
        return attachment;
    }

    private void Detach(Attachment attachment)
    {
        lock (_attachments)
            _attachments.Remove(attachment);
        attachment.Native.Dispose();
        attachment.Presenter.SetRenderMode(MaterialRenderMode.Fallback);
    }

    private void OnAfterCommit()
    {
        if (_disposed || _framePending)
            return;
        if (Dispatcher.UIThread.CheckAccess())
            Wake();
        else
            Dispatcher.UIThread.Post(Wake, DispatcherPriority.Render);
    }

    private void Wake()
    {
        if (_disposed || _framePending || AttachmentCount == 0)
            return;
        _stableFrames = 0;
        _framePending = true;
        _topLevel.RequestAnimationFrame(AnimationFrame);
    }

    private void AnimationFrame(TimeSpan _)
    {
        if (_disposed || AttachmentCount == 0)
        {
            _framePending = false;
            return;
        }

        if (!_serverNeedsFrames && Volatile.Read(ref _stableFrames) >= 2)
        {
            _framePending = false;
            return;
        }

        Volatile.Write(ref _renderScaling, _topLevel.RenderScaling);
        _native.Abi.PostJob(_compositor, SynchronizeOnServer, false);
        _topLevel.RequestAnimationFrame(AnimationFrame);
    }

    private void SynchronizeOnServer()
    {
        var anyChanged = false;
        object? firstServerVisual = null;
        var scaling = Volatile.Read(ref _renderScaling);

        foreach (var attachment in AttachmentSnapshot())
        {
            if (attachment.IsDisposed || !attachment.IsNativeActive)
                continue;
            firstServerVisual ??= attachment.ServerVisual;
            var state = ReadServerState(attachment.ServerVisual, scaling);
            anyChanged |= attachment.Native.UpdateFrame(state.Transform, state.Opacity, state.Visible);
        }

        var needsTick = false;
        if (firstServerVisual is not null)
        {
            try
            {
                var serverCompositor = _native.Abi.CompositorOf(firstServerVisual);
                var animations = _native.Abi.AnimationsOf(serverCompositor);
                needsTick = _native.Abi.NeedNextTickOf(animations);
            }
            catch
            {
                needsTick = false;
            }
        }

        _serverNeedsFrames = needsTick;
        if (anyChanged || needsTick)
            Volatile.Write(ref _stableFrames, 0);
        else
            Interlocked.Increment(ref _stableFrames);
    }

    private ServerVisualState ReadServerState(object serverVisual, double scaling)
    {
        var matrix = Matrix.Identity;
        var opacity = 1f;
        var visible = true;
        object? current = serverVisual;
        var depth = 0;
        while (current is not null && depth++ < 512)
        {
            var own = _native.Abi.OwnTransformOf(current);
            if (own.HasValue)
                matrix *= own.Value;
            opacity *= _native.Abi.OpacityOf(current);
            visible &= _native.Abi.VisibleOf(current);
            current = _native.Abi.ParentOf(current);
        }

        return new ServerVisualState(ToPixels(matrix, scaling), Math.Clamp(opacity, 0, 1), visible);
    }

    private static Matrix4x4 ToPixels(Matrix matrix, double scaling)
    {
        var scale = (float)scaling;
        return new Matrix4x4(
            (float)matrix.M11, (float)matrix.M12, 0, (float)matrix.M13,
            (float)matrix.M21, (float)matrix.M22, 0, (float)matrix.M23,
            0, 0, 1, 0,
            (float)matrix.M31 * scale, (float)matrix.M32 * scale, 0, (float)matrix.M33);
    }

    private void ScalingChanged(object? sender, EventArgs e)
    {
        foreach (var attachment in AttachmentSnapshot())
            attachment.RefreshGeometry();
        Wake();
    }

    private void SceneClosed(object? sender, EventArgs e) => Dispose();

    private void ThemeVariantChanged(object? sender, EventArgs e)
    {
        foreach (var attachment in AttachmentSnapshot())
            attachment.RefreshMaterial();
        Wake();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _topLevel.Closed -= SceneClosed;
        _topLevel.ScalingChanged -= ScalingChanged;
        _topLevel.ActualThemeVariantChanged -= ThemeVariantChanged;
        try
        {
            _removeAfterCommit.Invoke(_compositor, [_afterCommit]);
        }
        catch
        {
        }
        foreach (var attachment in AttachmentSnapshot())
            attachment.DisposeFromScene();
        lock (_attachments)
            _attachments.Clear();
        _native.Dispose();
    }

    private int AttachmentCount
    {
        get { lock (_attachments) return _attachments.Count; }
    }

    private Attachment[] AttachmentSnapshot()
    {
        lock (_attachments)
            return _attachments.ToArray();
    }

    private readonly record struct ServerVisualState(Matrix4x4 Transform, float Opacity, bool Visible);

    private sealed class Attachment : IMaterialAttachment
    {
        private readonly TopLevelMaterialScene _scene;
        private bool _disposed;

        public MaterialPresenter Presenter { get; }
        public object ServerVisual { get; }
        public NativeMaterialSprite Native { get; }
        public bool IsDisposed => _disposed;
        public volatile bool IsNativeActive;

        public Attachment(TopLevelMaterialScene scene, MaterialPresenter presenter,
            CompositionVisual compositionVisual, object serverVisual, NativeMaterialSprite native)
        {
            _scene = scene;
            Presenter = presenter;
            ServerVisual = serverVisual;
            Native = native;
            presenter.EffectiveViewportChanged += EffectiveViewportChanged;
        }

        public void MaterialChanged()
        {
            if (_disposed)
                return;
            RefreshMaterial();
            _scene.Wake();
        }

        public void GeometryChanged()
        {
            if (_disposed)
                return;
            RefreshGeometry();
            _scene.Wake();
        }

        public void VisualStateChanged()
        {
            if (!_disposed)
                _scene.Wake();
        }

        public void RefreshMaterial()
        {
            IsNativeActive = Native.Apply(Presenter.Material);
            Presenter.SetRenderMode(IsNativeActive ? MaterialRenderMode.Native : MaterialRenderMode.Fallback);
            if (!IsNativeActive)
                Native.UpdateFrame(Matrix4x4.Identity, 0, false);
        }

        public void RefreshGeometry()
        {
            var scaling = _scene._topLevel.RenderScaling;
            var size = new Vector2((float)(Presenter.Bounds.Width * scaling), (float)(Presenter.Bounds.Height * scaling));
            var corner = Presenter.CornerRadius;
            var radius = Math.Max(Math.Max(corner.TopLeft, corner.TopRight), Math.Max(corner.BottomRight, corner.BottomLeft));
            Native.UpdateGeometry(size, (float)(radius * scaling));
        }

        private void EffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs e)
        {
            RefreshGeometry();
            _scene.Wake();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Presenter.EffectiveViewportChanged -= EffectiveViewportChanged;
            _scene.Detach(this);
        }

        public void DisposeFromScene()
        {
            if (_disposed)
                return;
            _disposed = true;
            Presenter.EffectiveViewportChanged -= EffectiveViewportChanged;
            Native.DisposeByOwner();
            Presenter.SetRenderMode(MaterialRenderMode.Fallback);
        }
    }
}
