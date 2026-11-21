using System.Numerics;
using System.Reflection;

namespace PCL.UI.Composition;

internal sealed class NativeMaterialSprite : IDisposable
{
    private readonly WindowCompositionContext _owner;
    private readonly CompositionInterop _abi;
    private readonly object _raw;
    private readonly object _proxy;
    private readonly Action<object, Matrix4x4> _setTransform;
    private readonly Action<object, Vector2> _setSize;
    private readonly Action<object, float> _setOpacity;
    private readonly Action<object, int> _setVisible;
    private object? _brush;
    private object? _clip;
    private Matrix4x4 _lastTransform;
    private Vector2 _lastSize;
    private float _lastOpacity = -1;
    private int _lastVisible = -1;
    private bool _disposed;

    internal NativeMaterialSprite(WindowCompositionContext owner, object raw, object proxy)
    {
        _owner = owner;
        _abi = owner.Abi;
        _raw = raw;
        _proxy = proxy;
        var visualType = _abi.VisualType;
        _setTransform = Reflector.Call<Matrix4x4>(CompositionInterop.Method(visualType, "SetTransformMatrix", 1));
        _setSize = Reflector.Call<Vector2>(CompositionInterop.Method(visualType, "SetSize", 1));
        _setOpacity = Reflector.Call<float>(CompositionInterop.Method(visualType, "SetOpacity", 1));
        _setVisible = Reflector.Call<int>(CompositionInterop.Method(visualType, "SetIsVisible", 1));
    }

    public bool Apply(MaterialBase? material)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (_owner.SyncRoot)
        {
            CompiledMaterial? compiled = null;
            try
            {
                compiled = MaterialBrushCompiler.Compile(_owner, material);
                if (compiled is null)
                    return false;
                var spriteType = _abi.Win32Type("Avalonia.Win32.WinRT.ISpriteVisual");
                CompositionInterop.Method(spriteType, "SetBrush", 1).Invoke(_raw, [compiled.Brush]);
                WindowCompositionContext.Release(_brush);
                _brush = compiled.Brush;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    public void UpdateGeometry(Vector2 size, float radius)
    {
        if (_disposed)
            return;
        lock (_owner.SyncRoot)
        {
            if (_lastSize != size)
            {
                _setSize(_proxy, size);
                _lastSize = size;
            }

            if (_clip is null)
            {
                try
                {
                    var visualArray = Array.CreateInstance(_abi.VisualType, 1);
                    visualArray.SetValue(_proxy, 0);
                    _clip = _abi.UtilsType.GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                        .FirstOrDefault(method => method.Name == "ClipVisual" && method.GetParameters().Length == 3)
                        ?.Invoke(null, [_owner.Compositor, (float?)radius, visualArray]);
                }
                catch
                {
                    _clip = null;
                }
            }

            if (_clip is not null)
            {
                var geometryType = _abi.Win32Type("Avalonia.Win32.WinRT.ICompositionRoundedRectangleGeometry");
                CompositionInterop.Method(geometryType, "SetSize", 1).Invoke(_clip, [size]);
                CompositionInterop.Method(geometryType, "SetCornerRadius", 1).Invoke(_clip, [new Vector2(radius)]);
            }
        }
    }

    public bool UpdateFrame(Matrix4x4 transform, float opacity, bool visible)
    {
        if (_disposed)
            return false;
        var changed = false;
        lock (_owner.SyncRoot)
        {
            if (!NearlyEqual(_lastTransform, transform))
            {
                _setTransform(_proxy, transform);
                _lastTransform = transform;
                changed = true;
            }
            if (Math.Abs(_lastOpacity - opacity) > 0.0001f)
            {
                _setOpacity(_proxy, opacity);
                _lastOpacity = opacity;
                changed = true;
            }
            var intVisible = visible ? 1 : 0;
            if (_lastVisible != intVisible)
            {
                _setVisible(_proxy, intVisible);
                _lastVisible = intVisible;
                changed = true;
            }
        }
        return changed;
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _owner.Remove(this, _proxy);
        DisposeByOwner();
    }

    internal void DisposeByOwner()
    {
        if (_disposed)
            return;
        _disposed = true;
        WindowCompositionContext.Release(_clip);
        WindowCompositionContext.Release(_brush);
        WindowCompositionContext.Release(_proxy);
        WindowCompositionContext.Release(_raw);
    }

    private static bool NearlyEqual(in Matrix4x4 left, in Matrix4x4 right)
    {
        const float epsilon = 0.0001f;
        return Math.Abs(left.M11 - right.M11) < epsilon && Math.Abs(left.M12 - right.M12) < epsilon &&
               Math.Abs(left.M21 - right.M21) < epsilon && Math.Abs(left.M22 - right.M22) < epsilon &&
               Math.Abs(left.M41 - right.M41) < epsilon && Math.Abs(left.M42 - right.M42) < epsilon;
    }
}
