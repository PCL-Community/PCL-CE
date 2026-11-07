using System.Collections.Generic;

namespace PCL.Core.Platform;

public interface IProcessCleanupService
{
    int CleanupAdditionalData();
    void DeleteFoldersAndRestart(IReadOnlyList<string> folders);
}