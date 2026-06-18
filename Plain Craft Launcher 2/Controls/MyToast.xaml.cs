using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using PCL.Core.UI.Theme;

namespace PCL;

public partial class MyToast
{
    public int Uuid = ModBase.GetUuid();

    public MyToast()
    {
        InitializeComponent();
        BtnClose.Click += (_, _) => Dismiss();
        Loaded += (_, _) => UpdateColors();
        Unloaded += (_, _) =>
        {
            ModAnimation.AniStop($"Toast Show {Uuid}");
            ModAnimation.AniStop($"Toast Hide {Uuid}");
            ModAnimation.AniStop($"Toast Dismiss {Uuid}");
            ModAnimation.AniStop($"Toast Emphasize {Uuid}");
            ProgressBar.BeginAnimation(WidthProperty, null);
        };
    }

    public string Context
    {
        get => TitleText.Text;
        set => TitleText.Text = value;
    }

    public string Icon { get; set; } = "lucide/info";

    public HintType ToastType { get; set; } = HintType.Info;

    public double DisplayDuration { get; set; } = 5000;

    public bool IsDismissing { get; private set; }

    private double _targetHeight;

    public void Show()
    {
        if (Parent is not Panel)
            return;
        if (System.Windows.Application.Current.MainWindow is not null)
            MaxWidth = System.Windows.Application.Current.MainWindow.ActualWidth * 0.9;
        Margin = new Thickness(0, 0, 16, 4);
        Opacity = 0;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));
        _targetHeight = Math.Max(ActualHeight, 45d);
        Height = 0;

        RenderTransform = new TranslateTransform(60, 0);

        var enterAnimations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -60, 400, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaHeight(this, _targetHeight, 150, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this, 1, 100)
        };
        ModAnimation.AniStart(enterAnimations, $"Toast Show {Uuid}");

        RestartHideAnimation();
    }

    public void Emphasize()
    {
        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ProgressBar.BeginAnimation(WidthProperty, null);
        if (RenderTransform is TranslateTransform tt) tt.X = 0;
        Opacity = 1;
        Height = _targetHeight;
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -8, 70, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaTranslateX(this, 16, 70, after: true),
            ModAnimation.AaTranslateX(this, -8, 60, after: true, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaCode(RestartHideAnimation, after: true),
        }, $"Toast Emphasize {Uuid}");
    }

    private void RestartHideAnimation()
    {
        var delay = (int)Math.Round(DisplayDuration);
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 200, delay, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 150, delay),
            ModAnimation.AaHeight(this, -_targetHeight, 100, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
            }, after: true)
        }, $"Toast Hide {Uuid}");
        StartProgressAnimation(DisplayDuration);
    }

    public void Dismiss()
    {
        if (IsDismissing) return;
        IsDismissing = true;
        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ModAnimation.AniStop($"Toast Emphasize {Uuid}");
        ProgressBar.BeginAnimation(WidthProperty, null);
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 150, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 100),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
            }, after: true)
        }, $"Toast Dismiss {Uuid}");
    }

    private void StartProgressAnimation(double duration)
    {
        var totalMs = (int)Math.Round(duration);
        if (totalMs <= 0)
            return;
        var w = ProgressBar.ActualWidth;
        if (w <= 0) w = 300;
        ProgressBar.HorizontalAlignment = HorizontalAlignment.Left;
        ProgressBar.Width = w;
        var anim = new DoubleAnimation(w, 0d, TimeSpan.FromMilliseconds(totalMs));
        ProgressBar.BeginAnimation(WidthProperty, anim);
    }

    private void RootGrid_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        RootGrid.Clip = new RectangleGeometry(new Rect(0, 0, RootGrid.ActualWidth, RootGrid.ActualHeight), 8, 8);
    }

    private void UpdateColors()
    {
        var baseHue = ToastType switch
        {
            HintType.Success => 145d,
            HintType.Error => 355d,
            HintType.Warning => 40d,
            _ => 210d
        };
        var res = System.Windows.Application.Current.Resources;
        var accent = new ModBase.MyColor().FromHSL2(baseHue, 75, 60);
        var bg = ThemeService.IsDarkMode
            ? new SolidColorBrush(LabColor.FromLch(0.35))
            : (Brush)res["ColorBrushBackground"];
        var text = (SolidColorBrush)res["ColorBrushGray1"];
        var accentBrush = new SolidColorBrush(accent);

        Root.Background = bg;
        Root.BorderBrush = bg;
        TitleText.Foreground = text;
        ProgressBar.Fill = accentBrush;
        BtnClose.Foreground = text;
        ToastIcon.Icon = Icon;
        ToastIcon.IconBrush = accentBrush;
        ToastIcon.StrokeThickness = 0;
    }
}
