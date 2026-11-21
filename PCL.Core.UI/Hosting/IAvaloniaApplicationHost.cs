using Avalonia;
using PCL.Core.Platform;

namespace PCL.Core.UI;

/// <summary>Avalonia 应用宿主的附加生命周期入口。</summary>
public interface IAvaloniaApplicationHost : IApplicationHost
{
    /// <summary>绑定由 Avalonia 创建的应用实例。</summary>
    void Attach(Application application);
}