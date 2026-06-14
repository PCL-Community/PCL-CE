using System.Text.RegularExpressions;

namespace PCL
{
    public static class XamlEventSanitizer
    {
        public class SanitizeResult
        {
            public string SanitizedXaml { get; set; } = "";
            public List<string> UnsupportedTypesFound { get; set; } = new();
            public List<string> UnrecognizedTypes { get; set; } = new();
        }

        private static readonly Regex EventTypeAttributeRegex = new(
            @"(local:CustomEventService\.EventType\s*=\s*"")([^""]+)("")",
            RegexOptions.Compiled);

        private static readonly Regex EventTypePropertyElementRegex = new(
            @"(<local:CustomEventService\.EventType\s*>\s*)([^<]+?)(\s*</local:CustomEventService\.EventType\s*>)",
            RegexOptions.Compiled);

        private static readonly Regex CustomEventTypePropertyElementRegex = new(
            @"(<local:CustomEvent\.Type\s*>\s*)([^<]+?)(\s*</local:CustomEvent\.Type\s*>)",
            RegexOptions.Compiled);

        private static readonly Regex LocalCustomEventTypeAttributeRegex = new(
            @"(<local:CustomEvent\s+[^>]*?\bType\s*=\s*"")([^""]+)("")",
            RegexOptions.Compiled);

        private static readonly Regex LocalCustomEventSelfClosingRegex = new(
            @"<local:CustomEvent\s+[^>]*\bType\s*=\s*""([^""]+)""[^>]*/\s*>",
            RegexOptions.Compiled);

        private static readonly Regex LocalCustomEventOpenTagRegex = new(
            @"<local:CustomEvent\s+[^>]*\bType\s*=\s*""([^""]+)""[^>]*>",
            RegexOptions.Compiled);

        private static readonly Regex SetterEventTypeValueRegex = new(
            @"(Property\s*=\s*""local:CustomEventService\.EventType""\s+Value\s*=\s*"")([^""]+)("")",
            RegexOptions.Compiled);

        private static readonly Regex SetterValueEventTypeRegex = new(
            @"(Value\s*=\s*"")([^""]+)(""\s+Property\s*=\s*""local:CustomEventService\.EventType"")",
            RegexOptions.Compiled);

        public static SanitizeResult Sanitize(string xaml)
        {
            var result = new SanitizeResult();
            var sanitized = xaml;

            sanitized = EventTypeAttributeRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[2].Value;
                return ReplaceEventType(match, chineseValue, result);
            });

