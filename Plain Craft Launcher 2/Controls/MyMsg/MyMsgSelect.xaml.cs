using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using PCL.Core.UI.Controls;
using PCL.Core.UI.Dialogs;

using PCL.Core.App.Localization;
namespace PCL;

public partial class MyMsgSelect
{
    private readonly DialogViewModel myDialog;
    private readonly int uuid = Common.GetUuid();

    public MyMsgSelect(DialogViewModel dialog)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            myDialog = dialog;
            LabTitle.Text = dialog.Title;
            ConfigurePrimaryButton(dialog.ButtonText(1), dialog.IsWarn);
            ConfigureSecondaryButton(dialog.ButtonText(2));
            ShapeLine.StrokeThickness = Common.GetWpfSize(1d);
            InitializeSelectionList();
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "选择弹窗初始化失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }

        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
        LabTitle.MouseLeftButtonDown += Drag;
        PanBorder.MouseLeftButtonDown += Drag;
    }

    private void AppendUniqueNameSuffix(FrameworkElement element)
    {
        element.Name += Common.GetUuid();
    }

    private void ConfigurePrimaryButton(string text, bool isWarn)
    {
        Btn1.Text = text;
        if (isWarn)
        {
            Btn1.ColorType = MyButton.ColorState.Red;
            LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight");
        }
    }

    private void ConfigureSecondaryButton(string text)
    {
        Btn2.Text = text;
        Btn2.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void InitializeSelectionList()
    {
        // 添加选择控件
        Btn1.IsEnabled = false;
        foreach (var state in myDialog.Options)
        {
            IMyRadio selection;
            if (myDialog.MultiSelect || state.Description is not null || state.Image is not null)
                selection = new MyListItem
                {
                    Title = state.Text,
                    Info = state.Description ?? "",
                    Logo = state.Image ?? "",
                    LogoScale = state.ImageScale,
                    Checked = state.IsChecked,
                    Type = myDialog.MultiSelect ? MyListItem.CheckType.CheckBox : MyListItem.CheckType.RadioBox,
                    Tag = state,
                    MinHeight = 24d
                };
            else
                selection = new MyRadioBox
                {
                    Text = state.Text,
                    Tag = state,
                    MinHeight = 24d
                };
            PanSelection.Children.Add((UIElement)selection);
            selection.Check += (_, _) => OnChecked(state);
            selection.Changed += (_, _) => _UpdateCheckedState();
        }
    }

    private void Load(object sender, EventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && !(Btn1.ColorType == MyButton.ColorState.Red))
                Btn1.ColorType = MyButton.ColorState.Highlight;
            // 动画
            Opacity = 0d;
            Animations.AniStart(
                Animations.AaColor(Launcher.frmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    (myDialog.IsWarn
                        ? new Common.MyColor(140d, 80d, 0d, 0d)
                        : new Common.MyColor(90d, 0d, 0d, 0d)) -
                    Launcher.frmMain.PanMsgBackground.Background, 200),
                "PanMsgBackground Background");
            Animations.AniStart(
                new[]
                {
                    Animations.AaOpacity(this, 1d, 120, 60),
                    Animations.AaDouble(i => TransformPos.Y += (double)i,
                        -TransformPos.Y, 300, 60,
                        new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)),
                    Animations.AaDouble(i => TransformRotate.Angle += (double)i,
                        -TransformRotate.Angle, 300, 60,
                        new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak))
                }, "MyMsgBox " + uuid);
            // 记录日志
            Common.Log("[Control] 选择弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            Common.Log(
                ex,
                "选择弹窗加载失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }

    private void Close()
    {
        // 动画
        Animations.AniStart(new[]
        {
            Animations.AaCode(() =>
            {
                if (!PCL.Core.UI.Dialogs.DialogService.Instance.IsShowing && !PCL.Core.UI.Dialogs.DialogService.Instance.HasPending)
                    Animations.AniStart(Animations.AaColor(Launcher.frmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new Common.MyColor(0d, 0d, 0d, 0d) -
                        Launcher.frmMain.PanMsgBackground.Background, 200,
                        ease: new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak)));
            }, 30),
            Animations.AaOpacity(this, -Opacity, 80, 20),
            Animations.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new Animations.AniEaseOutFluent()),
            Animations.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0,
                new Animations.AniEaseInFluent(Animations.AniEasePower.Weak)),
            Animations.AaCode(() => ((Grid)Parent).Children.Remove(this), after: true)
        }, "MyMsgBox " + uuid);
    }

    public void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
        if (myDialog.IsClosed)
            return;
        if (myDialog.MultiSelect)
        {
            _UpdateCheckedState();
            if (myDialog.GetCheckedIndices() is not { Count: > 0 })
                return;
            myDialog.CloseByButton(1);
        }
        else
        {
            if (myDialog.SelectedIndex < 0)
                return;
            myDialog.CloseByButton(1);
        }
        Close();
    }

    public void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (myDialog.IsClosed)
            return;
        myDialog.Close(DialogResult.Cancel);
        Close();
    }

    private void OnChecked(DialogOptionState state)
    {
        if (myDialog.MultiSelect)
            return;
        Btn1.IsEnabled = true;
        myDialog.Select(state.Index);
    }

    private void _UpdateCheckedState()
    {
        // 多选
        if (!myDialog.MultiSelect)
            return;
        var checkedCount = 0;
        foreach (var child in PanSelection.Children)
        {
            if (child is not MyListItem item)
                continue;
            if (item.Tag is DialogOptionState state)
                state.IsChecked = item.Checked;
            if (item.Checked)
                checkedCount += 1;
        }
        Btn1.IsEnabled = checkedCount > 0;
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                if (e.GetPosition(ShapeLine).Y <= 2d)
                    Launcher.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            Common.Log(
                ex,
                "拖拽移动失败",
                Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }
}
