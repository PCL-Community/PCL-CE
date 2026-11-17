using PCL.Core.Platform;

namespace PCL;

public interface IUpdatePolicy
{
    bool SupportsInAppUpdate { get; }
    string InstallButtonKey { get; }
}

public static class UpdatePolicies
{
    public static IUpdatePolicy Current =>
        ApplicationRuntime.GetService<IUpdatePolicy>()
        ?? throw new InvalidOperationException("更新策略服务尚未初始化");
}

public sealed class WindowsUpdatePolicy : IUpdatePolicy
{
    public bool SupportsInAppUpdate => true;
    public string InstallButtonKey => "Setup.Update.Install";
}

public sealed class ManualUpdatePolicy : IUpdatePolicy
{
    public bool SupportsInAppUpdate => false;
    public string InstallButtonKey => "Setup.Update.OpenDownload";
}