using PCL.Core.Minecraft.Folder;
using PCL.Core.Minecraft.Instance.Impl;

namespace PCL.Core.Minecraft.Instance.Interface;

public interface IMcInstance {
    /// <summary>
    /// 实例处于的 Minecraft 文件夹
    /// </summary>
    McFolder Folder { get; }
    
    /// <summary>
    /// 实例文件夹路径，以“\”结尾
    /// </summary>
    string Path { get; }
    
    /// <summary>
    /// 应用版本隔离后的 Minecraft 根文件夹路径
    /// </summary>
    string IsolatedPath { get; }

    /// <summary>
    /// 实例文件夹名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 实例卡片类型
    /// </summary>
    McInstanceCardType CardType { get; set; }
    
    /// <summary>
    /// 显示的实例描述文本
    /// </summary>
    string Desc { get; set; }
    
    /// <summary>
    /// 显示的实例图标路径
    /// </summary>
    string Logo { get; set;  }
    
    /// <summary>
    /// 实例是否被收藏
    /// </summary>
    bool IsStarred { get; }
    
    /// <summary>
    /// 实例由版本 JSON 分析得到的信息
    /// </summary>
    PatchInstanceInfo InstanceInfo { get; set; }

    /// <summary>
    /// 加载实例方法
    /// </summary>
    void Load();
}