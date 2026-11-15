namespace PCL.Core.UI;

public enum HintTheme
{
    Info,
    Success,
    Error,
    Warning
}

/// <summary>旧版提示入口的兼容层，内部转发到 <see cref="Hints.HintService" />。</summary>
public static class HintWrapper
{
    public static void Show(string message, HintTheme theme = HintTheme.Info)
    {
        Hints.HintService.Show(new Hints.HintRequest
        {
            Message = message,
            Kind = theme switch
            {
                HintTheme.Success => Hints.HintKind.Success,
                HintTheme.Error => Hints.HintKind.Error,
                HintTheme.Warning => Hints.HintKind.Warning,
                _ => Hints.HintKind.Info
            }
        });
    }
}
