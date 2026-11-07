using Avalonia.Controls;
using Avalonia.Layout;
using PCL.Core.UI.Dialogs;
using PCL.UI.Controls;
using PCL.UI.Controls.Common;

namespace PCL.UI.Dialogs;

/// <summary>
///     选择弹窗：普通选项用单选方块，带说明/图标的选项用列表项；多选时用复选框。
///     未选中任何有效选项前主按钮不可用。
/// </summary>
public sealed class MyMsgSelect : MyMsgDialogBase
{
    private readonly List<IMyRadio> _selections = [];

    public MyMsgSelect(DialogViewModel dialog) : base(dialog)
    {
        var list = new StackPanel
        {
            Orientation = Orientation.Vertical,
            Spacing = 2d
        };

        // 同一弹窗内只用一种选项控件：不同控件的互斥范围各自独立，混用会出现多项同时勾选
        var useListItems = dialog.MultiSelect
            || dialog.Options.Any(option =>
                option.Description is not null || option.Image is not null);

        foreach (var state in dialog.Options)
        {
            IMyRadio selection;
            if (useListItems)
            {
                var item = new MyListItem
                {
                    Title = state.Text,
                    Info = state.Description ?? string.Empty,
                    Logo = state.Image ?? string.Empty,
                    LogoScale = state.ImageScale,
                    Checked = state.IsChecked,
                    Type = dialog.MultiSelect
                        ? MyListItem.CheckType.CheckBox
                        : MyListItem.CheckType.RadioBox,
                    Tag = state,
                    MinHeight = 24d
                };
                item.Check += (_, _) => OnChecked(state);
                item.Changed += (_, _) => UpdateCheckedState();
                selection = item;
            }
            else
            {
                var radio = new MyRadioBox
                {
                    Text = state.Text,
                    Tag = state,
                    MinHeight = 24d
                };
                radio.Check += (_, _) => OnChecked(state);
                radio.Changed += (_, _) => UpdateCheckedState();
                selection = radio;
            }

            _selections.Add(selection);
            list.Children.Add((Control)selection);
        }

        SetBody(list);
        Button(1).IsEnabled = false;
        if (dialog.MultiSelect) UpdateCheckedState();
    }

    private void OnChecked(DialogOptionState state)
    {
        if (Dialog.MultiSelect) return;
        Button(1).IsEnabled = true;
        Dialog.Select(state.Index);
    }

    private void UpdateCheckedState()
    {
        if (!Dialog.MultiSelect) return;

        var checkedCount = 0;
        foreach (var selection in _selections)
        {
            if (selection is not MyListItem item) continue;
            if (item.Tag is DialogOptionState state) state.IsChecked = item.Checked;
            if (item.Checked) checkedCount++;
        }

        Button(1).IsEnabled = checkedCount > 0;
    }

    /// <summary>单选要求已选中一项，多选要求至少勾选一项，否则保持弹窗不关闭。</summary>
    protected override void HandleButton(int number)
    {
        switch (number)
        {
            case 1:
                if (Dialog.MultiSelect)
                {
                    UpdateCheckedState();
                    if (Dialog.GetCheckedIndices().Count == 0) return;
                    Dialog.CloseByButton(1);
                    return;
                }

                if (Dialog.SelectedIndex < 0) return;
                Dialog.CloseByButton(1);
                return;
            case 2:
                CloseAsCancelled();
                return;
            default:
                InvokeActionOrClose(number);
                return;
        }
    }
}