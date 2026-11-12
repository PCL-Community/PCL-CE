namespace PCL.Core.UI.Hints;

/// <summary>一条提示请求。</summary>
public sealed record HintRequest
{
    /// <summary>提示正文，换行会被折叠为空格。</summary>
    public required string Message { get; init; }

    /// <summary>提示类型，决定图标与主色。</summary>
    public HintKind Kind { get; init; } = HintKind.Info;

    /// <summary>是否写入日志。</summary>
    public bool Log { get; init; } = true;

    /// <summary>自定义图标（lucide 路径），为空时按 <see cref="Kind" /> 取默认图标。</summary>
    public string? Icon { get; init; }

    /// <summary>自定义显示时长（毫秒），为空时按正文长度自动计算。</summary>
    public double? Duration { get; init; }
}
