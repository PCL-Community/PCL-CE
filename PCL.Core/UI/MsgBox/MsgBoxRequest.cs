using System;
using System.Collections.Generic;
using System.Threading;

namespace PCL.Core.UI.MsgBox;

public record MsgBoxRequest
{
    public Guid RequestId { get; init; } = Guid.NewGuid();
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public MsgBoxTheme Theme { get; init; }
    public IReadOnlyList<MsgBoxButtonInfo> Buttons { get; init; } = [];
    public bool IsBlocking { get; init; } = true;
    public CancellationToken CancellationToken { get; init; }
    /// <summary>
    /// Gets or sets the timeout duration for the message box. If set, the message box will automatically close after the specified time has elapsed. <br/>
    /// <see langword="null"/> means infinite.
    /// </summary>
    public TimeSpan? Timeout { get; init; } = null;
}