using System.Text.RegularExpressions;
using System.Xml.Linq;
using Avalonia.Media;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private static void CollectStyles(XElement element, StyleScope scope)
        {
            foreach (var resources in element
                         .Elements()
                         .Where(child =>
                             child.Name.LocalName.EndsWith(
                                 ".Resources",
                                 StringComparison.OrdinalIgnoreCase)
                             || child.Name.LocalName.Equals(
                                 "Resources",
                                 StringComparison.OrdinalIgnoreCase)))
            foreach (var style in EnumerateStyles(resources))
            {
                var setters = ReadSetters(style);
                if (setters.Count == 0)
                    continue;

                var key = FindAttribute(style, "Key");
                if (!string.IsNullOrWhiteSpace(key))
                {
                    scope.ByKey[key] = setters;
                    continue;
                }

                var targetType = FindAttribute(style, "TargetType");
                if (!string.IsNullOrWhiteSpace(targetType))
                {
                    targetType = StripTypePrefix(targetType);
                    if (scope.ByType.TryGetValue(targetType, out var existing))
                        foreach (var pair in setters)
                            existing[pair.Key] = pair.Value;
                    else
                        scope.ByType[targetType] = new Dictionary<string, string>(
                            setters,
                            StringComparer.OrdinalIgnoreCase);
                }
            }
        }

        private static IEnumerable<XElement> EnumerateStyles(XElement container)
        {
            foreach (var child in container.Elements())
                switch (child.Name.LocalName)
                {
                    case "Style":
                        yield return child;
                        continue;

                    case "ResourceDictionary":
                    {
                        foreach (var nested in EnumerateStyles(child))
                            yield return nested;
                        break;
                    }
                }
        }

        private static Dictionary<string, string> ReadSetters(XElement style)
        {
            var setters = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var setterElements =
                style.Elements().Where(child => child.Name.LocalName == "Setter").ToList();
            foreach (var setterContainer in style
                         .Elements()
                         .Where(child => child.Name.LocalName == "Style.Setters"))
                setterElements.AddRange(
                    setterContainer
                        .Elements()
                        .Where(child => child.Name.LocalName == "Setter"));

            foreach (var setter in setterElements)
            {
                var property = FindAttribute(setter, "Property");
                var value = FindAttribute(setter, "Value")
                    ?? setter
                        .Elements()
                        .FirstOrDefault(child => child.Name.LocalName == "Setter.Value")
                        ?.Value;
                if (string.IsNullOrWhiteSpace(property) || value is null)
                    continue;
                if (property.Contains("Command", StringComparison.OrdinalIgnoreCase))
                    continue;
                setters[property] = value;
            }

            return setters;
        }

        private static string StripTypePrefix(string value)
        {
            var trimmed = value.Trim();
            var match = Regex.Match(trimmed, @"^\{x:Type\s+([^}]+)\}$");
            if (match.Success)
                trimmed = match.Groups[1].Value.Trim();
            var separator = trimmed.LastIndexOf(':');
            return separator >= 0 ? trimmed[(separator + 1)..] : trimmed;
        }

        private string? ResolveString(string? raw)
        {
            if (raw is null)
                return null;

            var match = MarkupExtensionPattern().Match(raw.Trim());
            if (!match.Success)
                return raw;
            
            var extension = match.Groups[1].Value;
            if (extension is not ("DynamicResource" or "StaticResource"))
                return raw;
            
            var key = match.Groups[2].Value.Trim();
            return _localResources.TryGetValue(key, out var localValue)
                ? localValue
                : resolveResource(key)?.ToString() ?? "";
        }

        private IBrush? ParseBrush(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            var resolved = ResolveString(raw);
            if (resolved is not null && !ReferenceEquals(resolved, raw))
                if (resolveResource(MarkupKey(raw) ?? "") is IBrush brush)
                    return brush;

            var value = (resolved ?? raw).Trim();
            if (value.StartsWith('#'))
                try
                {
                    return new SolidColorBrush(Color.Parse(value));
                }
                catch (Exception)
                {
                    return null;
                }

            return value switch
            {
                "Transparent" => Brushes.Transparent,
                "White" => Brushes.White,
                "Black" => Brushes.Black,
                "Red" => Brushes.Red,
                "Blue" => Brushes.Blue,
                "Green" => Brushes.Green,
                "Gray" or "Grey" => Brushes.Gray,
                _ => null
            };
        }

        private static string? MarkupKey(string raw)
        {
            var match = MarkupExtensionPattern().Match(raw.Trim());
            return match.Success ? match.Groups[2].Value.Trim() : null;
        }
    }

    private sealed class StyleScope(StyleScope? parent)
    {
        public Dictionary<string, Dictionary<string, string>> ByKey { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        public Dictionary<string, Dictionary<string, string>> ByType { get; } =
            new(StringComparer.OrdinalIgnoreCase);

        private StyleScope? Parent { get; } = parent;

        public IReadOnlyDictionary<string, string> Resolve(string elementName, XElement element)
        {
            var merged = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var chain = new Stack<StyleScope>();
            for (var scope = this; scope is not null; scope = scope.Parent)
                chain.Push(scope);

            foreach (var scope in chain)
                if (scope.ByType.TryGetValue(elementName, out var setters))
                    foreach (var pair in setters)
                        merged[pair.Key] = pair.Value;

            var styleKey = MarkupKey(FindAttribute(element, "Style"));
            if (!string.IsNullOrWhiteSpace(styleKey))
                for (var scope = this; scope is not null; scope = scope.Parent)
                    if (scope.ByKey.TryGetValue(styleKey, out var setters))
                    {
                        foreach (var pair in setters)
                            merged[pair.Key] = pair.Value;
                        break;
                    }

            return merged;
        }

        private static string? MarkupKey(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return null;
            var match = Regex.Match(raw.Trim(), @"^\{[A-Za-z_][\w.:]*\s+([^{}]*)\}$");
            return match.Success ? match.Groups[1].Value.Trim() : null;
        }
    }
}