using System.Xml.Linq;
using PCL.UI.Controls;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private MyButton ConvertMyButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseEnum<MyButton.ColorState>(GetProp(element, styles, "ColorType"))
                is { } colorType)
                button.ColorType = colorType;
            if (ParseThickness(GetProp(element, styles, "Padding")) is { } padding)
                button.Padding = padding;
            return button;
        }

        private MyTextButton ConvertMyTextButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyTextButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? ""
            };
            ApplyCommon(button, element, styles);
            return button;
        }

        private MyIconButton ConvertMyIconButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyIconButton
            {
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = ResolveString(GetProp(element, styles, "Logo")) ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyIconTextButton ConvertMyIconTextButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyIconTextButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? "",
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = ResolveString(GetProp(element, styles, "Logo")) ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseEnum<MyIconTextButton.ColorState>(GetProp(element, styles, "ColorType"))
                is { } colorType)
                button.ColorType = colorType;
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyExtraButton ConvertMyExtraButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyExtraButton
            {
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = ResolveString(GetProp(element, styles, "Logo")) ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyExtraTextButton ConvertMyExtraTextButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var button = new MyExtraTextButton
            {
                Text = ResolveString(GetProp(element, styles, "Text"))
                    ?? ResolveString(GetProp(element, styles, "Content"))
                    ?? "",
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = ResolveString(GetProp(element, styles, "Logo")) ?? ""
            };
            ApplyCommon(button, element, styles);
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                button.LogoScale = logoScale;
            return button;
        }

        private MyCollapseBar ConvertMyCollapseBar(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var bar = new MyCollapseBar
            {
                Title = ResolveString(GetProp(element, styles, "Title")) ?? ""
            };
            ApplyCommon(bar, element, styles);
            AddChildren(bar, element, styleScope);
            return bar;
        }
    }
}