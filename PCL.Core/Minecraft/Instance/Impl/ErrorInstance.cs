using PCL.Core.App;
using PCL.Core.Minecraft.Folder;
using PCL.Core.Minecraft.Instance.Handler;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance.Impl;

public class ErrorInstance : IMcInstance {
    /// <summary>
    /// 初始化错误的 Minecraft 实例
    /// </summary>
    public ErrorInstance(string path, McFolder folder, string? desc = null, string? logo = null) {
        // 定义基础路径
        var basePath = System.IO.Path.Combine(folder.Path, "versions");

        // 判断是否为绝对路径，并拼接正确的路径
        Path = path.Contains(':') ? path : System.IO.Path.Combine(basePath, path);

        Folder = folder;

        Desc = desc ?? "该实例未被加载，请向作者反馈此问题";
        Logo = logo ?? Basics.GetAppImagePath("Blocks/RedstoneBlock.png");
    }

    public McFolder Folder { get; }
    
    public string Path { get; }

    public string Name => InstanceBasicHandler.GetName(Path);

    public string IsolatedPath => string.Empty;

    public McInstanceCardType CardType { get; set; } = McInstanceCardType.Error;

    public string Desc { get; set; }

    public string Logo { get; set; }

    public bool IsStarred => false;

    public PatchInstanceInfo InstanceInfo { get; set; } = new PatchInstanceInfo();

    public void Load() {}
}
