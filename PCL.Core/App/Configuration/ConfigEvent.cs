namespace PCL.Core.App.Configuration;

/// <summary>
/// 配置项事件。
/// </summary>
public enum ConfigEvent
{
    /// <summary>
    /// 初始化，当且仅当程序初始化时调用一次。
    /// </summary>
    Init = 0b00001,

    /// <summary>
    /// 获取。
    /// </summary>
    Get = 0b00010,

    /// <summary>
    /// 设置值。
    /// </summary>
    Set = 0b00100,

    /// <summary>
    /// 重置值。
    /// </summary>
    Reset = 0b01000,

    /// <summary>
    /// 检查是否为默认值。
    /// </summary>
    CheckDefault = 0b10000,

    /// <summary>
    /// 保留备用。
    /// </summary>
    None = 0,

    /// <summary>
    /// 所有读取操作。
    /// </summary>
    Read = Get | CheckDefault,

    /// <summary>
    /// 所有更新操作。
    /// </summary>
    Update = Set | Reset,

    /// <summary>
    /// 所有改变操作。
    /// </summary>
    Changed = Init | Update,

    /// <summary>
    /// 所有操作。没事别监听这个，一点风吹草动都会触发它。
    /// </summary>
    All = Read | Changed
}
