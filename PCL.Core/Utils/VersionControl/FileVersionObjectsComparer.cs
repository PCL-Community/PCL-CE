using System.Collections.Generic;

namespace PCL.Core.Utils.VersionControl;

public class FileVersionObjectsComparer : IEqualityComparer<FileVersionObjects>
{
    public static readonly FileVersionObjectsComparer Instance = new();
    
    public bool Equals(FileVersionObjects x, FileVersionObjects y)
    {
        return x.Hash == y.Hash || x.Path == y.Path;
    }

    public int GetHashCode(FileVersionObjects obj)
    {
        return obj.Hash.GetHashCode();
    }
}