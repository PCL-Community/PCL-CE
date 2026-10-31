namespace PCL.Core.UI.Hints;

/// <summary>常用提示请求的预设构造。</summary>
public static class Hints
{
    public static HintRequest Info(string message, bool log = true) =>
        new() { Message = message, Kind = HintKind.Info, Log = log };

    public static HintRequest Success(string message, bool log = true) =>
        new() { Message = message, Kind = HintKind.Success, Log = log };

    public static HintRequest Warning(string message, bool log = true) =>
        new() { Message = message, Kind = HintKind.Warning, Log = log };

    public static HintRequest Error(string message, bool log = true) =>
        new() { Message = message, Kind = HintKind.Error, Log = log };
}
