using PCL.Core.App.IoC;

namespace PCL;

[LifecycleService(LifecycleState.Loaded)]
public sealed class VanillaVersionService : GeneralService
{
    private VanillaVersionService() : base("minecraft-versions", "versions 列表", false)
    {
    }

    public override void Start()
    {
        VanillaVersionIndex.Capture();
        VanillaVersionIndex.BeginRefresh?.Invoke();
    }
}
