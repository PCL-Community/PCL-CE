namespace PCL.Core.Platform;

/// <summary>
///     应用级对话框。具体实现由 UI 层注册到 <see cref="PlatformServices.DialogService" />。
/// </summary>
public interface IDialogService
{
    /// <summary>显示致命错误对话框。该调用可能在 UI 完全初始化之前发生，实现必须保证可用。</summary>
    void ShowFatal(string message, string caption);
}