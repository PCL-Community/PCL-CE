using Avalonia.Controls;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>可访问底层 Avalonia 窗口的 <see cref="PCL.Core.Platform.IWindowHost" />。</summary>
public interface IAvaloniaWindowHost : IWindowHost
{
    Window Window { get; }
}