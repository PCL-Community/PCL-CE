using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using PCL.Core.UI;
using PCL.Core.UI.Controls;

namespace PCL;

public partial class DialogControl
{
    private int _result;
    private bool _exited;
    private readonly int _uuid = ModBase.GetUuid();
    internal readonly List<(DialogButton Data, MyButton Control)> _buttons = [];

    public DispatcherFrame WaitFrame { get; } = new(true);

    public string Title
    {
        get => LabTitle.Text;
        set => LabTitle.Text = value;
    }

    public bool IsWarn { get; set; }

    private bool _showTitle = true;
    internal bool ShowTitle
    {
        get => _showTitle;
        set
        {
            _showTitle = value;
            LabTitle.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            ShapeLine.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
        }
    }

    public UIElement? DialogContent
    {
        get => (UIElement?)ContentArea.Content;
        set => ContentArea.Content = value;
    }

    public int Result => _result;

    public DialogControl()
    {
        try
        {
            InitializeComponent();
            ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "DialogControl 初始化失败", ModBase.LogLevel.Hint);
        }

        Loaded += OnLoad;
    }

    public MyButton AddButton(DialogButton button)
    {
        var btn = DialogButtonBuilder.Build(button, IsWarn);
        var buttonId = button.Id > 0 ? button.Id : _buttons.Count + 1;
        btn.Click += (_, _) =>
        {
            if (_exited) return;
            if (button.OnClick is not null)
                button.OnClick();
            else
                Close(buttonId);
        };
        _buttons.Add((button, btn));
        PanBtn.Children.Add(btn);

        return btn;
    }

    public event Action<int>? OnClosed;

    private void OnLoad(object sender, RoutedEventArgs e)
    {
        try
        {
            if (IsWarn)
                LabTitle.SetResourceReference(TextBlock.ForegroundProperty, "ColorBrushRedLight");
            if (_buttons.Count > 1 && _buttons[0].Control.ColorType != MyButton.ColorState.Red)
                _buttons[0].Control.ColorType = MyButton.ColorState.Highlight;
            if (_buttons.Count > 0)
                _buttons[0].Control.Focus();
            else
                PanBtn.Visibility = Visibility.Collapsed;

            Opacity = 0d;
            ModAnimation.AniStart(
                ModAnimation.AaColor(ModMain.frmMain.PanMsgBackground, BlurBorder.BackgroundProperty,
                    (IsWarn
                        ? new ModBase.MyColor(140d, 80d, 0d, 0d)
                        : new ModBase.MyColor(90d, 0d, 0d, 0d)) - ModMain.frmMain.PanMsgBackground.Background, 200),
                "PanMsgBackground Background");
            ModAnimation.AniStart(
            new ModAnimation.AniData[]
            {
                ModAnimation.AaOpacity(this, 1d, 120, 60),
                ModAnimation.AaDouble(i => TransformPos.Y += (double)i,
                    -TransformPos.Y, 300, 60, new ModAnimation.AniEaseOutBack(ModAnimation.AniEasePower.Weak)),
                ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                    -TransformRotate.Angle, 300, 60,
                    new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak))
            }, "DialogControl " + _uuid);

            ModBase.Log("[Dialog] " + LabTitle.Text);
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "DialogControl 加载失败", ModBase.LogLevel.Hint);
        }
    }

    public void Close(int result)
    {
        if (_exited) return;
        _exited = true;
        _result = result;
        CloseInternal();
    }

    public void Close()
    {
        if (_exited) return;
        _exited = true;
        _result = -1;
        CloseInternal();
    }

    private void CloseInternal()
    {
        try
        {
            WaitFrame.Continue = false;
        }
        catch
        {
            // ignore
        }

        try
        {
            ComponentDispatcher.PopModal();
        }
        catch
        {
            // ignore
        }

        OnClosed?.Invoke(_result);

        ModAnimation.AniStart(
        new ModAnimation.AniData[]
        {
            ModAnimation.AaCode(() =>
            {
                var hasMore = (ModMain.frmMain?.PanMsg?.Children.Count ?? 0) > 1;
                if (!hasMore)
                    ModAnimation.AniStart(ModAnimation.AaColor(ModMain.frmMain.PanMsgBackground,
                        BlurBorder.BackgroundProperty,
                        new ModBase.MyColor(0d, 0d, 0d, 0d) - ModMain.frmMain.PanMsgBackground.Background, 200,
                        ease: new ModAnimation.AniEaseOutFluent(ModAnimation.AniEasePower.Weak)));
            }, 30),
            ModAnimation.AaOpacity(this, -Opacity, 80, 20),
            ModAnimation.AaDouble(i => TransformPos.Y += (double)i, 20d - TransformPos.Y,
                150, 0, new ModAnimation.AniEaseOutFluent()),
            ModAnimation.AaDouble(i => TransformRotate.Angle += (double)i,
                6d - TransformRotate.Angle, 150, 0, new ModAnimation.AniEaseInFluent(ModAnimation.AniEasePower.Weak)),
            ModAnimation.AaCode(() => ((Grid)Parent)?.Children.Remove(this), after: true)
        }, "DialogControl " + _uuid);
    }

    private void Drag(object sender, MouseButtonEventArgs e)
    {
        try
        {
            if (e.LeftButton == MouseButtonState.Pressed && e.GetPosition(ShapeLine).Y <= 2d)
                ModMain.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint);
        }
    }
}
