using System.Collections.ObjectModel;
using FluentValidation;
using PCL.Core.UI;

namespace PCL;

/// <summary>将弹窗服务请求转发至启动器静态弹窗入口的实现。</summary>
public sealed class DialogService : IDialogService
{
    public Task<int> ShowAsync(
        string title,
        string content,
        string button1,
        string? button2 = null,
        string? button3 = null,
        bool isWarn = false)
        => Launcher.MyMsgBoxAsync(content, title, button1, button2, button3, isWarn: isWarn);

    public Task<string?> InputAsync(
        string title,
        string content,
        string? defaultValue = null,
        string? hintText = null,
        IReadOnlyList<IValidator<string>>? validateRules = null,
        string? button1 = null,
        string? button2 = null,
        bool isWarn = false)
        => Launcher.MyMsgBoxInputAsync(
            title,
            content,
            defaultValue ?? "",
            validateRules is null ? null : new Collection<IValidator<string>>(validateRules.ToList()),
            hintText ?? "",
            button1,
            button2,
            isWarn);

    public async Task<int?> SelectAsync(string title, IReadOnlyList<string> options, string? button2 = null)
    {
        var selections = new List<IMyRadio>(options.Count);
        foreach (var option in options)
            selections.Add(new MyRadioBox { Text = option });
        return await Launcher.MyMsgBoxSelectAsync(selections, title, button2: button2);
    }

    public async Task<List<int>> MultiSelectAsync(string title, IReadOnlyList<string> options, string? button2 = null)
    {
        var selections = new List<MyListItem>(options.Count);
        foreach (var option in options)
            selections.Add(new MyListItem { Title = option });
        return await Launcher.MyMsgBoxMultiSelectAsync(selections, title, button2: button2);
    }
}
