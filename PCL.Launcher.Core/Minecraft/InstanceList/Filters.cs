namespace PCL;

public static partial class InstanceList
{
    /// <summary>
    ///     筛选特定种类的实例，并直接添加为卡片。
    /// </summary>
    /// <param name="instanceList">用于筛选的列表。</param>
    /// <param name="formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    /// <param name="cardType">卡片的名称。</param>
    private static void McInstanceFilter(
        ref List<McInstance> instanceList,
        ref Dictionary<McInstanceCardType, List<McInstance>> target,
        McInstanceState[] formula,
        McInstanceCardType cardType)
    {
        var keepList = instanceList
            .Where(v => formula.Contains(v.state))
            .ToList();
        if (keepList.Count == 0) return;

        target.Add(cardType, keepList);
        instanceList = [.. instanceList.Except(keepList)];
    }

    /// <summary>
    ///     筛选特定种类的实例，并增加入一个已有列表中。
    /// </summary>
    /// <param name="instanceList">用于筛选的列表。</param>
    /// <param name="formula">需要筛选出的实例类型。-2 代表隐藏的实例。</param>
    /// <param name="keepList">传入需要增加入的列表。</param>
    private static void McInstanceFilter(
        ref List<McInstance> instanceList,
        McInstanceState[] formula,
        ref List<McInstance> keepList)
    {
        keepList.AddRange(
            instanceList
                .Where(v => formula.Contains(v.state)));
        if (keepList.Count != 0) instanceList = [.. instanceList.Except(keepList)];
    }
}