using System;

namespace PCL.Core.Platform;

/// <summary>应用运行时的完整服务集合。由组合根在启动时构造一次。</summary>
public sealed class ApplicationServices
{
    public required PlatformModule Platform { get; init; }
    public required UiModule Ui { get; init; }

    /// <summary>由组合根构建的服务容器，供迁移期静态门面读取。</summary>
    public IServiceProvider? Services { get; set; }
}