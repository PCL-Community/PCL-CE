namespace PCL.Core.Logging;

/// <summary>
/// 事件/意外行为等级。
/// </summary>
public enum ActionLevel
{
    TraceLog = 00,
    NormalLog = 10,
    Hint = 20,
    HintErr = 21,
    MsgBox = 30,
    MsgBoxErr = 31,
    MsgBoxFatal = 32,
}
