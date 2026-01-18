using System.Text.Json.Nodes;
using System.Threading.Tasks;
using PCL.Core.App;
using PCL.Core.Minecraft.Folder;
using PCL.Core.Minecraft.Instance.Handler;
using PCL.Core.Minecraft.Instance.Handler.Info;
using PCL.Core.Minecraft.Instance.Interface;

namespace PCL.Core.Minecraft.Instance.Impl;

/// <summary>
/// 管理以 Merge 类型 JSON 为基础的实例基础信息
/// </summary>
public class MergeInstance : IMcInstance, IJsonBasedInstance {
    // 使用缓存以避免复杂属性的重复计算

    /// <summary>
    /// 初始化以 Merge JSON 为基础的 Minecraft 实例
    /// </summary>
    public MergeInstance(string path, in McFolder folder, string? logo = null, string? desc = null, JsonObject? versionJson = null) {
        // 定义基础路径
        var basePath = System.IO.Path.Combine(folder.Path, "versions");

        // 判断是否为绝对路径，并拼接正确的路径
        Path = path.Contains(':') ? path : System.IO.Path.Combine(basePath, path);
        
        Logo = logo ?? Basics.GetAppImagePath("Blocks/RedstoneBlock.png");
        
        Desc = desc ?? "该实例未被加载，请向作者反馈此问题";

        Folder = folder;
        
        VersionJson = versionJson;
    }
    
    public McFolder Folder { get; }
    
    public string Path { get; }
    
    public string Name => InstanceBasicHandler.GetName(Path);
    
    public string IsolatedPath =>  InstanceIsolationHandler.GetIsolatedPath(this, FolderService.FolderManager.CurrentFolder!);
    
    public string Desc { get; set; }

    public string Logo { get; set; }
    
    public bool IsStarred => InstanceBasicHandler.GetIsStarred(Path);
    
    public McInstanceCardType CardType {
        get {
            if (!InstanceBasicHandler.HasCorrectCardType(field)) {
                field = InstanceBasicHandler.RefreshInstanceCardType(this);
            }
            return field;
        }
        set {
            if (InstanceBasicHandler.HasCorrectCardType(field)) {
                return;
            }
            field = value;
            Config.Instance.CardType[Path] = (int)value;
        }
    }
    
    /// <summary>
    /// 异步获取 JSON 对象。
    /// </summary>
    /// <returns>表示 Minecraft 实例的 JSON 对象。</returns>
    private async Task GetVersionJsonAsync() {
        VersionJson ??= await InstanceJsonHandler.RefreshVersionJsonAsync(this);
    }

    private async Task RefreshVersionJsonAsync() {
        VersionJson = await InstanceJsonHandler.RefreshVersionJsonAsync(this);
    }
    
    public JsonObject? VersionJson { get; private set; }

    /// <summary>
    /// 异步获取 Jar 中的 JSON 对象。
    /// </summary>
    /// <returns>表示 Minecraft 实例的 Jar 中的 JSON 对象。</returns>
    private async Task GetVersionJsonInJarAsync() {
        VersionJsonInJar ??= await InstanceJsonHandler.RefreshVersionJsonInJarAsync(this);
    }

    private async Task RefreshVersionJsonInJarAsync() {
        VersionJsonInJar = await InstanceJsonHandler.RefreshVersionJsonInJarAsync(this);
    }
    
    public JsonObject? VersionJsonInJar { get; private set; }

    public PatchInstanceInfo InstanceInfo
    {
        get
        {
            if (field == null)
            {
                InstanceFactory.UpdateFromClonedInstance(this,
                    InfoMergeHandler.RefreshMergeInstanceInfo(this, VersionJson!));
            }

            return field!;
        }
        set;
    }

    /// <summary>
    /// 是否为旧版 JSON 格式
    /// </summary>
    public bool IsOldJson => VersionJson!.ContainsKey("minecraftArguments");

    public void Load() {
        SetDescriptiveInfo();
    }

    public async Task RefreshAsync() {
        await RefreshVersionJsonAsync();

        SetDescriptiveInfo();
    }

    private void SetDescriptiveInfo() {
        // 确定实例描述和状态
        Desc = InstanceUiHandler.GetDescription(this);
        Logo = InstanceUiHandler.GetLogo(this);

        // 写入缓存
        Config.Instance.Info[Path] = Desc;
        Config.Instance.LogoPath[Path] = Logo;
    }
}
