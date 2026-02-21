using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;

namespace PCL;

public partial class MyMsgMarkdown
{
    private readonly ModMain.MyMsgBoxConverter MyConverter;
    private readonly int Uuid = ModBase.GetUuid();

    public MyMsgMarkdown(ModMain.MyMsgBoxConverter Converter)
    {
        try
        {
            InitializeComponent();
            Btn1.Name = Btn1.Name + ModBase.GetUuid();
            Btn2.Name = Btn2.Name + ModBase.GetUuid();
            Btn3.Name = Btn3.Name + ModBase.GetUuid();
            MyConverter = Converter;
            LabTitle.Text = Converter.Title;
            LabCaption.Markdown = Converter.Text;
            DataContext = this;
            Btn1.Text = Converter.Button1;
            if (Converter.IsWarn)
            {
                Btn1.ColorType = MyButton.ColorState.Red;
                LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight");
            }

            Btn2.Text = Converter.Button2;
            Btn3.Text = Converter.Button3;
            Btn2.Visibility = string.IsNullOrEmpty(Converter.Button2) ? Visibility.Collapsed : Visibility.Visible;
            Btn3.Visibility = string.IsNullOrEmpty(Converter.Button3) ? Visibility.Collapsed : Visibility.Visible;
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "普通弹窗初始化失败", ModBase.LogLevel.Hint);
        }

        Loaded += Load;
    }

    private void Load(object sender, EventArgs e)
    {
        try
        {
            // UI 初始化
            if (Btn2.IsVisible && !(Btn1.ColorType == MyButton.ColorState.Red))
                Btn1.ColorType = MyButton.ColorState.Highlight;
            Btn1.Focus();
            // 动画
            Opacity = 0d;
            AniStart(
                AaColor(ModMain.FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    (MyConverter.IsWarn
                        ? new ModBase.MyColor(140d, 80d, 0d, 0d)
                        : new ModBase.MyColor(90d, 0d, 0d, 0d)) - ModMain.FrmMain.PanMsgBackground.Background, 200),
                "PanMsgBackground Background");
            ModAnimation.AniStart(
                new[]
                {
                    ModAnimation.AaOpacity(this, 1d, 120, 60),
                    ModAnimation.AaDouble(i => TransformPos.Y = Conversions.ToDouble(TransformPos.Y + i),
                        -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle = Conversions.ToDouble(TransformRotate.Angle + i),
                        -TransformRotate.Angle, 300, 60,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "MyMsgBox " + Uuid);
            // 记录日志
            ModBase.Log("[Control] 普通弹窗：" + LabTitle.Text + Constants.vbCrLf + LabCaption.Markdown);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "普通弹窗加载失败", ModBase.LogLevel.Hint);
        }
    }

    private void Close()
    {
        // 结束线程阻塞
        if (MyConverter.ForceWait || !string.IsNullOrEmpty(MyConverter.Button2))
            MyConverter.WaitFrame.Continue = false;
        ComponentDispatcher.PopModal();
        // 动画
        AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (!ModMain.WaitingMyMsgBox.Any())
                    AniStart(AaColor(ModMain.FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                        new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsgBackground.Background, 200,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y = Conversions.ToDouble(TransformPos.Y + i), 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle = Conversions.ToDouble(TransformRotate.Angle + i),
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => ((Grid)Parent).Children.Remove(this), After: true)
        }, "MyMsgBox " + Uuid);
    }

    public void Btn1_Click()
    {
        if (MyConverter.IsExited)
            return;
        if (MyConverter.Button1Action is not null)
        {
            MyConverter.Button1Action();
        }
        else
        {
            MyConverter.IsExited = true;
            MyConverter.Result = 1;
            Close();
        }
    }

    public void Btn2_Click()
    {
        if (MyConverter.IsExited)
            return;
        if (MyConverter.Button2Action is not null)
        {
            MyConverter.Button2Action();
        }
        else
        {
            MyConverter.IsExited = true;
            MyConverter.Result = 2;
            Close();
        }
    }

    public void Btn3_Click()
    {
        if (MyConverter.IsExited)
            return;
        if (MyConverter.Button3Action is not null)
        {
            MyConverter.Button3Action();
        }
        else
        {
            MyConverter.IsExited = true;
            MyConverter.Result = 3;
            Close();
        }
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                if (e.GetPosition(ShapeLine).Y <= 2d)
                    ModMain.FrmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint);
        }
    }
}