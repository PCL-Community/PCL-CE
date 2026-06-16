using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
            ProgressBar.BeginAnimation(WidthProperty, null);
        };
    }

    public string Title
    {
        get => TitleText.Text;
        set
        {
            TitleText.Text = value;
            TitleText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public string Description
    {
        get => DescText.Text;
        set
        {
            DescText.Text = value;
            DescText.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
        }
    }

    public string Icon { get; set; } = "lucide/info";

    public ModMain.HintType ToastType { get; set; } = ModMain.HintType.Info;

    public double DisplayDuration { get; set; } = 5000;

    public event Action? Dismissed;

    public void Show()
    {
        if (Parent is not Panel panel)
            return;
        Margin = new Thickness(0, 0, 16, 4);
        Opacity = 0;

        Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Arrange(new Rect(0, 0, DesiredSize.Width, DesiredSize.Height));
        var targetHeight = Math.Max(ActualHeight, 45d);
        Height = 0;

        RenderTransform = new TranslateTransform(60, 0);

        var enterAnimations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, -60, 400, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaHeight(this, targetHeight, 150, ease: new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaOpacity(this, 1, 100)
        };
        ModAnimation.AniStart(enterAnimations, $"Toast Show {Uuid}");

        var delay = (int)Math.Round(DisplayDuration);
        var hideAnimations = new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 200, delay, new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 150, delay),
            ModAnimation.AaHeight(this, -targetHeight, 100, ease: new ModAnimation.AniEaseOutFluent(), after: true),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
                Dismissed?.Invoke();
            }, after: true)
        };
        ModAnimation.AniStart(hideAnimations, $"Toast Hide {Uuid}");

        StartProgressAnimation(DisplayDuration);
    }

    public void Dismiss()
    {
        ModAnimation.AniStop($"Toast Show {Uuid}");
        ModAnimation.AniStop($"Toast Hide {Uuid}");
        ProgressBar.BeginAnimation(WidthProperty, null);
        ModAnimation.AniStart(new List<ModAnimation.AniData>
        {
            ModAnimation.AaTranslateX(this, 60, 150, ease: new ModAnimation.AniEaseInFluent()),
            ModAnimation.AaOpacity(this, -1, 100),
            ModAnimation.AaCode(() =>
            {
                if (Parent is Panel p)
                    p.Children.Remove(this);
                Dismissed?.Invoke();
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

    private void UpdateColors()
    {
        var baseHue = ToastType switch
        {
            ModMain.HintType.Finish => 145d,
            ModMain.HintType.Critical => 355d,
            _ => 210d
        };
        var s = ThemeService.CurrentTone;
        var bg = new ModBase.MyColor().FromHSL2(baseHue, 90, s.L7 * 100);
        var fg = new ModBase.MyColor().FromHSL2(baseHue, 90, s.L2 * 100);
        var border = new ModBase.MyColor().FromHSL2(baseHue, 90, s.L4 * 100);

        Root.Background = bg;
        Root.BorderBrush = border;
        TitleText.Foreground = fg;
        DescText.Foreground = fg;
        ProgressBar.Fill = fg;
        BtnClose.Foreground = fg;
        ToastIcon.Icon = Icon;
        ToastIcon.IconBrush = fg;
        ToastIcon.StrokeThickness = 0;
    }
}
