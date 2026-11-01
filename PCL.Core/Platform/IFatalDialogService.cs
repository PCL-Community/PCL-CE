namespace PCL.Core.Platform;

/// <summary>
///     致命错误对话框：仅用于界面尚未完全就绪时兜底展示崩溃信息。<br />
///     业务层的数据化弹窗请使用 <see cref="PCL.Core.UI.IDialogService" />，二者语义不同。
///     具体实现由 UI 层注册到 <see cref="PlatformServices.DialogService" />。
/// </summary>
public interface IFatalDialogService
{
    /// <summary>显示致命错误对话框。该调用可能在 UI 完全初始化之前发生，实现必须保证可用。</summary>
    void ShowFatal(string message, string caption);
}