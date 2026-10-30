using System.Collections.Generic;
using System.Threading.Tasks;
using FluentValidation;

namespace PCL.Core.UI;

/// <summary>不产生任何界面交互的弹窗服务实现。</summary>
public sealed class NullDialogService : IDialogService
{
    public static readonly NullDialogService Instance = new();

    public Task<int> ShowAsync(
        string title,
        string content,
        string button1,
        string? button2 = null,
        string? button3 = null,
        bool isWarn = false) => Task.FromResult(0);

    public Task<string?> InputAsync(
        string title,
        string content,
        string? defaultValue = null,
        string? hintText = null,
        IReadOnlyList<IValidator<string>>? validateRules = null,
        string? button1 = null,
        string? button2 = null,
        bool isWarn = false)
        => Task.FromResult<string?>(null);

    public Task<int?> SelectAsync(string title, IReadOnlyList<string> options, string? button2 = null)
        => Task.FromResult<int?>(null);

    public Task<List<int>> MultiSelectAsync(string title, IReadOnlyList<string> options, string? button2 = null)
        => Task.FromResult(new List<int>());
}
