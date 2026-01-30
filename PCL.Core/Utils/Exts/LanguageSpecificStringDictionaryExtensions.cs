using System.Globalization;
using System.Linq;
using System.Windows.Markup;
using System.Windows.Media;

namespace PCL.Core.Utils.Exts;

public static class LanguageSpecificStringDictionaryExtensions
{
    public static string GetForCurrentUiCulture(this LanguageSpecificStringDictionary dict, string? fallback = null)
    {
        var ui = CultureInfo.CurrentUICulture;

        // 1) 精确匹配，如 zh-Hans-CN
        if (TryFromTag(dict, ui.IetfLanguageTag, out var v))
            return v;

        // 2) 逐级回退，如 zh-Hans-CN -> zh-Hans -> zh
        var tag = ui.IetfLanguageTag;
        for (var dash = tag.LastIndexOf('-'); dash > 0; dash = tag.LastIndexOf('-'))
        {
            tag = tag.Substring(0, dash);
            if (TryFromTag(dict, tag, out v))
                return v;
        }

        // 3) 再尝试父文化（当 CurrentUICulture 是特定文化时）
        if (!ui.IsNeutralCulture && TryFromTag(dict, ui.Parent.IetfLanguageTag, out v))
            return v;

        // 4) 兜底：取字典中的第一个值，或使用传入的 fallback，或空字符串
        if (dict.Count > 0) return dict.Values!.First();
        return fallback ?? string.Empty;

        static bool TryFromTag(LanguageSpecificStringDictionary d, string ietf, out string value)
            => d.TryGetValue(XmlLanguage.GetLanguage(ietf), out value!);
    }
}
