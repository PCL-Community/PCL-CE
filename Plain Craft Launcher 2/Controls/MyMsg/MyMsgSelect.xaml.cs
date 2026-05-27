using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using PCL.Core.UI.Controls;

namespace PCL;

public partial class MyMsgSelect
{
    private readonly ModMain.MyMsgBoxConverter MyConverter;
    private readonly int Uuid = ModBase.GetUuid();

    private int SelectedIndex = -1;

    public MyMsgSelect(ModMain.MyMsgBoxConverter Converter)
    {
        try
        {
            InitializeComponent();
            AppendUniqueNameSuffix(Btn1);
            AppendUniqueNameSuffix(Btn2);
            MyConverter = Converter;
            LabTitle.Text = Converter.Title;
            ConfigurePrimaryButton(Converter.Button1, Converter.IsWarn);
            ConfigureSecondaryButton(Converter.Button2);
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
            InitializeSelectionList(Converter.Content);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "选择弹窗初始化失败", ModBase.LogLevel.Hint);
        }

        Loaded += Load;
        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
        LabTitle.MouseLeftButtonDown += Drag;
        PanBorder.MouseLeftButtonDown += Drag;
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

    private void InitializeSelectionList(object content)
    {
        // 添加选择控件
        Btn1.IsEnabled = false;
        foreach (var rawContent in (IEnumerable)content)
        {
            // 1. Initialize and get the actual element
            // Note: We use a new variable because 'foreach' variables are read-only
            var selectionContent = MyVirtualizingElement.TryInit((FrameworkElement)rawContent);

            // 2. Interface casting and event subscription
            if (selectionContent is IMyRadio selection)
            {
                PanSelection.Children.Add((UIElement)selection);
                selection.Check += (sender, e) => OnChecked((IMyRadio)sender, e);

                // 3. Property configuration based on specific type
                if (selection is MyListItem listItem)
                {
                    listItem.Type = MyListItem.CheckType.RadioBox;
                    listItem.MinHeight = 24.0;
                }
                else if (selection is MyRadioBox radioBox)
                {
                    radioBox.MinHeight = 24.0;
                }
            }
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
            ModAnimation.AniStart(
                ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    (MyConverter.IsWarn
                        ? new ModBase.MyColor(140d, 80d, 0d, 0d)
                        : new ModBase.MyColor(90d, 0d, 0d, 0d)) - ModMain.FrmMain.PanMsgBackground.Background, 200),
                "PanMsgBackground Background");
            ModAnimation.AniStart(
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
            ModBase.Log("[Control] 选择弹窗：" + LabTitle.Text);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "选择弹窗加载失败", ModBase.LogLevel.Hint);
        }
    }

    private void Close()
    {
        // 结束线程阻塞
        MyConverter.WaitFrame.Continue = false;
        ComponentDispatcher.PopModal();
        // 动画
        ModAnimation.AniStart(new[]
        {
            ModAnimation.AaCode(() =>
            {
                if (!ModMain.WaitingMyMsgBox.Any())
                    ModAnimation.AniStart(ModAnimation.AaColor(ModMain.FrmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.FrmMain.PanMsgBackground.Background, 200,
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
        if (MyConverter.IsExited || SelectedIndex == -1)
            return;
        MyConverter.IsExited = true;
        MyConverter.Result = SelectedIndex;
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

    private void OnChecked(IMyRadio sender, EventArgs e)
    {
        Btn1.IsEnabled = true;
        SelectedIndex = PanSelection.Children.IndexOf((UIElement)sender);
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
