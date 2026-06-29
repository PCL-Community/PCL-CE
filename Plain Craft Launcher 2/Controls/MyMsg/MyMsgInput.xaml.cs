using PCL.Controls.MyMsg;
using PCL.Core.UI.MsgBox;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;

namespace PCL;

public partial class MyMsgInput : IMsgBoxControl
{
    public MsgBoxRequest Request { get; }
    public event EventHandler<MsgBoxResponse>? Completed;

    private readonly MsgBoxAnimationProfile _anim;
    private bool _isExited;
    private readonly string _animGroup;

    public MyMsgInput(MsgBoxRequest request)
    {
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = "MyMsgInput " + Request.RequestId;
        InitFromRequest(request.Content as string ?? "", request.Hint ?? "", request.ValidateRules);
    }

    public MyMsgInput(ModMain.MyMsgBoxConverter converter)
    {
        var isWarn = converter.IsWarn;
        var buttons = new List<MsgBoxButtonInfo>
        {
            new(converter.Button1, 1),
            new(converter.Button2, 2)
        };

        var content = (string?)converter.Content ?? "";
        var hint = converter.HintText;
        var rules = converter.ValidateRules;

        var request = new MsgBoxRequest
        {
            Caption = converter.Title,
            Message = converter.Text,
            Theme = isWarn ? MsgBoxTheme.Warning : MsgBoxTheme.Info,
            Buttons = buttons,
            IsBlocking = true,
            Content = content,
            Hint = hint,
            ValidateRules = rules
        };
        Request = request;
        _anim = MsgBoxAnimationProfile.ForTheme(request.Theme);
        _animGroup = "MyMsgBox " + ModBase.GetUuid();

        Completed += async (_, response) =>
        {
            converter.IsExited = true;
            converter.Result = response.ButtonValue == 1 ? TextArea.Text : null;
            converter.WaitFrame.Continue = false;
            ComponentDispatcher.PopModal();
            await InvokeCloseAnimationAsync(response).ConfigureAwait(true);
        };

        InitFromRequest(content, hint, rules);
    }

    private void InitFromRequest(string content, string hint,
        System.Collections.ObjectModel.Collection<FluentValidation.IValidator<string>>? rules)
    {
        var isWarn = Request.Theme is MsgBoxTheme.Warning or MsgBoxTheme.Error;
        var btn1 = Request.Buttons.ElementAtOrDefault(0);
        var btn2 = Request.Buttons.ElementAtOrDefault(1);

        InitializeComponent();
        LabTitle.Text = Request.Caption;
        LabText.Text = Request.Message;
        PanText.Visibility = string.IsNullOrEmpty(Request.Message) ? Visibility.Collapsed : Visibility.Visible;
        TextArea.Text = content;
        TextArea.HintText = hint;
        if (rules is not null) TextArea.ValidateRules = rules;
        ConfigurePrimaryButton(btn1?.Text ?? "确定", isWarn);
        ConfigureSecondaryButton(btn2?.Text ?? "");
        ShapeLine.StrokeThickness = ModBase.GetWPFSize(1d);

        if (_anim.HighlightPrimaryButton && Btn2.IsVisible && Btn1.ColorType != MyButton.ColorState.Red)
            Btn1.ColorType = MyButton.ColorState.Highlight;

        Loaded += (_, _) =>
        {
            try
            {
                TextArea.Focus();
                TextArea.SelectionStart = TextArea.Text.Length;
                InvokeShowAnimation();
                ModBase.Log("[Control] 输入弹窗：" + LabTitle.Text);
            }
            catch (Exception ex)
            {
                ModBase.Log(ex, "输入弹窗加载失败", ModBase.LogLevel.Hint);
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

    private void ConfigureSecondaryButton(string text)
    {
        Btn2.Text = text;
        Btn2.Visibility = string.IsNullOrEmpty(text) ? Visibility.Collapsed : Visibility.Visible;
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
        TextArea.Validate();
        if (_isExited || !TextArea.IsValidated) return;
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
        _isExited = true;
        Completed?.Invoke(this, new MsgBoxResponse
        {
            RequestId = Request.RequestId,
            ButtonValue = Request.Buttons.ElementAtOrDefault(1)?.Value ?? 2,
            Button = Request.Buttons.ElementAtOrDefault(1)
        });
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
                    ModMain.frmMain.DragMove();
        }
        catch (Exception ex)
        {
            ModBase.Log(ex, "拖拽移动失败", ModBase.LogLevel.Hint);
        }
    }
}
