using System.Threading;
using System.Threading.Tasks;

namespace PCL.Core.Platform;

/// <summary>剪贴板访问。具体实现由组合根注册到 <see cref="ApplicationRuntime" />。</summary>
public interface IClipboardService
{
    /// <summary>获取剪贴板中的文件/文件夹路径列表；无文件时返回 <c>null</c> 或空数组。</summary>
    Task<string[]?> GetFileDropListAsync(CancellationToken cancellationToken = default);

    /// <summary>获取剪贴板文本；无文本时返回 <c>null</c>。</summary>
    Task<string?> GetTextAsync(CancellationToken cancellationToken = default);

    /// <summary>写入剪贴板文本。</summary>
    Task SetTextAsync(string text, CancellationToken cancellationToken = default);
}