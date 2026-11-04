using PCL.Core.App.Localization;
using PCL.UI.Controls;

namespace PCL.Views.Pages.Instance;

/// <summary>
///     实例总览顶部卡片展示的实例条目。
///     结构与实例选择页的版本条目一致（名称、描述、版本标签、图标），仅用于展示，不挂任何操作按钮。
/// </summary>
internal static class InstanceVersionItem
{
    public static MyListItem Create(McInstance instance)
    {
        var item = new MyListItem
        {
            Title = instance.Name,
            Height = 42d,
            tag = instance,
            Type = MyListItem.CheckType.Clickable
        };
        LocalizedText.TryBind(item, "Info", () => instance.Desc);

        var info = instance.Info;
        var tags = new List<string> { McInstanceInfo.GetVanillaNameDisplay(info.VanillaName) };
        if (info.HasForge)
            tags.Add("Forge" + McInstanceInfo.FormatVersion(info.Forge));
        else if (info.HasNeoForge)
            tags.Add("NeoForge" + McInstanceInfo.FormatVersion(info.NeoForge));
        else if (info.HasCleanroom)
            tags.Add("Cleanroom" + McInstanceInfo.FormatVersion(info.Cleanroom));
        else if (info.HasLabyMod)
            tags.Add("LabyMod" + McInstanceInfo.FormatVersion(info.LabyMod));
        else if (info.HasQuilt)
            tags.Add("Quilt" + McInstanceInfo.FormatVersion(info.Quilt));
        else if (info.HasFabric)
            tags.Add("Fabric" + McInstanceInfo.FormatVersion(info.Fabric));
        if (info.HasLiteLoader)
            tags.Add("LiteLoader");
        if (info.HasOptiFine)
            tags.Add("OptiFine" + McInstanceInfo.FormatVersion(info.OptiFine));
        item.PlainTags = tags;

        try
        {
            item.Logo = instance.Logo.EndsWith(Path.Combine("PCL", "Logo.png"))
                ? Path.Combine(instance.PathInstance, "PCL", "Logo.png")
                : instance.Logo;
        }
        catch (Exception ex)
        {
            LauncherCommon.Log(
                ex,
                Lang.Text("Select.Instance.Error.IconLoad"),
                LauncherLogLevel.Hint,
                userSummary: Lang.Text("Select.Instance.Error.IconLoad"));
            item.Logo = LauncherPaths.ImageRoot + "Blocks/RedstoneBlock.png";
        }

        return item;
    }
}
