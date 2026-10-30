using System.Collections;
using PCL.Core.Utils;

namespace PCL;

public static partial class Animations
{
    /// <summary>
    ///     开始一个动画组。
    /// </summary>
    /// <param name="aniGroup">由 Aa 开头的函数初始化的 AniData 对象集合。</param>
    /// <param name="name">动画组的名称。如果重复会直接停止同名动画组。</param>
    public static void AniStart(
        IList aniGroup,
        string name = "",
        bool refreshTime = false)
    {
        if (refreshTime)
            _aniLastTick = TimeUtils.GetTimeTick();
        var newEntry = new AniGroupEntry
        {
            data = Common.GetFullList<AniData>(aniGroup),
            startTick = TimeUtils.GetTimeTick()
        };
        if (string.IsNullOrEmpty(name))
            name = newEntry.uuid.ToString();
        else
            AniStop(name);
        aniGroups.TryAdd(name, newEntry);
    }

    /// <summary>
    ///     开始一个动画组。
    /// </summary>
    public static void AniStart(
        AniData aniGroup,
        string name = "",
        bool refreshTime = false)
    {
        AniStart(
            new List<AniData>
            {
                aniGroup
            },
            name,
            refreshTime);
    }

    /// <summary>
    ///     直接停止一个动画组。
    /// </summary>
    /// <param name="name">需要停止的动画组的名称。</param>
    public static void AniStop(string name)
    {
        aniGroups.Remove(name, out _);
    }

    /// <summary>
    ///     获取动画是否正在进行中。
    /// </summary>
    public static bool AniIsRun(string name)
    {
        return aniGroups.ContainsKey(name);
    }
}
