using System.Collections.Generic;

namespace PCL.Core.Minecraft.Profile;

public interface IProfileManagement<T>
{
    /// <summary>
    /// 创建一个档案
    /// </summary>
    /// <returns></returns>
    public void Add(T profile);
    /// <summary>
    /// 删除一个档案
    /// </summary>
    public void Delete(T profile);
    /// <summary>
    /// 更新档案
    /// </summary>
    /// <param name="origin">原档案文件</param>
    /// <param name="newProfile">新档案文件</param>
    public void Update(T origin, T newProfile);
    /// <summary>
    /// 从指定文件加载档案
    /// </summary>
    /// <param name="path"></param>
    public void LoadFromPath(string path);
    /// <summary>
    /// 从指定字符串加载档案信息
    /// </summary>
    /// <param name="profiles">档案信息</param>
    public void LoadFromString(string profiles);
    /// <summary>
    /// 清空档案列表
    /// </summary>
    public void Clear();

    public IEnumerable<T> GetAll();

}