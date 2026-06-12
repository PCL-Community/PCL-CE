using System.Text.RegularExpressions;

namespace PCL
{
    public static class XamlEventSanitizer
    {
        public class SanitizeResult
        {
            public string SanitizedXaml { get; set; } = "";
            public List<string> UnsupportedTypesFound { get; } = new();
            public List<string> UnrecognizedTypes { get; } = new();
        }

        private static readonly Regex EventTypeAttributeRegex = new(
            @"(local:CustomEventService\.EventType\s*=\s*"")([^""]+)("")",
            RegexOptions.Compiled);

        private static readonly Regex EventTypePropertyElementRegex = new(
            @"(<local:CustomEventService\.EventType\s*>\s*)([^<]+?)(\s*</local:CustomEventService\.EventType\s*>)",
            RegexOptions.Compiled);

        private static readonly Regex LocalCustomEventTypeAttributeRegex = new(
            @"(<local:CustomEvent\s+[^>]*?\bType\s*=\s*"")([^""]+)("")",
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

            sanitized = LocalCustomEventTypeAttributeRegex.Replace(sanitized, match =>
            {
                var chineseValue = match.Groups[2].Value;
                return ReplaceEventType(match, chineseValue, result);
            });

            var unsupportedSnapshot = result.UnsupportedTypesFound.ToList();
            foreach (var type in unsupportedSnapshot)
                sanitized = RemoveElementsWithEventType(sanitized, type, result.UnsupportedTypesFound);

            var unrecognizedSnapshot = result.UnrecognizedTypes.ToList();
            foreach (var type in unrecognizedSnapshot)
                sanitized = RemoveElementsWithEventType(sanitized, type, result.UnrecognizedTypes);

            var distinctUnsupported = new List<string>(new HashSet<string>(result.UnsupportedTypesFound));
            var distinctUnrecognized = new List<string>(new HashSet<string>(result.UnrecognizedTypes));
            result.UnsupportedTypesFound.Clear();
            result.UnsupportedTypesFound.AddRange(distinctUnsupported);
            result.UnrecognizedTypes.Clear();
            result.UnrecognizedTypes.AddRange(distinctUnrecognized);

            result.SanitizedXaml = sanitized;
            return result;
        }

        private static string ReplaceEventType(Match match, string chineseValue, SanitizeResult result)
        {
            if (EventTypeMapper.TryToEnglish(chineseValue, out var englishName))
                return match.Groups[1].Value + englishName + match.Groups[3].Value;

            if (Enum.TryParse<EventType>(chineseValue, true, out _))
                return match.Value;

            if (EventTypeMapper.IsUnSupportedType(chineseValue))
            {
                result.UnsupportedTypesFound.Add(chineseValue);
                return match.Value;
            }

            result.UnrecognizedTypes.Add(chineseValue);
            return match.Value;
        }

        private static string RemoveElementsWithEventType(string xaml, string eventTypeValue, List<string> trackingList)
        {
            var escaped = Regex.Escape(eventTypeValue);

            var selfClosingPattern = $@"<\w+[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*/\s*>";
            xaml = Regex.Replace(xaml, selfClosingPattern, match =>
            {
                trackingList.Add(eventTypeValue);
                return "";
            }, RegexOptions.Compiled);

            var openTagPattern = $@"<\w+[^>]*\s+local:CustomEventService\.EventType\s*=\s*""{escaped}""[^>]*>";
            xaml = Regex.Replace(xaml, openTagPattern, match =>
            {
                var elementName = Regex.Match(match.Value, @"<(\w+)").Groups[1].Value;
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
                var lastOpenMatch = Regex.Match(beforeMatch, @"<(\w+)[^>]*>$", RegexOptions.RightToLeft);
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
