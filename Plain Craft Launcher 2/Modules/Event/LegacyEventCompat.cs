using System.Text.RegularExpressions;

namespace PCL;

/// <summary>
///     旧版主页 XAML 兼容转换 — 将裸 EventType/EventData 属性（含中文名）
///     统一替换为 <c>local:CustomEventService.EventType="..."</c> 格式。
/// </summary>
public static partial class LegacyEventCompat
{
    public static readonly Dictionary<string, string> NameMap = new()
    {
        ["打开网页"] = "OpenUrl",
        ["启动游戏"] = "LaunchGame", 
        ["复制文本"] = "CopyText",
        ["刷新主页"] = "RefreshHome", 
        ["刷新页面"] = "RefreshHome",
        ["弹出窗口"] = "ShowDialog",
        ["弹出提示"] = "ShowHint"
    };

    private static readonly HashSet<string> Unsupported =
    [
        "打开文件",
        "打开帮助",
        "执行命令",
        "刷新帮助",
        "今日人品",
        "内存优化",
        "清理垃圾",
        "切换页面",
        "导入整合包",
        "安装整合包",
        "下载文件",
        "修改设置",
        "写入设置",
        "修改变量",
        "写入变量",
        "加入房间",
        "检查更新"
    ];

    /// <summary>
    ///     将 XAML 中裸 EventType/EventData 转换为 attached property 格式。
    ///     中文名映射为英文；已是英文枚举值的原样保留；其余弹 Hint 后移除。
    /// </summary>
    public static string TransformLegacyXaml(string xaml)
    {
        // 1. 裸 EventData → local:CustomEventService.EventData
        xaml = BareEventDataRegex().Replace(xaml, """ local:CustomEventService.EventData="$1" """);
        // 2. 裸 EventType → 附加属性格式
        xaml = BareEventTypeRegex().Replace(xaml, match =>
        {
            var name = match.Groups[1].Value;
            if (NameMap.TryGetValue(name, out var english))
                return $" local:CustomEventService.EventType=\"{english}\" ";
            if (Unsupported.Contains(name))
            {
                ModMain.Hint(Core.App.Localization.Lang.Text("Event.Hint.LegacyTypeRemoved", name),
                    ModMain.HintType.Critical);
                return " ";
            }
            // 合法的英文枚举值（OpenUrl 等）
            return $" local:CustomEventService.EventType=\"{name}\" ";
        });
        // 3. 收尾
        return MultiSpaceRegex().Replace(xaml, " ")
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");
    }

    [GeneratedRegex("""(?<![.:])EventType\s*=\s*"([^"]*)"\s*""")]
    private static partial Regex BareEventTypeRegex();
    [GeneratedRegex("""(?<![.:])EventData\s*=\s*"([^"]*)"\s*""")]
    private static partial Regex BareEventDataRegex();
    [GeneratedRegex("""  +""")]
    private static partial Regex MultiSpaceRegex();
}
