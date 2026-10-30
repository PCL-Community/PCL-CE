using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;
using PCL.Core.App;
using PCL.Core.UI.Theme;

using PCL.Core.App.Localization;
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

    public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string),
        typeof(MyRadioButton), new PropertyMetadata((sender, e) =>
        {
            if (sender is MyRadioButton rb && rb.LabText is not null) rb.LabText.Text = (string)e.NewValue;
        }));

    private bool _Checked; // 是否选中
    private bool _hasLegacyLogo;
    private bool isMouseDown;

    // 基础

    public int Uuid = ModBase.GetUuid();

    public MyRadioButton()
    {
        InitializeComponent();
        RefreshLogoHostVisibility();

        Loaded += (_, _) =>
        {
            LabText?.Text = (string)GetValue(TextProperty);

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
        Dispatcher.Invoke(RefreshMyRadioButtonColor);
    }

    private void OnColorThemeChanged(ColorTheme theme)
    {
        Dispatcher.Invoke(RefreshMyRadioButtonColor);
    }

    // 自定义属性

    public string Logo
    {
        get => ShapeLogo.Data?.ToString() ?? string.Empty;
        set
        {
            if (ShapeLogo is null) return;
            _hasLegacyLogo = !string.IsNullOrWhiteSpace(value);
            ShapeLogo.Data = _hasLegacyLogo
                ? (Geometry)new GeometryConverter().ConvertFromString(value)
                : null;
            SvgIconControlHelper.ApplyVisibility(ShapeLogo, ShapeSvgIcon, IsUsingSvgIcon);
            RefreshLogoHostVisibility();
        }
    }

    public string SvgIcon
    {
        get;
        set
        {
            value ??= string.Empty;
            if (value == field)
                return;
            field = value;
            if (ShapeLogo is null || ShapeSvgIcon is null)
                return;
            SvgIconControlHelper.ApplyIcon(ShapeLogo, ShapeSvgIcon, field);
            ApplyLogoScale();
            RefreshLogoHostVisibility();
            RefreshColor();
        }
    } = string.Empty;

    private bool IsUsingSvgIcon => SvgIconControlHelper.HasSvgIcon(SvgIcon);

    private double EffectiveLogoScale => IsUsingSvgIcon ? 1D : LogoScale;

    private bool HasAnyIcon => IsUsingSvgIcon || _hasLegacyLogo;

    private void ApplyLogoScale()
    {
        LogoHost?.RenderTransform = new ScaleTransform
        {
            ScaleX = EffectiveLogoScale,
            ScaleY = EffectiveLogoScale
        };
    }

    private void RefreshLogoHostVisibility()
    {
        if (LogoHost is null || LabText is null)
            return;

        if (HasAnyIcon)
        {
            LogoHost.Visibility = Visibility.Visible;
            LogoHost.Width = 16;
        }
        else
        {
            LogoHost.Visibility = Visibility.Visible;
            LogoHost.Width = 0;
        }

        LogoHost.Height = 16;
        LogoHost.Margin = new Thickness(12, 0, 0, 0);
        LabText.Margin = new Thickness(8, 0, 12, 0);
    }

    public double LogoScale
    {
        get;
        set
        {
            field = value;
            ApplyLogoScale();
        }
    } = 1d;

    public bool Checked
    {
        get => _Checked;
        set => SetChecked(value, false);
    }

    public InlineCollection Inlines => LabText.Inlines;

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    } // 内容

    public ColorState ColorType
    {
        get;
        set
        {
            field = value;
            RefreshColor();
        }
    } = ColorState.White; // 颜色类别

    public event CheckEventHandler? Check;

    /// <summary>
    ///     手动设置 Checked 属性。
    /// </summary>
    /// <param name="value">新的 Checked 属性。</param>
    /// <param name="raiseByMouse">是否由用户引发。</param>
    public void SetChecked(bool value, bool raiseByMouse)
    {
        try
        {
            // 自定义属性基础

            var isChanged = false;
            if (_Checked != value)
            {
                _Checked = value;
                isChanged = true;
            }

            // 保证只有一个单选框选中

            if (Parent is null) return;
            var radioboxList = new List<MyRadioButton>();
            var checkedCount = 0;
            // 收集控件列表与选中个数
            foreach (var Control in ((Panel)Parent).Children)
                if (Control is MyRadioButton radioButton)
                {
                    radioboxList.Add(radioButton);
                    if (radioButton.Checked)
                        checkedCount += 1;
                }

            // 判断选中情况
            switch (checkedCount)
            {
                case 0:
                {
                    // 没有任何单选框被选中，选择第一个
                    radioboxList[0].Checked = true;
                    break;
                }
                case var @case when @case > 1:
                {
                    // 选中项目多于 1 个
                    if (Checked)
                    {
                        // 如果本控件选中，则取消其他所有控件的选中
                        foreach (var Control in radioboxList)
                            if (Control.Checked && !Control.Equals(this))
                                Control.Checked = false;
                    }
                    else
                    {
                        // 如果本控件未选中，则只保留第一个选中的控件
                        var firstChecked = false;
                        foreach (var Control in radioboxList)
                            if (Control.Checked)
                            {
                                if (firstChecked)
                                    Control.Checked = false; // 修改 Checked 会自动触发 Change 事件，所以不用额外触发
                                else
                                    firstChecked = true;
                            }
                    }

                    break;
                }
            }

            // 更改显示

            if (!isChanged)
                return;
            RefreshColor();

            // 触发事件
            if (Checked)
                Check?.Invoke(this, raiseByMouse);
            ModMain.RaiseCustomEvent(this);
        }

        catch (Exception ex)
        {
            ModBase.Log(
                ex,
                "单选按钮勾选改变错误",
                ModBase.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.Error.OperationFailed"));
        }
    }

    // 点击事件

    public event PreviewClickEventHandler? PreviewClick;

    private void Radiobox_MouseUp()
    {
        if (Checked)
            return;
        if (!isMouseDown)
            return;
        ModBase.Log("[Control] 按下单选按钮：" + Text);
        isMouseDown = false;
        var e = new ModBase.RouteEventArgs(true);
        PreviewClick?.Invoke(this, e);
        if (e.handled)
            return;
        SetChecked(true, true);
    }

    private void Radiobox_MouseDown()
    {
        if (Checked)
            return;
        isMouseDown = true;
        RefreshColor();
    }

    private void Radiobox_MouseLeave()
    {
        isMouseDown = false;
    }

    private void RefreshColor(object obj = null, object e = null)
    {
        try
        {
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
                        if (isMouseDown)
                            Background = new ModBase.MyColor(120d,
                                new ModBase.MyColor(ThemeManager.AppResources["ColorObject8"]));
                        else if (IsMouseOver)
                            Background = new ModBase.MyColor(50d,
                                new ModBase.MyColor(ThemeManager.AppResources["ColorObject8"]));
                        else
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
                        if (isMouseDown)
                            SetResourceReference(BackgroundProperty, "ColorBrush6");
                        else if (IsMouseOver)
                            SetResourceReference(BackgroundProperty, "ColorBrush7");
                        else
                            Background = (Brush)ThemeManager.AppResources["ColorBrushSemiTransparent"];
                        ShapeLogo.SetResourceReference(Shape.FillProperty, "ColorBrush3");
                        LabText.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrush3");
                    }

                    break;
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