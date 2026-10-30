using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PCL.Core.UI.Controls;

using PCL.Core.App.Localization;
namespace PCL;

public partial class MyMsgText
{
    private readonly ModMain.MyMsgBoxConverter myConverter;
    private readonly int uuid = ModBase.GetUuid();

    public MyMsgText(ModMain.MyMsgBoxConverter converter)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            AppendUniqueNameSuffix(Btn3);
            myConverter = converter;
            LabTitle.Text = converter.Title;
            LabCaption.Text = converter.Text;
            ConfigurePrimaryButton(converter.Button1, converter.IsWarn);
            ConfigureSecondaryButton(Btn2, converter.Button2);
            ConfigureSecondaryButton(Btn3, converter.Button3);
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "普通弹窗初始化失败",
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

    private static void ConfigureSecondaryButton(MyButton button, string text)
    {
        button.Text = text;
        button.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Load(object sender, RoutedEventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && !(Btn1.ColorType == MyButton.ColorState.Red))
                Btn1.ColorType = MyButton.ColorState.Highlight;
            Btn1.Focus();
        ModMain.frmMain.PanMsgBackground.Background = myConverter.IsWarn
            ? new ModBase.MyColor(140d, 80d, 0d, 0d)
            : new ModBase.MyColor(90d, 0d, 0d, 0d);
        Opacity = 1d;
        TransformPos.Y = 0d;
        TransformRotate.Angle = 0d;
            // 记录日志
            ModBase.Log("[Control] 普通弹窗：" + LabTitle.Text + "\r\n" + LabCaption.Text);
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "普通弹窗加载失败",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }

    private void Close()
    {
        // 结束线程阻塞
        if (myConverter.ForceWait || !string.IsNullOrEmpty(myConverter.Button2))
            myConverter.WaitFrame.Continue = false;
        myConverter.Completion.TrySetResult(myConverter.Result is int value ? value : null);
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

    public void Btn1_Click(object? sender = null, MouseButtonEventArgs? e = null)
    {
        if (myConverter.IsExited)
            return;
        if (myConverter.Button1Action is not null)
        {
            myConverter.Button1Action();
        }
        else
        {
            myConverter.IsExited = true;
            myConverter.Result = 1;
            Close();
        }
    }

    public void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (myConverter.IsExited)
            return;
        if (myConverter.Button2Action is not null)
        {
            myConverter.Button2Action();
        }
        else
        {
            myConverter.IsExited = true;
            myConverter.Result = 2;
            Close();
        }
    }

    public void Btn3_Click(object sender, MouseButtonEventArgs e)
    {
        if (myConverter.IsExited)
            return;
        if (myConverter.Button3Action is not null)
        {
            myConverter.Button3Action();
        }
        else
        {
            myConverter.IsExited = true;
            myConverter.Result = 3;
            Close();
        }
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
