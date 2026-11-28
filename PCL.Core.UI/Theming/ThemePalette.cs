using Avalonia.Media;

namespace PCL.Core.UI.Theming;

/// <summary>主题色阶的可视化数据，不包含预览界面所需的其它颜色。</summary>
public sealed class ThemePalette
{
    public required IReadOnlyList<IBrush> Ladder { get; init; }

    public static ThemePalette Empty { get; } = new()
    {
        Ladder = []
    };
}