using System.Windows;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF <see cref="Clipboard" /> 的剪贴板实现。</summary>
public sealed class WpfClipboardService : IClipboardService
{
    public string[]? GetFileDropList()
    {
        var dataObject = Clipboard.GetDataObject();
        return dataObject?.GetData(DataFormats.FileDrop) as string[];
    }

    public string? GetText()
    {
        return Clipboard.GetText();
    }

    public void SetText(string text)
    {
        Clipboard.SetText(text);
    }
}