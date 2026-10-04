using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using PCL.Core.Logging;

namespace PCL.Core.UI.Controls.SvgIcon;

public static class SvgIconLoader
{
    public const string DefaultIconPack = "default";

    private static readonly string _AssemblyName =
        typeof(SvgIconLoader).Assembly.GetName().Name ?? "PCL.Core";

    private static readonly ConcurrentDictionary<string, Lazy<SvgIconModel?>> _Cache =
        new(StringComparer.OrdinalIgnoreCase);

    internal static SvgIconModel? Load(string? icon, string? defaultPack = null)
    {
        var key = SvgIconKey.TryParse(icon, defaultPack ?? DefaultIconPack);
        if (key is null)
        {
            if (!string.IsNullOrWhiteSpace(icon))
                _LogDebug($"无效的 SVG 图标标识：{icon}");
            return null;
        }

        var cacheKey = key.Value.ToString();
        return _Cache.GetOrAdd(cacheKey, _ => new Lazy<SvgIconModel?>(() => _LoadCore(key.Value))).Value;
    }

    public static void ClearCache()
    {
        _Cache.Clear();
    }

    private static SvgIconModel? _LoadCore(SvgIconKey key)
    {
        try
        {
            var uri = new Uri(
                $"pack://application:,,,/{_AssemblyName};component/UI/Assets/IconPacks/{key.Pack}/{key.Name}.svg",
                UriKind.Absolute);
            var info = Application.GetResourceStream(uri);
            if (info is null)
            {
                _LogDebug($"缺少 SVG 图标资源：{key} ({uri})");
                return null;
            }

            using var stream = info.Stream;
            using var reader = new StreamReader(stream, Encoding.UTF8, true);
            var svg = reader.ReadToEnd();
            return SvgIconParser.Parse(svg);
        }
        catch (Exception ex)
        {
            _LogDebug($"加载 SVG 图标失败：{key}", ex);
            return null;
        }
    }

    private static void _LogDebug(string message, Exception? ex = null)
    {
#if DEBUG
        if (ex is null)
            LogWrapper.Debug("SvgIcon", message);
        else
            LogWrapper.Debug(ex, "SvgIcon", message);
#endif
    }

    private readonly record struct SvgIconKey(string Pack, string Name)
    {
        public static SvgIconKey? TryParse(string? icon, string defaultPack)
        {
            if (string.IsNullOrWhiteSpace(icon))
                return null;

            var normalized = icon.Trim().Replace('\\', '/');
            if (normalized.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                normalized = normalized[..^4];

            normalized = normalized.Trim('/');
            if (normalized.Length == 0 || normalized.Contains("..", StringComparison.Ordinal))
                return null;

            var parts = normalized.Split('/', 2,
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var pack = parts.Length == 2 ? parts[0] : defaultPack;
            var name = parts.Length == 2 ? parts[1] : parts[0];

            if (!_IsSafeResourcePath(pack) || !_IsSafeResourcePath(name))
                return null;

            return new SvgIconKey(pack, name);
        }

        public override string ToString()
        {
            return $"{Pack}/{Name}";
        }

        private static bool _IsSafeResourcePath(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.StartsWith('/') || value.EndsWith('/'))
                return false;

            return value.Split('/', StringSplitOptions.RemoveEmptyEntries).All(_IsSafeResourceSegment);
        }

        private static bool _IsSafeResourceSegment(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value is "." or "..")
                return false;

            return value.All(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.');
        }
    }
}