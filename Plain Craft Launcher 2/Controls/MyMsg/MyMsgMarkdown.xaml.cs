using PCL.Controls.MyMsg;
using PCL.Core.UI;
using PCL.Core.UI.MsgBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace PCL;

public partial class MyMsgMarkdown : IMsgBoxControl
{
    public MsgBoxRequest Request { get; }
    public event EventHandler<MsgBoxResponse>? Completed;

    private readonly MsgBoxAnimationProfile _anim;
    private bool _isExited;
    private readonly string _animGroup;

    public MyMsgMarkdown(MsgBoxRequest request)
    {
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = "MyMsgMarkdown " + Request.RequestId;
        InitFromRequest();
    }

    public MyMsgMarkdown(ModMain.MyMsgBoxConverter converter)
    {
        var isWarn = converter.IsWarn;
        var buttons = new List<MsgBoxButtonInfo>();
        buttons.Add(new MsgBoxButtonInfo(converter.Button1, 1, converter.Button1Action));
        if (!string.IsNullOrEmpty(converter.Button2))
            buttons.Add(new MsgBoxButtonInfo(converter.Button2, 2, converter.Button2Action));
        if (!string.IsNullOrEmpty(converter.Button3))
            buttons.Add(new MsgBoxButtonInfo(converter.Button3, 3, converter.Button3Action));

        var request = new MsgBoxRequest
        {
            Caption = converter.Title,
            Message = converter.Text,
            Theme = isWarn ? MsgBoxTheme.Warning : MsgBoxTheme.Info,
            Buttons = buttons,
            IsBlocking = converter.ForceWait || !string.IsNullOrEmpty(converter.Button2)
        };
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = "MyMsgBox " + ModBase.GetUuid();

        Completed += async (_, response) =>
        {
            converter.IsExited = true;
            converter.Result = response.ButtonValue;
            if (converter.ForceWait || !string.IsNullOrEmpty(converter.Button2))
                converter.WaitFrame.Continue = false;
            ComponentDispatcher.PopModal();
            await InvokeCloseAnimationAsync(response).ConfigureAwait(true);
        };

        InitFromRequest();
    }

    private void InitFromRequest()
    {
        var isWarn = Request.Theme is MsgBoxTheme.Warning or MsgBoxTheme.Error;
        var btn1 = Request.Buttons.ElementAtOrDefault(0);
        var btn2 = Request.Buttons.ElementAtOrDefault(1);
        var btn3 = Request.Buttons.ElementAtOrDefault(2);

        InitializeComponent();
        LabTitle.Text = Request.Caption;
        LabCaption.Markdown = Request.Message;
        DataContext = this;
        ConfigurePrimaryButton(btn1?.Text ?? "确定", isWarn);
        ConfigureSecondaryButton(Btn2, btn2?.Text ?? "");
        ConfigureSecondaryButton(Btn3, btn3?.Text ?? "");
        ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);

        if (_anim.HighlightPrimaryButton && Btn2.IsVisible && Btn1.ColorType != MyButton.ColorState.Red)
            Btn1.ColorType = MyButton.ColorState.Highlight;

        Loaded += (_, _) =>
        {
            try
            {
                Btn1.Focus();
                InvokeShowAnimation();
                ModBase.Log("[Control] Markdown 弹窗：" + LabTitle.Text);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "普通弹窗加载失败", ModBase.LogLevel.Hint);
            }
        };
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

    private static void ConfigureSecondaryButton(MyButton button, string text)
    {
        button.Text = text;
        button.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
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
        if (_isExited) return;
        if (Request.Buttons.ElementAtOrDefault(0)?.OnClick is { } action)
        {
            action();
            return;
        }
        _isExited = true;
        Completed?.Invoke(this, new MsgBoxResponse
        {
            RequestId = Request.RequestId,
            ButtonValue = Request.Buttons.ElementAtOrDefault(0)?.Value ?? 1,
            Button = Request.Buttons.ElementAtOrDefault(0)
        });
    }

    public void Btn2_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isExited) return;
        if (Request.Buttons.ElementAtOrDefault(1)?.OnClick is { } action)
        {
            action();
            return;
        }
        _isExited = true;
        Completed?.Invoke(this, new MsgBoxResponse
        {
            RequestId = Request.RequestId,
            ButtonValue = Request.Buttons.ElementAtOrDefault(1)?.Value ?? 2,
            Button = Request.Buttons.ElementAtOrDefault(1)
        });
    }

    public void Btn3_Click(object sender, MouseButtonEventArgs e)
    {
        if (_isExited) return;
        if (Request.Buttons.ElementAtOrDefault(2)?.OnClick is { } action)
        {
            action();
            return;
        }
        _isExited = true;
        Completed?.Invoke(this, new MsgBoxResponse
        {
            RequestId = Request.RequestId,
            ButtonValue = Request.Buttons.ElementAtOrDefault(2)?.Value ?? 3,
            Button = Request.Buttons.ElementAtOrDefault(2)
        });
    }

    private void Drag(object? sender = null, MouseButtonEventArgs? e = null)
    {
        try
        {
            if (e?.LeftButton == MouseButtonState.Pressed)
                if (e.GetPosition(ShapeLine).Y <= 2d)
                    ModMain.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint);
        }
    }
}
