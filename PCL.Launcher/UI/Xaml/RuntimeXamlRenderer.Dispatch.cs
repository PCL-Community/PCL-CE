using System.Xml.Linq;
using Avalonia.Controls;

namespace PCL.UI.Xaml;

public static partial class RuntimeXamlRenderer
{
    private sealed partial class RenderContext
    {
        public Control? ConvertElement(XElement element, StyleScope? inheritedStyles)
        {
            var elementName = element.Name.LocalName;

            if (elementName.Contains('.'))
                return null;

            if (IgnoredElementNames.Contains(elementName))
                return null;

            var styleScope = new StyleScope(inheritedStyles);
            if (options.ApplyLocalStyles)
                CollectStyles(element, styleScope);

            var styles = styleScope.Resolve(elementName, element);
            var control = elementName switch
            {
                "StackPanel" => ConvertStackPanel(element, styleScope, styles),
                "WrapPanel" => ConvertWrapPanel(element, styleScope, styles),
                "DockPanel" => ConvertDockPanel(element, styleScope, styles),
                "Canvas" => ConvertCanvas(element, styleScope, styles),
                "Grid" => ConvertGrid(element, styleScope, styles),
                "Border" => ConvertBorder(element, styleScope, styles),
                "Viewbox" => ConvertViewbox(element, styleScope, styles),
                "ScrollViewer" => ConvertScrollViewer(element, styleScope, styles),
                "MyScrollViewer" => ConvertMyScrollViewer(element, styleScope, styles),
                "ContentControl" => ConvertContentControl(element, styleScope, styles),
                "TextBlock" => ConvertTextBlock(element, styles),
                "Run" or "Span" or "Bold" or "Italic" or "Hyperlink" =>
                    ConvertInlineTextBlock(element, styles),
                "LineBreak" => new TextBlock
                {
                    Text = "\n"
                },
                "Image" or "MyImage" => ConvertImage(element, styles),
                "Rectangle" or "Ellipse" or "Line" => ConvertShape(element, styles),
                "Path" => ConvertPath(element, styles),
                "FlowDocumentScrollViewer" =>
                    ConvertFlowDocumentScrollViewer(element, styleScope, styles),
                "FlowDocument" => ConvertFlowDocument(element, styleScope, styles),
                "Paragraph" or "Section" => ConvertParagraph(element, styleScope, styles),
                "List" => ConvertDocumentList(element, styleScope, styles),
                "ListItem" => ConvertDocumentListItem(element, styleScope, styles),
                "TextBox" => ConvertWpfTextBox(element, styleScope, styles),
                "Button" or "MyButton" => ConvertMyButton(element, styles),
                "MyTextButton" => ConvertMyTextButton(element, styles),
                "MyIconButton" => ConvertMyIconButton(element, styles),
                "MyIconTextButton" => ConvertMyIconTextButton(element, styles),
                "MyExtraButton" => ConvertMyExtraButton(element, styles),
                "MyExtraTextButton" => ConvertMyExtraTextButton(element, styles),
                "MyCollapseBar" => ConvertMyCollapseBar(element, styleScope, styles),
                "MyCard" => ConvertMyCard(element, styleScope, styles),
                "MyHint" => ConvertMyHint(element, styleScope, styles),
                "MyListItem" => ConvertMyListItem(element, styles),
                "MyLoading" => ConvertMyLoading(element, styles),
                "MyTextBox" => ConvertMyTextBox(element, styles),
                "MyPasswordBox" => ConvertMyPasswordBox(element, styles),
                "MyComboBox" => ConvertMyComboBox(element, styleScope, styles),
                "MyComboBoxItem" or "ComboBoxItem" =>
                    ConvertMyComboBoxItem(element, styleScope, styles),
                "MyCheckBox" => ConvertMyCheckBox(element, styles),
                "MyRadioBox" => ConvertMyRadioBox(element, styles),
                "MyRadioButton" => ConvertMyRadioButton(element, styles),
                "MySlider" => ConvertMySlider(element, styles),
                "MySearchBox" => ConvertMySearchBox(element, styles),
                "Separator" => ConvertSeparator(element, styles),
                _ => null
            };

            if (control is null
                && !elementName.Contains('.')
                && !IgnoredElementNames.Contains(elementName))
                result.Warnings.Add($"不支持的元素：{elementName}");

            return control is not null && _rejectedControls.Remove(control)
                ? null
                : control;
        }
    }
}