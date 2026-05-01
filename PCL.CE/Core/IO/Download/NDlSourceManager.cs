using System.Collections.Generic;

namespace PCL.CE.Core.IO.Download;

public class NDlSourceManager<TSourceArgument>(IList<IDlResourceMapping<TSourceArgument>> sources)
    : IDlResourceMapping<TSourceArgument>
{
    public IList<IDlResourceMapping<TSourceArgument>> Sources { get; } = sources;

    public TSourceArgument? Parse(string resId)
    {
        throw new System.NotImplementedException();
    }
}
