using System.Collections.Generic;

namespace PCL.Core.Net.NDownload;

public class NDlSourceManager<TSourceArgument>(IList<IDlResourceMapping<TSourceArgument>> sources)
    : IDlResourceMapping<TSourceArgument>
{
    public IList<IDlResourceMapping<TSourceArgument>> Sources { get; } = sources;

    public TSourceArgument? Parse(string resId)
    {
        throw new System.NotImplementedException();
    }
}
