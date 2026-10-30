using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PCL.Core.UI.Controls;

using PCL.Core.App.Localization;
namespace PCL;

public partial class MyMsgInput
{
    private readonly ModMain.MyMsgBoxConverter myConverter;
    private readonly int uuid = ModBase.GetUuid();

    public MyMsgInput(ModMain.MyMsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            myConverter = converter;
            LabTitle.Text = converter.Title;
            LabText.Text = converter.Text;
            PanText.Visibility = string.IsNullOrEmpty(converter.Text) ? Visibility.Collapsed : Visibility.Visible;
            TextArea.Text = (string)converter.Content;
            TextArea.HintText = converter.HintText;
            TextArea.ValidateRules = converter.ValidateRules;
            ConfigurePrimaryButton(converter.Button1, converter.IsWarn);
            ConfigureSecondaryButton(converter.Button2);
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "输入弹窗初始化失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }

        Loaded += Load;
    }

    private void AppendUniqueNameSuffix(FrameworkElement element)
    {
        element.Name += ModBase.GetUuid();
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

    private void Load(object sender, EventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && !(Btn1.ColorType == MyButton.ColorState.Red))
                Btn1.ColorType = MyButton.ColorState.Highlight;
            TextArea.Focus();
            TextArea.SelectionStart = TextArea.Text.Length;
        ModMain.frmMain.PanMsgBackground.Background = myConverter.IsWarn
            ? new ModBase.MyColor(140d, 80d, 0d, 0d)
            : new ModBase.MyColor(90d, 0d, 0d, 0d);
        Opacity = 1d;
        TransformPos.Y = 0d;
        TransformRotate.Angle = 0d;
            // 记录日志
            ModBase.Log("[Control] 输入弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "输入弹窗加载失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }

    private void Close()
    {
        // 结束线程阻塞
        myConverter.WaitFrame.Continue = false;
        myConverter.CompletionObject.TrySetResult(myConverter.Result);
        ComponentDispatcher.PopModal();
        // 隐藏
        ModBase.RunInUiLater(() =>
        {
            if (!ModMain.WaitingMyMsgBox.Any())
                ModMain.frmMain.PanMsgBackground.Background = new ModBase.MyColor(0d, 0d, 0d, 0d);
        }, 30);
        if (Parent is Grid parentGrid)
            parentGrid.Children.Remove(this);
    }

    public void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
        TextArea.Validate(); // #5773
        if (myConverter.IsExited || !TextArea.IsValidated)
            return;
        myConverter.IsExited = true;
        myConverter.Result = TextArea.Text;
        Close();
    }

    public void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (myConverter.IsExited)
            return;
        myConverter.IsExited = true;
        myConverter.Result = null;
        Close();
    }

    private void TextCaption_ValidateChanged(object sender, EventArgs e)
    {
        Btn1.IsEnabled = TextArea.IsValidated;
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                if (e.GetPosition(ShapeLine).Y <= 2d)
                    ModMain.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "拖拽移动失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }
}
