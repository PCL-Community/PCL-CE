using FluentValidation;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace PCL.Core.UI.MsgBox;

public record MsgBoxRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public string? Hint { get; init; } = null;
    public string Caption { get; init; } = string.Empty;
    public object? Content { get; init; } = null;
    public MsgBoxRequestType RequestType { get; init; }
    public MsgBoxTheme Theme { get; init; }
    public Collection<IValidator<string>>? ValidateRules { get; init; } = null;
    public IReadOnlyList<MsgBoxButtonInfo> Buttons { get; init; } = [];
    public bool IsBlocking { get; init; } = true;
    public CancellationToken CancellationToken { get; init; }

    /// <summary>
    /// Gets or sets the timeout duration for the message box. If set, the message box will automatically close after the specified time has elapsed. <br/>
    /// <see langword="null"/> means infinite.
    /// </summary>
    public TimeSpan? Timeout { get; init; } = null;
}

public enum MsgBoxRequestType
{
    Text,
    Select,
    Input,
    Login,
    Markdown
}