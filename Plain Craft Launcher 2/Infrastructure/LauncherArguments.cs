using Microsoft.VisualBasic;

namespace PCL;

/// <summary>
///     PCL2 历史命令行参数解析兼容层。
/// </summary>
public static class LauncherArguments
{
    public static object? Get(string name, object? defaultValue = null)
    {
        var allArguments = Interaction.Command().Split(" ");
        for (int i = 0, loopTo = allArguments.Length - 1; i <= loopTo; i++)
            if ((allArguments[i] ?? "") == ("-" + name ?? ""))
            {
                if (allArguments.Length == i + 1 || allArguments[i + 1].StartsWithF("-"))
                    return true;
                return allArguments[i + 1];
            }

        return defaultValue;
    }
}