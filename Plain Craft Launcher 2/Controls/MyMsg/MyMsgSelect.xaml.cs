using PCL.Controls.MyMsg;
using PCL.Core.UI.MsgBox;
using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace PCL;

public partial class MyMsgSelect : IMsgBoxControl
{
    public MsgBoxRequest Request { get; }
    public event EventHandler<MsgBoxResponse>? Completed;

    private readonly MsgBoxAnimationProfile _anim;
    private bool _isExited;
    private int _selectedIndex = -1;
    private readonly string _animGroup;

    public MyMsgSelect(MsgBoxRequest request)
    {
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = "MyMsgSelect " + Request.RequestId;
        InitFromRequest(request.Content as IEnumerable);
    }

    public MyMsgSelect(ModMain.MyMsgBoxConverter converter)
    {
        var isWarn = converter.IsWarn;
        var buttons = new List<MsgBoxButtonInfo>
        {
            new(converter.Button1, 1),
            new(converter.Button2, 2)
        };

        var content = converter.Content as IEnumerable;

        var request = new MsgBoxRequest
        {
            Caption = converter.Title,
            Theme = isWarn ? MsgBoxTheme.Warning : MsgBoxTheme.Info,
            Buttons = buttons,
            IsBlocking = true,
            Content = converter.Content
        };
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = "MyMsgBox " + ModBase.GetUuid();

        Completed += async (_, response) =>
        {
            converter.IsExited = true;
            converter.Result = response.ButtonValue == 1 ? _selectedIndex : null;
            converter.WaitFrame.Continue = false;
            ComponentDispatcher.PopModal();
            await InvokeCloseAnimationAsync(response).ConfigureAwait(true);
        };

        InitFromRequest(content);
    }

    private void InitFromRequest(IEnumerable? selections)
    {
        var isWarn = Request.Theme is MsgBoxTheme.Warning or MsgBoxTheme.Error;
        var btn1 = Request.Buttons.ElementAtOrDefault(0);
        var btn2 = Request.Buttons.ElementAtOrDefault(1);

        InitializeComponent();
        LabTitle.Text = Request.Caption;
        ConfigurePrimaryButton(btn1?.Text ?? "确定", isWarn);
        ConfigureSecondaryButton(btn2?.Text ?? "");
        ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);
        InitializeSelectionList(selections);

        if (_anim.HighlightPrimaryButton && Btn2.IsVisible && Btn1.ColorType != MyButton.ColorState.Red)
            Btn1.ColorType = MyButton.ColorState.Highlight;

        Loaded += (_, _) =>
        {
            try
            {
                InvokeShowAnimation();
                ModBase.Log("[Control] 选择弹窗：" + LabTitle.Text);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "选择弹窗加载失败", ModBase.LogLevel.Hint);
            }
        };

        Btn1.Click += Btn1_Click;
        Btn2.Click += Btn2_Click;
        LabTitle.MouseLeftButtonDown += Drag;
        PanBorder.MouseLeftButtonDown += Drag;
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

    private void InitializeSelectionList(IEnumerable? rawList)
    {
        Btn1.IsEnabled = false;
        if (rawList is null) return;

        foreach (var rawContent in rawList)
        {
            var selectionContent = MyVirtualizingElement.TryInit((FrameworkElement)rawContent);
            if (selectionContent is IMyRadio selection)
            {
                PanSelection.Children.Add((UIElement)selection);
                selection.Check += (_, _2) => OnChecked(selection, _2);

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

    public void InvokeShowAnimation()
    {
        Opacity = 0d;
        MsgBoxAnimations.AnimateShow(this, TransformPos, TransformRotate, _anim, _animGroup);
    }

    public async Task InvokeCloseAnimationAsync(MsgBoxResponse response)
    {
        await MsgBoxAnimations.AnimateCloseAsync(this, TransformPos, TransformRotate, _anim, _animGroup).ConfigureAwait(true);
        if (Parent is Grid g) g.Children.Remove(this);
    }

    public void Btn1_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isExited || _selectedIndex == -1) return;
        _isExited = true;
        Completed?.Invoke(this, new MsgBoxResponse
        {
            RequestId = Request.RequestId,
            ButtonValue = 1,
            Button = Request.Buttons.ElementAtOrDefault(0)
        });
    }

    public void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isExited) return;
        _isExited = true;
        Completed?.Invoke(this, new MsgBoxResponse
        {
            RequestId = Request.RequestId,
            ButtonValue = 2,
            Button = Request.Buttons.ElementAtOrDefault(1)
        });
    }

    private void OnChecked(IMyRadio sender, EventArgs e)
    {
        Btn1.IsEnabled = true;
        _selectedIndex = PanSelection.Children.IndexOf((UIElement)sender);
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
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint);
        }
    }
}
