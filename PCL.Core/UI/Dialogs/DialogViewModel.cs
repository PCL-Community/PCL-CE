using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using System.Threading;
using System.Text.Json.Nodes;
using FluentValidation;
using PCL.Core.Minecraft.IdentityModel.OAuth;

namespace PCL.Core.UI.Dialogs;

/// <summary>可勾选选项的响应式状态。</summary>
public sealed partial class DialogOptionState : ObservableObject
{
    public DialogOptionState(DialogOption option, int index)
    {
        Option = option;
        Index = index;
        _isChecked = option.IsChecked;
    }

    public DialogOption Option { get; }

    public int Index { get; }

    public string Text => Option.Text;

    public string? Description => Option.Description;

    public string? Image => Option.Image;

    public double ImageScale => Option.ImageScale;

    [ObservableProperty] private bool _isChecked;
}

/// <summary>弹窗的响应式状态，供 UI 绑定与 Core 派发结果。</summary>
public sealed partial class DialogViewModel : ObservableObject
{
    public DialogViewModel(DialogRequest request)
    {
        Request = request;
        _inputText = (request as InputDialogRequest)?.DefaultValue ?? "";
        Options = request is SelectDialogRequest select
            ? select.Options.Select((option, index) => new DialogOptionState(option, index)).ToList()
            : [];
    }

    public DialogRequest Request { get; }

    public DialogKind Kind => Request.Kind;

    public string Title => Request.Title;

    public DialogButtons Buttons => Request.Buttons;

    public bool IsWarn => Request.IsWarn;

    public bool HighLight => Request.HighLight;

    /// <summary>选择模式下的可选项。</summary>
    public IReadOnlyList<DialogOptionState> Options { get; }

    public bool MultiSelect => (Request as SelectDialogRequest)?.MultiSelect ?? false;

    /// <summary>输入模式下的文本。</summary>
    [ObservableProperty] private string _inputText;

    /// <summary>单选模式下的选中项索引，-1 表示未选中。</summary>
    [ObservableProperty] private int _selectedIndex = -1;

    [ObservableProperty] private bool _isClosed;

    public DialogResult? Result { get; private set; }

    public bool IsExited => IsClosed;

    internal TaskCompletionSource<DialogResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private DispatcherFrame? _waitFrame;

    /// <summary>同步入口用于等待弹窗关闭的消息泵。首次访问时必须位于界面线程。</summary>
    public DispatcherFrame WaitFrame => _waitFrame ??= new DispatcherFrame(true);

    internal int PresentAttempts;

    /// <summary>为无分支弹窗（单按钮）预先填入结果，使调用方可立即读取而无需等待。</summary>
    internal void PresetResult(DialogResult result)
    {
        Result = result;
    }

    /// <summary>已勾选的项索引（多选模式）。</summary>
    public string TextContent => Request switch
    {
        TextDialogRequest text => text.Content,
        MarkdownDialogRequest markdown => markdown.Content,
        InputDialogRequest input => input.Content,
        _ => ""
    };

    public string InputDefaultValue => (Request as InputDialogRequest)?.DefaultValue ?? "";

    public string? HintText => (Request as InputDialogRequest)?.HintText;

    public IReadOnlyList<IValidator<string>> ValidateRules =>
        (Request as InputDialogRequest)?.ValidateRules ?? [];

    public string DefaultServer => (Request as AuthServerDialogRequest)?.DefaultServer ?? "";

    public IReadOnlyDictionary<string, string> Presets =>
        (Request as AuthServerDialogRequest)?.Presets ?? new Dictionary<string, string>();

    public JsonObject? LoginData => (Request as LoginDialogRequest)?.Data;

    public Func<JsonObject, CancellationToken, Task<AuthorizeResult?>>? DeviceCodePoll =>
        (Request as LoginDialogRequest)?.DeviceCodePoll;

    public Func<AuthorizeResult, CancellationToken, Task>? LoginResultHandler =>
        (Request as LoginDialogRequest)?.LoginResultHandler;

    public Action<object?>? CompletionHandler => (Request as LoginDialogRequest)?.CompletionHandler;

    public string ButtonText(int button) =>
        button >= 1 && button <= Buttons.Length ? Buttons[button - 1].Text ?? "" : "";

    public Action? ButtonAction(int button) =>
        button >= 1 && button <= Buttons.Length ? Buttons[button - 1].OnClick : null;

    public IReadOnlyList<int> GetCheckedIndices()
        => Options.Where(option => option.IsChecked).Select(option => option.Index).ToList();

    public void Select(int index) => SelectedIndex = index;

    public void Toggle(int index)
    {
        if (index < 0 || index >= Options.Count) return;
        Options[index].IsChecked = !Options[index].IsChecked;
    }

    /// <summary>关闭弹窗。重复调用无副作用。</summary>
    public void Close(DialogResult result)
    {
        if (IsClosed) return;

        Result = result;
        IsClosed = true;
        if (_waitFrame is { } frame) frame.Continue = false;
        Completion.TrySetResult(result);
        DialogService.Instance.OnDialogClosed(this);
    }

    /// <summary>按当前状态构建结果：输入模式取文本，选择模式取索引。</summary>
    public void CloseByButton(int button)
    {
        var request = Request;
        var result = request switch
        {
            InputDialogRequest when button == 1 => new DialogResult(button, InputText),
            SelectDialogRequest { MultiSelect: true } when button == 1
                => new DialogResult(button, SelectedIndices: GetCheckedIndices()),
            SelectDialogRequest when button == 1
                => new DialogResult(button, SelectedIndex: SelectedIndex),
            AuthServerDialogRequest when button == 1 => new DialogResult(button, InputText),
            LoginDialogRequest => new DialogResult(button),
            _ => new DialogResult(button)
        };

        Close(result);
    }
}
