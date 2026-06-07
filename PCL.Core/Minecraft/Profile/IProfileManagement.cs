namespace PCL.Core.Minecraft.Profile;

public interface IProfileManagement<T>
{
    /// <summary>
    /// 创建一个档案
    /// </summary>
    /// <returns></returns>
    public T Create();
    /// <summary>
    /// 删除一个档案
    /// </summary>
    public void Delete(int index);
    /// <summary>
    /// 更新档案
    /// </summary>
    /// <param name="profile">档案文件</param>
    public void Update(T profile);
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
    /// <param name="deleteLocal">是否删除本地档案</param>
    /// </summary>
    public void Clear(bool deleteLocal = false);

}