using System;
using System.IO;
using System.Windows;
using PCL.Core.Platform;

namespace PCL.Core.Wpf;

/// <summary>基于 WPF <see cref="Application.GetResourceStream(Uri)" /> 的打包资源访问实现。</summary>
public sealed class WpfResourceStore : IResourceStore
{
    public Stream? GetResourceStream(string path)
    {
        var info = Application.GetResourceStream(
            new Uri($"pack://application:,,,/{path}", UriKind.Absolute));
        return info?.Stream;
    }
}