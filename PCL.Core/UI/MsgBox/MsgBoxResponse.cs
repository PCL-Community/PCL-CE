using System;

namespace PCL.Core.UI.MsgBox;

public record MsgBoxResponse
{
    public Guid RequestId { get; init; }
    public int? ButtonValue { get; init; }
    public MsgBoxButtonInfo? Button { get; init; }

    public static MsgBoxResponse Cancelled(Guid requestId) => new()
    {
        RequestId = requestId,
        ButtonValue = null,
        Button = null
    };
}