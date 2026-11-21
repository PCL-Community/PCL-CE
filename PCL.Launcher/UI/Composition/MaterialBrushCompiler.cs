using System.Reflection;

namespace PCL.UI.Composition;

internal static class MaterialBrushCompiler
{
    public static CompiledMaterial? Compile(WindowCompositionContext context, MaterialBase? material)
    {
        try
        {
            var brush = material switch
            {
                null => null,
                MicaMaterial mica => CompileMica(context, mica),
                BlurMaterial => CompileAcrylic(context),
                _ => null,
            };
            return brush is null ? null : new CompiledMaterial(brush);
        }
        catch
        {
            return null;
        }
    }

    private static object? CompileMica(WindowCompositionContext context, MicaMaterial material)
    {
        var dark = material.Mode == MaterialThemeMode.Dark ||
                   material.Mode == MaterialThemeMode.Auto && context.IsDarkTheme;
        var alt = material.Variant == MicaVariant.Alt;
        var luminosity = dark ? (alt ? 20f : 32f) : (alt ? 224f : 242f);
        var opacity = dark ? (alt ? 0.88f : 0.8f) : (alt ? 0.72f : 0.6f);
        var brush = InvokeUtils(context, "CreateMicaBackdropBrush", 3, context.Compositor, luminosity, opacity);
        return brush ?? CompileAcrylic(context);
    }

    private static object? CompileAcrylic(WindowCompositionContext context)
    {
        return InvokeUtils(context, "CreateAcrylicBlurBackdropBrush", 1, context.Compositor);
    }

    private static object? InvokeUtils(WindowCompositionContext context, string name, int parameterCount, params object?[] args)
    {
        try
        {
            var method = context.Abi.UtilsType
                .GetMethods(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == name && candidate.GetParameters().Length == parameterCount);
            return method?.Invoke(null, args);
        }
        catch
        {
            return null;
        }
    }
}
