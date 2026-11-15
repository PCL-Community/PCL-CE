namespace PCL.Core.Platform;

/// <summary>剪贴板访问。具体实现由组合根注册到 <see cref="ApplicationRuntime" />。</summary>
public interface IClipboardService
{
    /// <summary>获取剪贴板中的文件/文件夹路径列表；无文件时返回 <c>null</c> 或空数组。</summary>
    string[]? GetFileDropList();

    /// <summary>获取剪贴板文本；无文本时返回 <c>null</c>。</summary>
    string? GetText();

    void SetText(string text);
}