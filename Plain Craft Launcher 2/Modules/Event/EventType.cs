namespace PCL;

/// <summary>事件动作类型。</summary>
public enum EventType
{
    None = 0,
    /// <summary>打开网页</summary>
    OpenUrl,
    /// <summary>启动游戏</summary>
    LaunchGame,
    /// <summary>复制文本</summary>
    CopyText,
    /// <summary>刷新主页</summary>
    RefreshHome,
    /// <summary>弹出窗口</summary>
    ShowDialog,
    /// <summary>弹出提示</summary>
    ShowHint,
    /// <summary>调用应用内函数（C# 语法）</summary>
    InvokeFunction,
}
