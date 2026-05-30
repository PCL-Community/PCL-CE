namespace PCL.Core.IO.Net.Http.Cache.Models;

public enum HttpCacheStatus
{
    /// <summary>
    /// 缓存无效（例如文件已经删除）
    /// </summary>
    Invalid,
    Ok,
    Expired,
    Updating
}