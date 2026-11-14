using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Controls;
using PCL.Xaml;

namespace PCL.UI.Xaml;

/// <summary>运行时 XAML 渲染选项。</summary>
public sealed class RuntimeXamlRenderOptions
{
    /// <summary>是否读取并应用内联 <c>Resources/Style</c>；默认开启。</summary>
    public bool ApplyLocalStyles { get; init; } = true;
}

/// <summary>运行时 XAML 渲染结果。</summary>
public sealed class RuntimeXamlRenderResult
{
    /// <summary>渲染出的顶层控件，按原 XAML 顺序排列。</summary>
    public List<Control> Controls { get; } = [];

    /// <summary>已移除的不受支持事件类型（例如 CE 不再提供的功能）。</summary>
    public List<string> UnsupportedEventTypes { get; } = [];

    /// <summary>无法识别的自定义事件类型。</summary>
    public List<string> UnrecognizedEventTypes { get; } = [];

    /// <summary>非致命提示（例如忽略了未知的样式属性）。</summary>
    public List<string> Warnings { get; } = [];

    public bool Success { get; internal set; }

    public string? Error { get; internal set; }
}

/// <summary>
///     白名单运行时 XAML 渲染器：先用 <see cref="XamlSecurityValidator" /> 校验，
///     再自行把受支持的 PCL/基础控件映射为 Avalonia 控件。不使用 Avalonia 运行时 XAML 加载器，
///     因此不会实例化任意 CLR 类型，也不会执行事件处理器或加载外部资源字典。
/// </summary>
public static partial class RuntimeXamlRenderer
{
    private static readonly HashSet<string> IgnoredElementNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "SolidColorBrush",
            "LinearGradientBrush",
            "RadialGradientBrush",
            "ImageBrush",
            "GradientStop",
            "DropShadowEffect",
            "BlurEffect",
            "ScaleTransform",
            "TranslateTransform",
            "RotateTransform",
            "SkewTransform",
            "TransformGroup",
            "MatrixTransform",
            "Color",
            "Thickness",
            "Point",
            "Vector",
            "Rect",
            "Size",
            "String",
            "ControlTemplate"
        };

    public static RuntimeXamlRenderResult RenderFragment(
        string? fragment,
        Func<string, object?> resolveResource,
        RuntimeXamlRenderOptions? options = null)
    {
        var result = new RuntimeXamlRenderResult();
        if (string.IsNullOrWhiteSpace(fragment))
        {
            result.Success = true;
            return result;
        }

        var security = XamlSecurityValidator.ValidateFragment(fragment);
        if (!security.IsValid)
        {
            result.Error = string.Join("\n", security.Errors);
            return result;
        }

        try
        {
            var wrapped = "<Root xmlns=\""
                + XamlSecurityValidator.PresentationNamespace
                + "\""
                + " xmlns:x=\""
                + XamlSecurityValidator.XamlNamespace
                + "\""
                + " xmlns:local=\"clr-namespace:PCL\""
                + " xmlns:sys=\"clr-namespace:System;assembly=System.Runtime\">"
                + StripXmlDeclaration(fragment)
                + "</Root>";
            var document = XDocument.Parse(wrapped, LoadOptions.PreserveWhitespace);
            if (document.Root is null)
            {
                result.Success = true;
                return result;
            }

            var context = new RenderContext(
                resolveResource,
                options ?? new RuntimeXamlRenderOptions(),
                result);
            context.CollectStringResources(document.Root);
            foreach (var element in document.Root.Elements())
            {
                var control = context.ConvertElement(element, null);
                if (control is not null)
                    result.Controls.Add(control);
            }

            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Error = ex.Message;
        }

        return result;
    }

    [GeneratedRegex(@"^\{([A-Za-z_][\w.:]*)\s+([^{}]*)\}$", RegexOptions.Compiled)]
    private static partial Regex MarkupExtensionPattern();

    private sealed partial class RenderContext(
        Func<string, object?> resolveResource,
        RuntimeXamlRenderOptions options,
        RuntimeXamlRenderResult result)
    {
        private readonly HashSet<Control> _rejectedControls = [];
    }
}