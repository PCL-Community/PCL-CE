using PCL.Core.App.Region;

namespace PCL.Core.Platform;

public interface IRegionSignalProvider
{
    RegionSignals ReadSignals();
}