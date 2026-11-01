using System.Collections.Generic;
using PCL.Core.UI.Dialogs;

namespace PCL;

/// <summary>把界面控件转换为数据化的弹窗选项。</summary>
public static class DialogOptions
{
    /// <summary>转换一组可勾选控件（单选框、列表项）。</summary>
    public static List<DialogOption> From(IEnumerable<IMyRadio> radios)
    {
        var options = new List<DialogOption>();
        foreach (var radio in radios)
        {
            switch (radio)
            {
                case MyListItem item:
                    options.Add(new DialogOption
                    {
                        Text = item.Title,
                        Description = item.Info,
                        Image = item.Logo,
                        ImageScale = item.LogoScale,
                        IsChecked = item.Checked,
                        Tag = item
                    });
                    break;
                case MyRadioBox box:
                    options.Add(new DialogOption
                    {
                        Text = box.Text,
                        IsChecked = box.Checked,
                        Tag = box
                    });
                    break;
                default:
                    options.Add(new DialogOption { Text = radio.ToString() ?? "", Tag = radio });
                    break;
            }
        }

        return options;
    }

    /// <summary>转换一组列表项。</summary>
    public static List<DialogOption> From(IEnumerable<MyListItem> items)
    {
        var options = new List<DialogOption>();
        foreach (var item in items)
            options.Add(new DialogOption
            {
                Text = item.Title,
                Description = item.Info,
                Image = item.Logo,
                ImageScale = item.LogoScale,
                IsChecked = item.Checked,
                Tag = item
            });
        return options;
    }
}
