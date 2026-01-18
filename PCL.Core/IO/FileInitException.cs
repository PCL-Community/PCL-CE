using System;

namespace PCL.Core.IO;

public class FileInitException(
    string path,
    string? message = null,
    Exception? innerException = null
) : Exception(message, innerException)
{
    public string FilePath { get; } = path;
}
