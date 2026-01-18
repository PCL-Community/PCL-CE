namespace PCL.Core.UI;

public enum HintTheme
{
    Info,
    Success,
    Error
}

public delegate void HintHandler(
    string message,
    HintTheme theme
);

public static class HintWrapper
{
    public static event HintHandler? OnShow;
    
    public static void Show(string message, HintTheme theme = HintTheme.Info)
    {
        OnShow?.Invoke(message, theme);
    }
}
