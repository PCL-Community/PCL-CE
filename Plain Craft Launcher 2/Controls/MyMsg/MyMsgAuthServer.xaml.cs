using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PCL.Core.App.Localization;
using PCL.Core.UI.Controls;
using PCL.Core.Utils;
using PCL.Core.Utils.Exts;

namespace PCL;

public partial class MyMsgAuthServer
{
    private readonly Launcher.MyMsgBoxConverter _converter;
    private readonly int _uuid = Common.GetUuid();

    public MyMsgAuthServer(Launcher.MyMsgBoxConverter converter)
    {
        InitializeComponent();
        _converter = converter;
        LabTitle.Text = converter.Title;
        Btn1.Text = converter.Button1;
        Btn2.Text = converter.Button2;
        ComboServer.Text = converter.AuthServerDefault;
        foreach (var preset in converter.AuthServerPresets ?? new Dictionary<string, string>())
            ComboServer.Items.Add(new MyComboBoxItem { Content = preset.Key, Tag = preset.Value });
        ComboServer.SelectionChanged += (_, _) =>
        {
            if (ComboServer.SelectedItem is MyComboBoxItem item)
            {
                var value = item.Tag?.ToString() ?? string.Empty;
                Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
                {
                    ComboServer.SelectedItem = null;
                    ComboServer.Text = value;
                }));
            }
        };
        ShapeLine.StrokeThickness = Common.GetWpfSize(1d);
        LabTitle.MouseLeftButtonDown += Drag;
        PanBorder.MouseLeftButtonDown += Drag;
        Loaded += Load;
    }

    private void Load(object sender, EventArgs e)
    {
        Opacity = 0d;
        Animations.AniStart(
            new[]
        {
            Animations.AaColor(Launcher.frmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                new Common.MyColor(90d, 0d, 0d, 0d) -
                Launcher.frmMain.PanMsgBackground.Background, 200),
            Animations.AaOpacity(this, 1d, 120, 60),
            Animations.AaDouble(i => TransformPos.Y += (double)i, -TransformPos.Y, 300, 60,
                new Animations.AniEaseOutBack(Animations.AniEasePower.Weak)),
            Animations.AaDouble(i => TransformRotate.Angle += (double)i, -TransformRotate.Angle,
                300, 60,
                new Animations.AniEaseOutFluent(Animations.AniEasePower.Weak))
        }, "MyMsgBox " + _uuid);
    }

    private void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
        var server = ComboServer.Text?.Trim();
        if (string.IsNullOrWhiteSpace(server) || !server.IsMatch(RegexPatterns.HttpUri))
        {
            HintService.Hint(Lang.Text("Launch.Account.Auth.InvalidServer"), HintType.Error);
            return;
        }
        _converter.IsExited = true;
        _converter.Result = server;
        _Close();
    }

    private void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (_converter.IsExited) return;
        _converter.IsExited = true;
        _converter.Result = null;
        _Close();
    }

    private void _Close()
    {
        _converter.WaitFrame.Continue = false;
        ComponentDispatcher.PopModal();
        Animations.AniStart(new[]
        {
            Animations.AaCode(() =>
            {
                if (!Launcher.WaitingMyMsgBox.Any())
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
        }, "MyMsgBox " + _uuid);
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(ShapeLine).Y <= 2d)
                Launcher.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            Common.Log(ex, "拖拽移动失败", Common.LogLevel.Hint,
                userSummary: Lang.Text("Application.Control.MessageBox.Error.OperationFailed"));
        }
    }
}
