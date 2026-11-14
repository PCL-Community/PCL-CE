using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Controls;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private readonly Dictionary<string, string> _localResources =
            new(StringComparer.OrdinalIgnoreCase);

        private void ApplyEvent(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var rawType = GetProp(element, styles, "EventType")
                ?? FindAttribute(element, "CustomEventService.EventType");

            if (string.IsNullOrWhiteSpace(rawType))
                return;

            if (EventTypeMapper.IsUnsupportedType(rawType))
            {
                result.UnsupportedEventTypes.Add(rawType);
                _rejectedControls.Add(control);
                return;
            }

            if (!EventTypeMapper.TryParse(rawType, out var eventType))
            {
                result.UnrecognizedEventTypes.Add(rawType);
                _rejectedControls.Add(control);
                return;
            }

            var rawData = GetProp(element, styles, "EventData")
                ?? FindAttribute(element, "CustomEventService.EventData")
                ?? "";

            CustomEventService.SetEventType(control, eventType);
            CustomEventService.SetEventData(
                control,
                ResolveString(rawData) ?? rawData);
        }

        public void CollectStringResources(XElement root)
        {
            foreach (var element in root.DescendantsAndSelf())
            {
                if (!element.Name.LocalName.Equals(
                        "String",
                        StringComparison.OrdinalIgnoreCase))
                    continue;

                var key = FindAttribute(element, "Key");

                if (!string.IsNullOrWhiteSpace(key))
                    _localResources[key] = element.Value;
            }
        }

        private void ApplyCommon(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            ApplyLayoutProperties(control, element, styles);
            ApplyStateProperties(control, element, styles);
            ApplyMetadataProperties(control, element, styles);
            ApplyControlSpecificProperties(control, element, styles);

            ApplyEvent(control, element, styles);
        }

        private static void ApplyLayoutProperties(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseThickness(GetProp(element, styles, "Margin")) is { } margin)
                control.Margin = margin;

            if (ParseDouble(GetProp(element, styles, "Width")) is { } width and > 0d)
                control.Width = width;

            if (ParseDouble(GetProp(element, styles, "Height")) is { } height and > 0d)
                control.Height = height;

            if (ParseDouble(GetProp(element, styles, "MinWidth")) is { } minWidth and >= 0d)
                control.MinWidth = minWidth;

            if (ParseDouble(GetProp(element, styles, "MaxWidth")) is { } maxWidth and > 0d)
                control.MaxWidth = maxWidth;

            if (ParseDouble(GetProp(element, styles, "MinHeight")) is { } minHeight and >= 0d)
                control.MinHeight = minHeight;

            if (ParseDouble(GetProp(element, styles, "MaxHeight")) is { } maxHeight and > 0d)
                control.MaxHeight = maxHeight;

            control.HorizontalAlignment = ParseHorizontalAlignment(
                    GetProp(element, styles, "HorizontalAlignment"))
                ?? control.HorizontalAlignment;

            control.VerticalAlignment = ParseVerticalAlignment(
                    GetProp(element, styles, "VerticalAlignment"))
                ?? control.VerticalAlignment;
        }

        private void ApplyStateProperties(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseDouble(GetProp(element, styles, "Opacity")) is { } opacity)
                control.Opacity = Math.Clamp(opacity, 0d, 1d);

            ApplyVisibility(control, element, styles);

            if (ParseBool(GetProp(element, styles, "IsEnabled")) is { } isEnabled)
                control.IsEnabled = isEnabled;

            if (ParseBool(GetProp(element, styles, "IsHitTestVisible")) is
                { } isHitTestVisible)
                control.IsHitTestVisible = isHitTestVisible;

            if (ParseBool(GetProp(element, styles, "ClipToBounds")) is { } clipToBounds)
                control.ClipToBounds = clipToBounds;

            if (ParseInt(GetProp(element, styles, "Panel.ZIndex")) is { } zIndex)
                control.ZIndex = zIndex;
        }

        private static void ApplyVisibility(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseBool(GetProp(element, styles, "IsVisible")) is { } isVisible)
            {
                control.IsVisible = isVisible;
                return;
            }

            if (GetProp(element, styles, "Visibility") is not { } visibility)
                return;

            control.IsVisible =
                !visibility.Equals("Collapsed", StringComparison.OrdinalIgnoreCase)
                && !visibility.Equals("Hidden", StringComparison.OrdinalIgnoreCase);
        }

        private void ApplyMetadataProperties(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (GetProp(element, styles, "Tag") is { } tag)
                control.Tag = tag;

            var tooltip = GetProp(element, styles, "ToolTip.Tip")
                ?? GetProp(element, styles, "ToolTip");

            if (tooltip is not null)
                ToolTip.SetTip(control, ResolveString(tooltip));
        }

        private void ApplyControlSpecificProperties(
            Control control,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            switch (control)
            {
                case TextBlock textBlock:
                    ApplyTextBlockProperties(textBlock, element, styles);
                    break;

                case Border border:
                    ApplyBorderProperties(border, element, styles);
                    break;

                case Panel panel:
                    ApplyPanelProperties(panel, element, styles);
                    break;

                case MyImage image:
                    ApplyMyImageProperties(image, element, styles);
                    break;
            }

            if (control is MyCard card
                && ParseCornerRadius(GetProp(element, styles, "CornerRadius")) is
                    { } cardRadius)
                card.CornerRadius = cardRadius;
        }

        private void ApplyTextBlockProperties(
            TextBlock block,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseDouble(GetProp(element, styles, "FontSize")) is { } fontSize and > 0d)
                block.FontSize = fontSize;

            if (GetProp(element, styles, "FontWeight") is { } fontWeight)
                block.FontWeight = ParseFontWeight(fontWeight);

            if (GetProp(element, styles, "FontStyle") is { } fontStyle)
                block.FontStyle = ParseFontStyle(fontStyle);

            if (GetProp(element, styles, "FontFamily") is { Length: > 0 } fontFamily)
                block.FontFamily = new FontFamily(fontFamily);

            if (ParseDouble(GetProp(element, styles, "LineHeight")) is
                { } lineHeight and > 0d)
                block.LineHeight = lineHeight;

            if (ParseEnum<TextWrapping>(GetProp(element, styles, "TextWrapping")) is
                { } wrapping)
                block.TextWrapping = wrapping;

            if (ParseTextTrimming(GetProp(element, styles, "TextTrimming")) is
                { } trimming)
                block.TextTrimming = trimming;

            if (ParseEnum<TextAlignment>(GetProp(element, styles, "TextAlignment")) is
                { } alignment)
                block.TextAlignment = alignment;

            if (ParseBrush(GetProp(element, styles, "Foreground")) is { } foreground)
                block.Foreground = foreground;

            if (ParseBrush(GetProp(element, styles, "Background")) is { } background)
                block.Background = background;
        }

        private void ApplyBorderProperties(
            Border border,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseBrush(GetProp(element, styles, "Background")) is { } background)
                border.Background = background;

            if (ParseBrush(GetProp(element, styles, "BorderBrush")) is { } borderBrush)
                border.BorderBrush = borderBrush;

            if (ParseThickness(GetProp(element, styles, "BorderThickness")) is
                { } borderThickness)
                border.BorderThickness = borderThickness;

            if (ParseThickness(GetProp(element, styles, "Padding")) is { } padding)
                border.Padding = padding;

            if (ParseCornerRadius(GetProp(element, styles, "CornerRadius")) is
                { } cornerRadius)
                border.CornerRadius = cornerRadius;
        }

        private void ApplyPanelProperties(
            Panel panel,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ParseBrush(GetProp(element, styles, "Background")) is { } background)
                panel.Background = background;
        }

        private void ApplyMyImageProperties(
            MyImage image,
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            if (ResolveString(GetProp(element, styles, "Source")) is
                { Length: > 0 } source)
                image.Source = NormalizeImageSource(source);

            if (ResolveString(GetProp(element, styles, "FallbackSource")) is
                { Length: > 0 } fallback)
                image.FallbackSource = NormalizeImageSource(fallback);

            if (ParseEnum<Stretch>(GetProp(element, styles, "Stretch")) is
                { } stretch)
                image.Stretch = stretch;

            if (ParseCornerRadius(GetProp(element, styles, "CornerRadius")) is
                { } cornerRadius)
                image.CornerRadius = cornerRadius;
        }

        private static bool LooksLikeBitmapPath(string path)
        {
            return path.EndsWith(".png", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeImageSource(string source)
        {
            const string imagesMarker = "/images/";

            var index = source.IndexOf(
                imagesMarker,
                StringComparison.OrdinalIgnoreCase);

            if (!source.StartsWith("pack://", StringComparison.OrdinalIgnoreCase)
                && !source.StartsWith(imagesMarker, StringComparison.OrdinalIgnoreCase))
                return source;

            return index >= 0
                ? LauncherPaths.ImageRoot + source[(index + imagesMarker.Length)..]
                : source;
        }
    }
}