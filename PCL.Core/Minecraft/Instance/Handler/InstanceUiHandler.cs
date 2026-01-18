using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PCL.Core.App;
using PCL.Core.Minecraft.Instance.Interface;
using PCL.Core.Utils;

namespace PCL.Core.Minecraft.Instance.Handler;

public static class InstanceUiHandler {
    private static readonly ImmutableDictionary<string, (int Year, string Description)> FoolVersionDescriptions =
        ImmutableDictionary.CreateRange(new Dictionary<string, (int Year, string Description)>
        {
            { "15w14a", (2015, "作为一款全年龄向的游戏，我们需要和平，需要爱与拥抱。") },
            { "1.rv-pre1", (2016, "是时候将现代科技带入 Minecraft 了！") },
            { "3d shareware v1.34", (2019, "我们从地下室的废墟里找到了这个开发于 1994 年的杰作！") },
            { "20w14∞", (2020, "我们加入了 20 亿个新的维度，让无限的想象变成了现实！") },
            { "22w13oneblockatatime", (2022, "一次一个方块更新！迎接全新的挖掘、合成与骑乘玩法吧！") },
            { "23w13a_or_b", (2023, "研究表明：玩家喜欢作出选择——越多越好！") },
            { "24w14potato", (2024, "毒马铃薯一直都被大家忽视和低估，于是我们超级加强了它！") },
            { "25w14craftmine", (2025, "你可以合成任何东西——包括合成你的世界！") }
        });

    private static readonly ImmutableDictionary<string, string> VariantSuffixes =
        ImmutableDictionary.CreateRange(new Dictionary<string, string>
        {
            { "red", "（红色版本）" },
            { "blue", "（蓝色版本）" },
            { "purple", "（紫色版本）" }
        });
    
    private static readonly ImmutableArray<string> DescStrings = [
        "开启一段全新的冒险之旅！",
        "创造属于你的独特世界。",
        "探索无尽的可能性。",
        "随时随地，开始你的旅程。",
        "打造你的梦想之地。",
        "自由发挥，享受无限乐趣。",
        "一个属于你的 Minecraft 故事。",
        "发现新奇，创造精彩。",
        "轻松开启，畅玩无忧。",
        "你的冒险，从这里起航。",
        "构建、探索、尽情享受！",
        "适合每一位玩家的乐园。",
        "创造与冒险的完美结合。",
        "开启属于你的游戏篇章。",
        "探索未知，创造奇迹。",
        "属于你的 Minecraft 世界。",
        "简单上手，乐趣无穷。",
        "打造你的专属冒险舞台。",
        "从零开始，创造无限。",
        "你的故事，等待书写！"
    ];

    /// <summary>
    /// 获得一个实例实际的描述文本
    /// </summary>
    public static string GetDescription(IMcInstance instance) {
        return string.IsNullOrEmpty(Config.Instance.CustomInfo[instance.Path])
            ? GetDefaultDescription(instance)
            : Config.Instance.CustomInfo[instance.Path];
    }
    
    /// <summary>
    /// 获得一个实例的默认描述文本
    /// </summary>
    private static string GetDefaultDescription(IMcInstance instance) {
        if (instance.CardType == McInstanceCardType.Error) {
            return "";
        }
        return instance.InstanceInfo.VersionType == McVersionType.Fool ? GetMcFoolVersionDesc(instance.InstanceInfo.McVersionStr!) : RandomUtils.PickRandom(DescStrings);
    }
    
    public static string GetMcFoolVersionDesc(string name) {
        name = name.ToLowerInvariant();

        // 精确匹配
        if (FoolVersionDescriptions.TryGetValue(name, out var match))
            return $"{match.Year} | {match.Description}";

        // 前缀匹配
        if (name.StartsWith("2.0") || name.StartsWith("2point0"))
            return $"2013 | 这个秘密计划了两年的更新将游戏推向了一个新高度！{GetVariantSuffix(name)}";

        return "";
    }
    
    private static string GetVariantSuffix(string name) {
        return VariantSuffixes.FirstOrDefault(s => name.EndsWith(s.Key)).Value ?? "";
    }
    
    public static string GetLogo(IMcInstance instance) {
        var logo = Config.Instance.LogoPath[instance.Path];
        if (string.IsNullOrEmpty(logo) || !Config.Instance.IsLogoCustom[instance.Path]) {
            return instance.InstanceInfo.GetLogo();
        }
        return logo;
    }
}
