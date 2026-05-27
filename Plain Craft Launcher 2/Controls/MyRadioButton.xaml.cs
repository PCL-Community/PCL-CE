using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App;
using PCL.Core.UI.Theme;

namespace PCL;

[ContentProperty("Inlines")]
public partial class MyRadioButton
{
    public delegate void ChangeEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void CheckEventHandler(MyRadioButton sender, bool raiseByMouse);

    public delegate void PreviewClickEventHandler(object sender, ModBase.RouteEventArgs e);

    public enum ColorState
    {
        White,
        Highlight
    }

    // 动画

    private const int AnimationTimeOfMouseIn = 90; // 鼠标指向动画长度
    private const int AnimationTimeOfMouseOut = 150; // 鼠标移出动画长度
    private const int AnimationTimeOfCheck = 120; // 勾选状态变更动画长度

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyRadioButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is MyRadioButton rb && rb.LabText != null) rb.LabText.Text = (string)e.NewValue;
        }));

    private bool _Checked; // 是否选中
    private ColorState _ColorType = ColorState.White;
    private double _LogoScale = 1d;
    private bool IsMouseDown;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyRadioButton()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            if (LabText != null)
                LabText.Text = (string)GetValue(TextProperty);
            
            ThemeService.ColorModeChanged += OnColorModeChanged;
            ThemeService.ColorThemeChanged += OnColorThemeChanged;
        };

        Unloaded += (_, _) =>
        {
            ThemeService.ColorModeChanged -= OnColorModeChanged;
            ThemeService.ColorThemeChanged -= OnColorThemeChanged;
        };

        MouseLeftButtonUp += (_, _) => Radiobox_MouseUp();
        MouseLeftButtonDown += (_, _) => Radiobox_MouseDown();
        MouseLeave += (_, _) => Radiobox_MouseLeave();
        MouseEnter += RefreshColor;
        MouseLeave += RefreshColor;
        Loaded += RefreshColor;
    }

    private void OnColorModeChanged(bool isDarkMode, ColorTheme theme)
    {
        Dispatcher.Invoke(() => RefreshMyRadioButtonColor());
    }
    private void OnColorThemeChanged(ColorTheme theme)
    {
        Dispatcher.Invoke(() => RefreshMyRadioButtonColor());
    }
    
    // 自定义属性

    public string Logo
    {
        get => ShapeLogo.Data.ToString();
        set
        {
            if (ShapeLogo == null) return;
            ShapeLogo.Data = (Geometry)new GeometryConverter().ConvertFromString(value);
        }
    }

    public double LogoScale
    {
        get => _LogoScale;
        set
        {
            _LogoScale = value;
            if (ShapeLogo != null)
                ShapeLogo.RenderTransform = new ScaleTransform { ScaleX = LogoScale, ScaleY = LogoScale };
        }
    }

    public bool Checked
    {
        get => _Checked;
        set => SetChecked(value, false, true);
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    public ColorState ColorType
    {
        get => _ColorType;
        set
        {
            _ColorType = value;
            RefreshColor();
        }
    } // 颜色类别

    public event CheckEventHandler? Check;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="raiseByMouse">是否由用户引发。</param>
    /// <param name="anime">是否执行动画。</param>
    public void SetChecked(bool value, bool raiseByMouse, bool anime)
    {
        try
        {
            // 自定义属性基础

            var IsChanged = false;
            if (_Checked != value)
            {
                _Checked = value;
                IsChanged = true;
            }

            // 保证只有一个单选框选中

            if (Parent == null) return;
            var RadioboxList = new List<MyRadioButton>();
            var CheckedCount = 0;
            // 收集控件列表与选中个数
            foreach (var Control in ((Panel)Parent).Children)
                if (Control is MyRadioButton radioButton)
                {
                    RadioboxList.Add(radioButton);
                    if (radioButton.Checked)
                        CheckedCount += 1;
                }

            // 判断选中情况
            switch (CheckedCount)
            {
                case 0:
                {
                    // 没有任何单选框被选中，选择第一个
                    RadioboxList[0].Checked = true;
                    break;
                }
                case var @case when @case > 1:
                {
                    // 选中项目多于 1 个
                    if (Checked)
                    {
                        // 如果本控件选中，则取消其他所有控件的选中
                        foreach (var Control in RadioboxList)
                            if (Control.Checked && !Control.Equals(this))
                                Control.Checked = false;
                    }
                    else
                    {
                        // 如果本控件未选中，则只保留第一个选中的控件
                        var FirstChecked = false;
                        foreach (var Control in RadioboxList)
                            if (Control.Checked)
                            {
                                if (FirstChecked)
                                    Control.Checked = false; // 修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                else
                                    FirstChecked = true;
                            }
                    }

                    break;
                }
            }

            // 更改动画

            if (!IsChanged)
                return;
            RefreshColor(null, anime);

            // 触发事件
            if (Checked)
                Check?.Invoke(this, raiseByMouse);
            ModMain.RaiseCustomEvent(this);
        }

        catch (Exception ex)
        {
            ModBase.Log(ex, "单选按钮勾选改变错误", ModBase.LogLevel.Hint);
        }
    }

    // 点击事件

    public event PreviewClickEventHandler? PreviewClick;

    private void Radiobox_MouseUp()
    {
        if (Checked)
            return;
        if (!IsMouseDown)
            return;
        ModBase.Log("[Control] 按下单选按钮：" + Text);
        IsMouseDown = false;
        var e = new ModBase.RouteEventArgs(true);
        PreviewClick?.Invoke(this, e);
        if (e.Handled)
            return;
        SetChecked(true, true, true);
    }

    private void Radiobox_MouseDown()
    {
        if (Checked)
            return;
        IsMouseDown = true;
        RefreshColor();
    }

    private void Radiobox_MouseLeave()
    {
        IsMouseDown = false;
    }

    private void RefreshColor(object obj = null, object e = null)
    {
        try
        {
            if (IsLoaded && ModAnimation.AniControlEnabled == 0 &&
                !false.Equals(e)) // 防止默认属性变更触发动画，若强制不执行动画，则 e 为 False
            {
                switch (ColorType)
                {
                    case ColorState.White:
                    {
                        if (Checked)
                        {
                            // 勾选
                            var color3 = new ModBase.MyColor(ThemeManager.AppResources["ColorObject3"]);
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, color3 - ShapeLogo.Fill,
                                        AnimationTimeOfCheck),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                                        color3 - LabText.Foreground, AnimationTimeOfCheck)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new ModBase.MyColor(255d, 255d, 255d) - Background, AnimationTimeOfCheck),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsMouseDown)
                        {
                            // 按下
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new ModBase.MyColor(120d,
                                        new ModBase.MyColor(ThemeManager.AppResources["ColorObject8"])) - Background, 60),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsMouseOver)
                        {
                            // 指向
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty,
                                        new ModBase.MyColor(255d, 255d, 255d) - ShapeLogo.Fill, AnimationTimeOfMouseIn),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                                        new ModBase.MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        AnimationTimeOfMouseIn)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new ModBase.MyColor(50d,
                                        new ModBase.MyColor(ThemeManager.AppResources["ColorObject8"])) - Background,
                                    AnimationTimeOfMouseIn), "MyRadioButton Color " + Uuid);
                        }
                        else
                        {
                            // 正常
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty,
                                        new ModBase.MyColor(255d, 255d, 255d) - ShapeLogo.Fill,
                                        AnimationTimeOfMouseOut),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                                        new ModBase.MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        AnimationTimeOfMouseOut)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new ModBase.MyColor(ThemeManager.AppResources["ColorBrushSemiTransparent"]) -
                                    Background, AnimationTimeOfMouseOut), "MyRadioButton Color " + Uuid);
                        }

                        break;
                    }
                    case ColorState.Highlight:
                    {
                        if (Checked)
                        {
                            // 勾选
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty,
                                        new ModBase.MyColor(255d, 255d, 255d) - ShapeLogo.Fill, AnimationTimeOfCheck),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty,
                                        new ModBase.MyColor(255d, 255d, 255d) - LabText.Foreground,
                                        AnimationTimeOfCheck)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush3", AnimationTimeOfCheck),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsMouseDown)
                        {
                            // 按下
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush6", AnimationTimeOfMouseIn),
                                "MyRadioButton Color " + Uuid);
                        }
                        else if (IsMouseOver)
                        {
                            // 指向
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        AnimationTimeOfMouseIn),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3",
                                        AnimationTimeOfMouseIn)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty, "ColorBrush7", AnimationTimeOfMouseIn),
                                "MyRadioButton Color " + Uuid);
                        }
                        else
                        {
                            // 正常
                            ModAnimation.AniStart(
                                new[]
                                {
                                    ModAnimation.AaColor(ShapeLogo, Shape.FillProperty, "ColorBrush3",
                                        AnimationTimeOfMouseOut),
                                    ModAnimation.AaColor(LabText, TextBlock.ForegroundProperty, "ColorBrush3",
                                        AnimationTimeOfMouseOut)
                                }, "MyRadioButton Checked " + Uuid);
                            ModAnimation.AniStart(
                                ModAnimation.AaColor(this, BackgroundProperty,
                                    new ModBase.MyColor(ThemeManager.AppResources["ColorBrushSemiTransparent"]) -
                                    Background, AnimationTimeOfMouseOut), "MyRadioButton Color " + Uuid);
                        }

                        break;
                    }
                }
            }

            else
            {
                // 不使用动画
                ModAnimation.AniStop("MyRadioButton Checked " + Uuid);
                ModAnimation.AniStop("MyRadioButton Color " + Uuid);
                switch (ColorType)
                {
                    case ColorState.White:
                    {
                        if (Checked)
                        {
                            Background = new ModBase.MyColor(255d, 255d, 255d);
                            ShapeLogo.SetResourceReference(Shape.FillProperty, "ColorBrush3");
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
                        }
                        else
                        {
                            Background = (Brush)ThemeManager.AppResources["ColorBrushSemiTransparent"];
                            ShapeLogo.Fill = new ModBase.MyColor(255d, 255d, 255d);
                            LabText.Foreground = new ModBase.MyColor(255d, 255d, 255d);
                        }

                        break;
                    }
                    case ColorState.Highlight:
                    {
                        if (Checked)
                        {
                            SetResourceReference(BackgroundProperty, "ColorBrush3");
                            ShapeLogo.Fill = new ModBase.MyColor(255d, 255d, 255d);
                            LabText.Foreground = new ModBase.MyColor(255d, 255d, 255d);
                        }
                        else
                        {
                            Background = (Brush)ThemeManager.AppResources["ColorBrushSemiTransparent"];
                            ShapeLogo.SetResourceReference(Shape.FillProperty, "ColorBrush3");
                            LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
                        }

                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "刷新单选按钮颜色出错");
        }
    }

    public void RefreshMyRadioButtonColor()
    {
        RefreshColor();
    }
}