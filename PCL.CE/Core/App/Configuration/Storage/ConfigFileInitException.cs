using System;

namespace PCL.CE.Core.App.Configuration.Storage;

public class ConfigFileInitException(string path, string message, Exception? inner = null)
    : Exception(message, inner)
{
    /// <summary>
    /// Relative file path.
    /// </summary>
    public string Path { get; } = path;
}
