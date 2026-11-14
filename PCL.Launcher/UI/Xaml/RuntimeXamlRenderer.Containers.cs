using System.Xml.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using PCL.UI.Controls;
using PCL.Xaml;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        private MyCard ConvertMyCard(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var card = new MyCard();
            ApplyCommon(card, element, styles);

            var titleElement = element
                .Elements()
                .FirstOrDefault(child => child.Name.LocalName == "MyCard.Title");
            var title = titleElement is not null
                ? titleElement.Value.Trim()
                : ResolveString(GetProp(element, styles, "Title"));
            if (!string.IsNullOrEmpty(title))
                card.Title = title;

            var canSwap = ParseBool(GetProp(element, styles, "CanSwap")) ?? false;
            var isSwapped = ParseBool(GetProp(element, styles, "IsSwapped"))
                ?? ParseBool(GetProp(element, styles, "IsSwaped"))
                ?? false;

            var childCountBefore = card.Children.Count;
            AddChildren(card, element, styleScope);

            if (canSwap || isSwapped)
            {
                card.CanSwap = true;
                if (card.Children.Count > childCountBefore)
                    card.SwapControl = card.Children[childCountBefore];
                if (isSwapped)
                    card.IsSwapped = true;
            }

            return card;
        }

        private MyHint ConvertMyHint(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var hint = new MyHint
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(hint, element, styles);
            if (ParseBool(GetProp(element, styles, "IsWarn")) is { } isWarn)
                hint.IsWarn = isWarn;
            if (string.IsNullOrWhiteSpace(hint.Text) && !element.HasElements)
                hint.Text = element.Value.Trim();
            return hint;
        }

        private MyListItem ConvertMyListItem(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var logo = ResolveString(GetProp(element, styles, "Logo")) ?? "";
            if (LooksLikeBitmapPath(logo) && !XamlSecurityValidator.IsSafeSource(logo))
                logo = "";
            if (logo.Contains("/images/", StringComparison.OrdinalIgnoreCase)
                || logo.StartsWith("pack://", StringComparison.OrdinalIgnoreCase))
                logo = NormalizeImageSource(logo);
            var item = new MyListItem
            {
                Title = ResolveString(GetProp(element, styles, "Title")) ?? "",
                Info = ResolveString(GetProp(element, styles, "Info")) ?? "",
                SvgIcon = GetProp(element, styles, "SvgIcon") ?? "",
                Logo = logo
            };
            ApplyCommon(item, element, styles);
            if (ParseEnum<MyListItem.CheckType>(GetProp(element, styles, "Type"))
                is { } checkType)
                item.Type = checkType;
            if (ParseBool(GetProp(element, styles, "Checked")) is { } isChecked)
                item.Checked = isChecked;
            if (ParseDouble(GetProp(element, styles, "LogoScale")) is { } logoScale)
                item.LogoScale = logoScale;
            return item;
        }

        private MyLoading ConvertMyLoading(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var loading = new MyLoading
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(loading, element, styles);
            return loading;
        }

        private MyTextBox ConvertMyTextBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var textBox = new MyTextBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? "",
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(textBox, element, styles);
            if (ParseInt(GetProp(element, styles, "MaxLength")) is { } maxLength and > 0)
                textBox.MaxLength = maxLength;
            if (ParseBool(GetProp(element, styles, "IsReadOnly")) is { } isReadOnly)
                textBox.IsReadOnly = isReadOnly;
            return textBox;
        }

        private MyPasswordBox ConvertMyPasswordBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var textBox = new MyPasswordBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? "",
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(textBox, element, styles);
            if (ParseInt(GetProp(element, styles, "MaxLength")) is { } maxLength and > 0)
                textBox.MaxLength = maxLength;
            return textBox;
        }

        private MyComboBoxItem ConvertMyComboBoxItem(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var item = new MyComboBoxItem();
            var content = ResolveString(GetProp(element, styles, "Content"))
                ?? ResolveString(GetProp(element, styles, "Text"))
                ?? (element.HasElements ? null : element.Value.Trim());
            if (!string.IsNullOrEmpty(content))
                item.Content = content;
            else if (element.HasElements)
                item.Content = ConvertSingleChild(element, styleScope);
            ApplyCommon(item, element, styles);
            return item;
        }

        private MyComboBox ConvertMyComboBox(
            XElement element,
            StyleScope styleScope,
            IReadOnlyDictionary<string, string> styles)
        {
            var comboBox = new MyComboBox
            {
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(comboBox, element, styles);
            if (ParseInt(GetProp(element, styles, "SelectedIndex")) is { } selectedIndex)
                comboBox.SelectedIndex = selectedIndex;

            foreach (var child in element.Elements())
            {
                if (IsSkippedElement(child))
                    continue;
                if (child.Name.LocalName is not ("MyComboBoxItem" or "ComboBoxItem"))
                    continue;
                if (ConvertElement(child, styleScope) is { } item)
                    comboBox.Items.Add(item);
            }

            return comboBox;
        }

        private MyCheckBox ConvertMyCheckBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var checkBox = new MyCheckBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(checkBox, element, styles);
            var isChecked = ParseBool(GetProp(element, styles, "IsChecked"))
                ?? ParseBool(GetProp(element, styles, "Checked"));
            if (isChecked is { } checkedValue)
                checkBox.Checked = checkedValue;
            return checkBox;
        }

        private MyRadioBox ConvertMyRadioBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var radioBox = new MyRadioBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(radioBox, element, styles);
            if (ParseBool(GetProp(element, styles, "Checked")) is { } isChecked)
                radioBox.Checked = isChecked;
            return radioBox;
        }

        private MyRadioButton ConvertMyRadioButton(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var radioButton = new MyRadioButton
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? ""
            };
            ApplyCommon(radioButton, element, styles);
            return radioButton;
        }

        private MySlider ConvertMySlider(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var slider = new MySlider();
            ApplyCommon(slider, element, styles);
            if (ParseInt(GetProp(element, styles, "Value")) is { } value)
                slider.Value = value;
            return slider;
        }

        private MySearchBox ConvertMySearchBox(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var searchBox = new MySearchBox
            {
                Text = ResolveString(GetProp(element, styles, "Text")) ?? "",
                HintText = ResolveString(GetProp(element, styles, "HintText"))
                    ?? ResolveString(GetProp(element, styles, "Watermark"))
                    ?? ""
            };
            ApplyCommon(searchBox, element, styles);
            return searchBox;
        }

        private Border ConvertSeparator(
            XElement element,
            IReadOnlyDictionary<string, string> styles)
        {
            var separator = new Border
            {
                Height = 1d,
                Background = ParseBrush(GetProp(element, styles, "Background"))
                    ?? Brushes.Gray
            };
            ApplyCommon(separator, element, styles);
            return separator;
        }
    }
}