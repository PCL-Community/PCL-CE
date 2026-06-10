using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PCL;

/// <summary>
///     旧版主页 XAML 兼容转换。
///     将 XAML 中的缩写 EventType（含中文名）统一转换为 attached property 格式。
/// </summary>
public static partial class LegacyEventCompat
{
    /// <summary>支持的中文名 → 英文枚举值（供 AnnouncementService 等外部调用者使用）。</summary>
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

    private static readonly HashSet<string> Unsupported = new()
    {
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
        "检查更新",
    };

    /// <summary>
    ///     将 XAML 中所有裸 EventType/EventData 替换为 attached property 格式，
    ///     中文名映射为英文枚举值。不支持的旧类型直接移除并弹 Hint。
    /// </summary>
    public static string TransformLegacyXaml(string xaml)
    {
        // 先处理裸 EventData → local:CustomEventService.EventData
        // 注意：不在纯文本注释中的 EventData 才处理。XAML 里的 Text="... EventData ..." 是纯文本不会匹配，只有作为属性的才匹配
        xaml = BareEventDataRegex().Replace(xaml, " local:CustomEventService.EventData=\"$1\" ");

        // 处理裸 EventType，将中文映射为英文
        xaml = BareEventTypeRegex().Replace(xaml, match =>
        {
            var name = match.Groups[1].Value;
            if (NameMap.TryGetValue(name, out var english))
                return $" local:CustomEventService.EventType=\"{english}\" ";
            if (Unsupported.Contains(name))
                ModMain.Hint(
                    PCL.Core.App.Localization.Lang.Text("Event.Hint.LegacyTypeRemoved", name),
                    ModMain.HintType.Critical);
            return " ";
        });

        // 清理多余的连续空格
        xaml = MultiSpaceRegex().Replace(xaml, " ");

        // Property="EventType" → attached property 格式
        xaml = xaml
            .Replace("Property=\"EventType\"", "Property=\"local:CustomEventService.EventType\"")
            .Replace("Property=\"EventData\"", "Property=\"local:CustomEventService.EventData\"");

        return xaml;
    }

    [GeneratedRegex("""(?<![.:])EventType\s*=\s*"([^"]*)"\s*""")]
    private static partial Regex BareEventTypeRegex();

    [GeneratedRegex("""(?<![.:])EventData\s*=\s*"([^"]*)"\s*""")]
    private static partial Regex BareEventDataRegex();

    [GeneratedRegex("""  +""")]
    private static partial Regex MultiSpaceRegex();
}
