using System.Reflection;
using Avalonia.Controls;
using Avalonia.Styling;

namespace PCL.UI.Composition;

internal sealed class WindowCompositionContext : IDisposable
{
    private readonly CompositionInterop _abi;
    private readonly TopLevel _topLevel;
    private readonly object _root;
    private readonly object _rootBox;
    private readonly object _rootList;
    private readonly object _layerBox;
    private readonly object _layer;
    private readonly object _layerList;
    private readonly List<NativeMaterialSprite> _sprites = [];
    private readonly IDisposable? _backdropLease;
    private bool _disposed;

    public object Compositor { get; }
    public object SyncRoot { get; }
    public CompositionInterop Abi => _abi;
    public bool IsDarkTheme => _topLevel.ActualThemeVariant == ThemeVariant.Dark;

    private WindowCompositionContext(TopLevel topLevel, CompositionInterop abi, object compositor, object syncRoot,
        object root, object rootBox, object rootList, object layerBox, object layer, object layerList)
    {
        _topLevel = topLevel;
        _backdropLease = HostBackdropLease.Acquire(topLevel);
        _abi = abi;
        Compositor = compositor;
        SyncRoot = syncRoot;
        _root = root;
        _rootBox = rootBox;
        _rootList = rootList;
        _layerBox = layerBox;
        _layer = layer;
        _layerList = layerList;
    }

    public static WindowCompositionContext? TryCreate(TopLevel topLevel, CompositionInterop abi)
    {
        try
        {
            var platform = topLevel.PlatformImpl;
            if (platform is null)
                return null;

            var surface = Field(platform.GetType(), "_glSurface").GetValue(platform);
            if (surface is null || !abi.SurfaceType.IsInstanceOfType(surface))
                return null;

            var window = CompositionInterop.Field(abi.SurfaceType, "_window").GetValue(surface)
                         ?? throw new InvalidOperationException("WinUI composited window is missing.");
            var target = CompositionInterop.Field(abi.WindowType, "_target").GetValue(window)
                         ?? throw new InvalidOperationException("WinUI composition target is missing.");
            var shared = CompositionInterop.Field(abi.WindowType, "_shared").GetValue(window)
                         ?? throw new InvalidOperationException("WinUI shared compositor is missing.");
            var swapchain = CompositionInterop.Field(abi.WindowType, "_visual").GetValue(window)
                            ?? throw new InvalidOperationException("Avalonia swapchain visual is missing.");

            var sharedType = abi.Win32Type("Avalonia.Win32.WinRT.Composition.WinUiCompositionShared");
            var compositor = CompositionInterop.Property(sharedType, "Compositor").GetValue(shared)
                             ?? throw new InvalidOperationException("WinUI compositor is missing.");
            var syncRoot = CompositionInterop.Property(sharedType, "SyncRoot").GetValue(shared)
                           ?? throw new InvalidOperationException("WinUI compositor lock is missing.");

            var targetType = abi.Win32Type("Avalonia.Win32.WinRT.ICompositionTarget");
            var root = CompositionInterop.Property(targetType, "Root").GetValue(target)
                       ?? throw new InvalidOperationException("WinUI root visual is missing.");
            var rootBox = abi.QI(root, abi.ContainerVisualType);
            var rootList = CompositionInterop.Property(abi.ContainerVisualType, "Children").GetValue(rootBox)
                           ?? throw new InvalidOperationException("WinUI root children are missing.");

            var compositorType = abi.Win32Type("Avalonia.Win32.WinRT.ICompositor");
            var layerBox = CompositionInterop.Method(compositorType, "CreateContainerVisual", 0).Invoke(compositor, null)
                           ?? throw new InvalidOperationException("Unable to create the material layer container.");
            var layer = abi.QI(layerBox, abi.VisualType);
            var layerList = CompositionInterop.Property(abi.ContainerVisualType, "Children").GetValue(layerBox)
                            ?? throw new InvalidOperationException("Material layer children are missing.");

            lock (syncRoot)
            {
                CompositionInterop
                    .Method(rootList.GetType().GetInterfaces().FirstOrDefault(type => type.Name == "IVisualCollection")
                            ?? abi.Win32Type("Avalonia.Win32.WinRT.IVisualCollection"), "InsertBelow", 2)
                    .Invoke(rootList, [layer, swapchain]);
            }

            return new WindowCompositionContext(topLevel, abi, compositor, syncRoot, root, rootBox, rootList,
                layerBox, layer, layerList);
        }
        catch
        {
            return null;
        }
    }

    public NativeMaterialSprite CreateSprite()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        lock (SyncRoot)
        {
            var compositorType = _abi.Win32Type("Avalonia.Win32.WinRT.ICompositor");
            var raw = CompositionInterop.Method(compositorType, "CreateSpriteVisual", 0).Invoke(Compositor, null)
                      ?? throw new InvalidOperationException("Unable to create the material sprite visual.");
            var proxy = _abi.QI(raw, _abi.VisualType);
            var collectionType = _abi.Win32Type("Avalonia.Win32.WinRT.IVisualCollection");
            CompositionInterop.Method(collectionType, "InsertAtTop", 1).Invoke(_layerList, [proxy]);
            var sprite = new NativeMaterialSprite(this, raw, proxy);
            _sprites.Add(sprite);
            return sprite;
        }
    }

    internal void Remove(NativeMaterialSprite sprite, object proxy)
    {
        if (_disposed)
            return;
        lock (SyncRoot)
        {
            _sprites.Remove(sprite);
            try
            {
                var collectionType = _abi.Win32Type("Avalonia.Win32.WinRT.IVisualCollection");
                CompositionInterop.Method(collectionType, "Remove", 1).Invoke(_layerList, [proxy]);
            }
            catch
            {
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        foreach (var sprite in _sprites.ToArray())
            sprite.DisposeByOwner();
        _sprites.Clear();

        lock (SyncRoot)
        {
            try
            {
                var collectionType = _abi.Win32Type("Avalonia.Win32.WinRT.IVisualCollection");
                CompositionInterop.Method(collectionType, "Remove", 1).Invoke(_rootList, [_layer]);
            }
            catch
            {
            }
        }

        Release(_layerList);
        Release(_layer);
        Release(_layerBox);
        Release(_rootList);
        Release(_rootBox);
        Release(_root);
        _backdropLease?.Dispose();
    }

    internal static void Release(object? value)
    {
        if (value is IDisposable disposable)
            disposable.Dispose();
    }

    private static FieldInfo Field(Type type, string name)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            var field = current.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field is not null)
                return field;
        }
        throw new MissingFieldException(type.FullName, name);
    }
}
