using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PCL.Core.Utils.VersionControl;

public interface IVersionControl
{
    /// <summary>
    /// 获取全部的 Node 的 ID 信息
    /// </summary>
    /// <returns></returns>
    List<VersionData> GetVersions();
    
    /// <summary>
    /// 获取指定的 Node 信息
    /// </summary>
    /// <param name="nodeId">Node ID</param>
    /// <returns></returns>
    VersionData? GetVersion(string nodeId);
    
    List<FileVersionObjects>? GetNodeObjects(string nodeId);
    
    /// <summary>
    /// 创建一个新的节点
    /// </summary>
    /// <returns>Node ID</returns>
    Task<string> CreateNewVersion(string? name = null, string? desc = null);
    
    /// <summary>
    /// 回到过去的一个 Node
    /// </summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    Task ApplyPastVersion(string nodeId);
    
    /// <summary>
    /// 删除过去的一个 Node
    /// </summary>
    /// <param name="nodeId"></param>
    /// <returns></returns>
    void DeleteVersion(string nodeId);

    /// <summary>
    /// 检查指定的 Node 是否损坏
    /// </summary>
    /// <param name="nodeId"></param>
    /// <param name="deepCheck">深度检查，进行哈希校验</param>
    /// <returns></returns>
    Task<bool> CheckVersion(string nodeId, bool deepCheck = false);

    /// <summary>
    /// 对没有在任何 Node 中有记录的 Objects 进行清理操作
    /// </summary>
    /// <returns></returns>
    Task CleanUnrecordObjects();

    /// <summary>
    /// 获取指定的一个 Object 的数据流
    /// </summary>
    /// <param name="objectId"></param>
    /// <returns></returns>
    Stream? GetObjectContent(string objectId);

    /// <summary>
    /// 导出当前快照
    /// </summary>
    /// <param name="nodeId">Node 的 ID</param>
    /// <param name="saveFilePath">保存到的位置，zip 文件</param>
    /// <returns></returns>
    Task Export(string nodeId, string saveFilePath);
}