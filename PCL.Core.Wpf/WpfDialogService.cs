using System.Windows;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF <see cref="MessageBox" /> 的应用级对话框实现。</summary>
public sealed class WpfDialogService : IDialogService
{
    public void ShowFatal(string message, string caption)
    {
        MessageBox.Show(message, caption, MessageBoxButton.OK, MessageBoxImage.Error);
    }
}