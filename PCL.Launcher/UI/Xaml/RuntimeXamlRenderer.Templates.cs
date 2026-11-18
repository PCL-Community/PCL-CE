using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Controls;
using PCL.Xaml;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private const int MaxTemplateDepth = 8;

        private int _templateDepth;

        /// <summary>实例化样式中的内联 ControlTemplate。模板内容仍走同一套白名单转换， 只替换 TemplateBinding 的文本属性，不实例化任意 CLR 类型。</summary>
        private Control? InstantiateTemplate(
            XElement template,
            XElement source,
            object? content,
            StyleScope styleScope)
        {
            if (_templateDepth >= MaxTemplateDepth)
            {
                result.Warnings.Add("ControlTemplate 嵌套过深，已停止展开");
                return null;
            }

            var root = template
                .Elements()
                .FirstOrDefault(child => !child.Name.LocalName.Contains('.'));
            if (root is null)
                return null;

            var clone = new XElement(root);
            var sourceStyles = styleScope.Resolve(source.Name.LocalName, source);
            SubstituteTemplateBindings(clone, source, sourceStyles, content);

            _templateDepth++;
            try
            {
                return ConvertElement(clone, styleScope);
            }
            finally
            {
                _templateDepth--;
            }
        }

        private void SubstituteTemplateBindings(
            XElement element,
            XElement source,
            IReadOnlyDictionary<string, string> sourceStyles,
            object? content)
        {
            foreach (var attribute in element.Attributes())
            {
                var key = TemplateBindingKey(attribute.Value);
                if (key is null)
                    continue;

                var value = key.Equals("Content", StringComparison.OrdinalIgnoreCase)
                    ? content as string
                    : FindAttribute(source, key)
                    ?? sourceStyles.GetValueOrDefault(key);
                if (value is null)
                    continue;

                // TemplateBinding 发生在安全校验之后，Source/Logo 等资源属性必须再次校验，
                // 防止通过 Content 注入 file:// 或路径穿越。
                if (IsSourceAttribute(attribute.Name.LocalName)
                    && !XamlSecurityValidator.IsSafeSource(value))
                    continue;

                attribute.Value = value;
            }

            foreach (var child in element.Elements())
                SubstituteTemplateBindings(child, source, sourceStyles, content);
        }

        private static string? TemplateBindingKey(string value)
        {
            var match = TemplateBindingPattern().Match(value);
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }

        private static bool IsSourceAttribute(string name)
        {
            return name is "Source"
                or "ImageSource"
                or "BaseUri"
                or "UriSource"
                or "FallbackSource"
                or "LoadingSource"
                or "Logo";
        }

        /// <summary>用 TextBox 的模板渲染只读代码块。模板中的 PART_ContentHost 由 TextBlock 替换， 其余 Border 样式继续走正常白名单转换。</summary>
        private Control? InstantiateTextBoxTemplate(
            XElement template,
            string text,
            IReadOnlyDictionary<string, string> styles,
            StyleScope styleScope)
        {
            var root = template
                .Elements()
                .FirstOrDefault(child => !child.Name.LocalName.Contains('.'));
            if (root is null)
                return null;

            var clone = new XElement(root);
            var contentHost = clone
                .DescendantsAndSelf()
                .FirstOrDefault(child => child.Name.LocalName.Equals(
                    "ScrollViewer",
                    StringComparison.OrdinalIgnoreCase));
            if (contentHost is null)
                return null;

            var textBlock = new XElement(
                XName.Get("TextBlock", contentHost.Name.NamespaceName),
                new XAttribute("Text", text),
                new XAttribute(
                    "TextWrapping",
                    styles.GetValueOrDefault("TextWrapping") ?? "Wrap"));
            foreach (var key in new[]
                     {
                         "FontFamily",
                         "FontSize",
                         "Foreground",
                         "LineHeight"
                     })
                if (styles.GetValueOrDefault(key) is { Length: > 0 } value)
                    textBlock.SetAttributeValue(key, value);

            contentHost.ReplaceWith(textBlock);
            var control = ConvertElement(clone, styleScope);
            if (control is null)
                return null;

            if (ParseThickness(styles.GetValueOrDefault("Margin")) is { } margin)
                control.Margin = margin;
            if (ParseDouble(styles.GetValueOrDefault("Height")) is { } height and > 0d)
                control.Height = height;
            return control;
        }

        [GeneratedRegex(
            @"^\{\s*TemplateBinding\s+([A-Za-z_][\w.]*)\s*\}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled)]
        private static partial Regex TemplateBindingPattern();
    }
}