            sanitized = EventTypePropertyElementRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[2].Value.Trim();
                return ReplaceEventType(match, chineseValue, result);
            });

            sanitized = CustomEventTypePropertyElementRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[2].Value.Trim();
                return ReplaceEventType(match, chineseValue, result);
            });

            sanitized = LocalCustomEventTypeAttributeRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[2].Value;
                return ReplaceEventType(match, chineseValue, result);
            });

            sanitized = LocalCustomEventSelfClosingRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[1].Value;
                return _RemoveBadCustomEventElement(match, chineseValue, result);
            });

            sanitized = LocalCustomEventOpenTagRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[1].Value;
                if (_IsBadType(chineseValue, result))
                {
                    var elementName = "local:CustomEvent";
                    var afterTag = xaml[(match.Index + match.Length)..];
                    var closeLen = FindMatchingCloseTag(afterTag, elementName);
                    if (closeLen < 0) return match.Value;
                    return "";
                }
                return match.Value;
            });

            sanitized = SetterEventTypeValueRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[2].Value;
                return ReplaceOrRemoveSetter(match, chineseValue, result);
            });

            sanitized = SetterValueEventTypeRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[2].Value;
                return ReplaceOrRemoveSetter(match, chineseValue, result);
            });

            RemoveElementsForTypes(result.UnsupportedTypesFound, ref sanitized);
            RemoveElementsForTypes(result.UnrecognizedTypes, ref sanitized);

            result.UnsupportedTypesFound = result.UnsupportedTypesFound.Distinct().ToList();
            result.UnrecognizedTypes = result.UnrecognizedTypes.Distinct().ToList();

            result.SanitizedXaml = sanitized;
            return result;
        }

        private static bool _IsBadType(string chineseValue, SanitizeResult result)
        {
            if (EventTypeMapper.IsUnsupportedType(chineseValue))
            {
                result.UnsupportedTypesFound.Add(chineseValue);
                return true;
            }
            if (!Enum.TryParse<EventType>(chineseValue, true, out _)
                && !EventTypeMapper.TryToEnglish(chineseValue, out _))
            {
                result.UnrecognizedTypes.Add(chineseValue);
                return true;
            }
            return false;
        }

        private static string _RemoveBadCustomEventElement(
            Match match, string chineseValue, SanitizeResult result)
        {
            return _IsBadType(chineseValue, result) ? "" : match.Value;
        }

        private static void RemoveElementsForTypes(List<string> types, ref string sanitized)
        {
            var snapshot = types.ToList();
            foreach (var type in snapshot)
                sanitized = RemoveElementsWithEventType(sanitized, type, types);
        }

        private static string ReplaceEventType(Match match, string rawValue, SanitizeResult result)
        {
            if (EventTypeMapper.TryToEnglish(rawValue, out var englishName))
                return match.Groups[1].Value + englishName + match.Groups[3].Value;

            if (Enum.TryParse<EventType>(rawValue, true, out _))
                return match.Value;

            if (EventTypeMapper.IsUnsupportedType(rawValue))
            {
                result.UnsupportedTypesFound.Add(rawValue);
                return match.Value;
            }

            result.UnrecognizedTypes.Add(rawValue);
            return match.Value;
        }

        private static string ReplaceOrRemoveSetter(Match match, string chineseValue, SanitizeResult result)
        {
            if (_IsBadType(chineseValue, result))
                return "";
            return ReplaceEventType(match, chineseValue, result);
        }

        private static string RemoveElementsWithEventType(string xaml, string eventTypeValue, List<string> trackingList)
        {
            var escaped = Regex.Escape(eventTypeValue);

            var selfClosingPattern = $@"<[\w:]+[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*/\s*>";
            xaml = Regex.Replace(xaml, selfClosingPattern, match =>
            {
                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            var openTagPattern = $@"<[\w:]+[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*>";
            xaml = Regex.Replace(xaml, openTagPattern, match =>
            {
                var elementName = Regex.Match(match.Value, @"<([\w:]+)").Groups[1].Value;
                var afterTag = xaml[(match.Index + match.Length)..];
                var closeLen = FindMatchingCloseTag(afterTag, elementName);
                if (closeLen < 0) return match.Value;

                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            var propertyElementPattern = $@"<local:CustomEventService\.EventType\s*>\s*{escaped}\s*</local:CustomEventService\.EventType\s*>";
            xaml = Regex.Replace(xaml, propertyElementPattern, match =>
            {
                var beforeMatch = xaml[..match.Index];
                var lastOpenMatch = Regex.Match(beforeMatch, @"<([\w:]+)[^>]*>\s*$", RegexOptions.RightToLeft);
                if (!lastOpenMatch.Success) return match.Value;
                var parentElementName = lastOpenMatch.Groups[1].Value;

                var afterProperty = xaml[(match.Index + match.Length)..];
                var parentCloseMatch = Regex.Match(afterProperty, $@"</{parentElementName}\s*>");
                if (!parentCloseMatch.Success) return match.Value;

                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            return xaml;
        }

        private static int FindMatchingCloseTag(string text, string elementName)
        {
            var closePattern = $@"</{elementName}\s*>";
            var closeMatch = Regex.Match(text, closePattern);
            return closeMatch.Success ? closeMatch.Index + closeMatch.Length : -1;
        }
    }
}
