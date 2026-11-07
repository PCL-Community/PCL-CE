using System.Windows;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF <see cref="MessageBox" /> 的致命错误对话框实现。</summary>
public sealed class WpfDialogService : IFatalDialogService
{
    public void ShowFatal(string message, string caption)
    {
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}