using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Rendering.Composition;
using MicroCom.Runtime;

namespace PCL.UI.Composition;

internal sealed class CompositionInterop
{
    private const string RequiredVersion = "12.1.6.0";
    private readonly Assembly _win32;
    private readonly Assembly _base;
    private readonly MethodInfo _queryInterface;

    public Type VisualType { get; }
    public Type ContainerVisualType { get; }
    public Type BrushType { get; }
    public Type SurfaceType { get; }
    public Type WindowType { get; }
    public Type UtilsType { get; }

    public Func<object, object?> ServerVisualOf { get; }
    public Func<object, Matrix?> OwnTransformOf { get; }
    public Func<object, object?> ParentOf { get; }
    public Func<object, float> OpacityOf { get; }
    public Func<object, bool> VisibleOf { get; }
    public Func<object, object> CompositorOf { get; }
    public Func<object, object> AnimationsOf { get; }
    public Func<object, bool> NeedNextTickOf { get; }
    public Action<object, Action, bool> PostJob { get; }

    private CompositionInterop(Assembly win32, Assembly @base)
    {
        _win32 = win32;
        _base = @base;
        _queryInterface = typeof(MicroComRuntime).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == nameof(MicroComRuntime.QueryInterface) && method.IsGenericMethodDefinition);

        VisualType = Win32Type("Avalonia.Win32.WinRT.IVisual");
        ContainerVisualType = Win32Type("Avalonia.Win32.WinRT.IContainerVisual");
        BrushType = Win32Type("Avalonia.Win32.WinRT.ICompositionBrush");
        SurfaceType = Win32Type("Avalonia.Win32.WinRT.Composition.WinUiCompositedWindowSurface");
        WindowType = Win32Type("Avalonia.Win32.WinRT.Composition.WinUiCompositedWindow");
        UtilsType = Win32Type("Avalonia.Win32.WinRT.Composition.WinUiCompositionUtils");

        var compositionVisual = typeof(CompositionVisual);
        var serverVisual = BaseType("Avalonia.Rendering.Composition.Server.ServerCompositionVisual");
        var simpleServerObject = BaseType("Avalonia.Rendering.Composition.Server.SimpleServerObject");
        var serverCompositor = BaseType("Avalonia.Rendering.Composition.Server.ServerCompositor");
        var serverAnimations = BaseType("Avalonia.Rendering.Composition.Server.ServerCompositorAnimations");

        ServerVisualOf = Reflector.Accessor<object?>(compositionVisual.GetProperties(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(property => property.Name == "Server" && property.PropertyType == serverVisual));
        OwnTransformOf = Reflector.Accessor<Matrix?>(Property(serverVisual, "OwnTransform"));
        ParentOf = Reflector.Accessor<object?>(Property(serverVisual, "Parent"));
        OpacityOf = Reflector.Accessor<float>(Property(serverVisual, "Opacity"));
        VisibleOf = Reflector.Accessor<bool>(Property(serverVisual, "Visible"));
        CompositorOf = Reflector.Accessor<object>(Property(simpleServerObject, "Compositor"));
        AnimationsOf = Reflector.Accessor<object>(Property(serverCompositor, "Animations"));
        NeedNextTickOf = Reflector.Accessor<bool>(Property(serverAnimations, "NeedNextTick"));
        PostJob = Reflector.Job(typeof(Compositor).GetMethod("PostServerJob",
            BindingFlags.Instance | BindingFlags.NonPublic, [typeof(Action), typeof(bool)])
            ?? throw new MissingMethodException(typeof(Compositor).FullName, "PostServerJob"));
    }

    public static CompositionInterop? TryResolve()
    {
        try
        {
            var win32 = typeof(global::Avalonia.Win32PlatformOptions).Assembly;
            var @base = typeof(CompositionVisual).Assembly;
            if (win32.GetName().Version?.ToString() != RequiredVersion || @base.GetName().Version?.ToString() != RequiredVersion)
                return null;
            return new CompositionInterop(win32, @base);
        }
        catch
        {
            return null;
        }
    }

    public object QI(object value, Type interfaceType)
    {
        if (value is not IUnknown unknown)
            throw new InvalidOperationException($"{value.GetType()} is not a MicroCom proxy.");
        return _queryInterface.MakeGenericMethod(interfaceType).Invoke(null, [unknown])
               ?? throw new InvalidOperationException($"QueryInterface({interfaceType.Name}) returned null.");
    }

    public Type Win32Type(string name) => _win32.GetType(name, true)!;
    public Type BaseType(string name) => _base.GetType(name, true)!;

    public static PropertyInfo Property(Type type, string name) =>
        type.GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingMemberException(type.FullName, name);

    public static FieldInfo Field(Type type, string name) =>
        type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(type.FullName, name);

    public static MethodInfo Method(Type type, string name, int parameterCount)
        => type.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
               .Single(method => method.Name == name && method.GetParameters().Length == parameterCount);
}
