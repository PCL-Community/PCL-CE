using PCL.Core.App;
using PCL.Core.UI;
using PCL.Core.UI.Controls;
using PCL.Core.Utils;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace PCL;

public partial class MyMsgInput
{
    private readonly ModMain.MyMsgBoxConverter MyConverter;
    private readonly ulong Uuid = GlobalUniqueId.GetUniqueId();

    public MyMsgInput(ModMain.MyMsgBoxConverter Converter)
    {
        try
        {
            InitializeComponent();
            Btn1.Name = Btn1.Name + GlobalUniqueId.GetUniqueId();
            Btn2.Name = Btn2.Name + GlobalUniqueId.GetUniqueId();
            MyConverter = Converter;
            LabTitle.Text = Converter.Title;
            LabText.Text = Converter.Text;
            PanText.Visibility = string.IsNullOrEmpty(Converter.Text) ? Visibility.Collapsed : Visibility.Visible;
            TextArea.Text = (string)Converter.Content;
            TextArea.HintText = Converter.HintText;
            TextArea.ValidateRules = Converter.ValidateRules;
            Btn1.Text = Converter.Button1;
            if (Converter.IsWarn)
            {
                Btn1.ColorType = MyButton.ColorState.Red;
                LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight");
            }

            Btn2.Text = Converter.Button2;
            Btn2.Visibility = string.IsNullOrEmpty(Converter.Button2) ? Visibility.Collapsed : Visibility.Visible;
            ShapeLine.StrokeThickness = ModBase.GetWpfSize(1d);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "输入弹窗初始化失败", ModBase.LogType.Hint);
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
            TextArea.Focus();
            TextArea.SelectionStart = TextArea.Text.Length;
            // 动画
            Opacity = 0d;
            ModAnimation.Start(
                ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    (MyConverter.IsWarn
                        ? new NColor(140, 80, 0, 0)
                        : new NColor(new SolidColorBrush(Color.FromArgb(90, 0, 0, 0)).Subtract((SolidColorBrush)ModMain.FrmMain.PanMsgBackground.Background))),
                    200),
                "PanMsgBackground Background");
            ModAnimation.Start(
                new[]
                {
                    ModAnimation.AaOpacity(this, 1d, 120, 60),
                    ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                        -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                    ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                        -TransformRotate.Angle, 300, 60,
                        new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
                }, "MyMsgBox " + Uuid);
            // 记录日志
            ModBase.Log("[Control] 输入弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "输入弹窗加载失败", ModBase.LogType.Hint);
        }
    }

    private void Close()
    {
        // 结束线程阻塞
        MyConverter.WaitFrame.Continue = false;
        ComponentDispatcher.PopModal();
        // 动画
        ModAnimation.Start(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (!ModMain.WaitingMyMsgBox.Any())
                    ModAnimation.Start(ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new NColor(0, 0, 0, 0) - ModMain.FrmMain.PanMsgBackground.Background, 200,
                        Ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => ((Grid)Parent).Children.Remove(this), After: true)
        }, "MyMsgBox " + Uuid);
    }

    public void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
        TextArea.Validate(); // #5773
        if (MyConverter.IsExited || !TextArea.IsValidated)
            return;
        MyConverter.IsExited = true;
        MyConverter.Result = TextArea.Text;
        Close();
    }

    public void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (MyConverter.IsExited)
            return;
        MyConverter.IsExited = true;
        MyConverter.Result = null;
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
                    ModMain.FrmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogType.Hint);
        }
    }
}