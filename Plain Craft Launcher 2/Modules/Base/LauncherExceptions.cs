using System;

namespace PCL;

/// <summary>
/// Marker exception indicating the caller should retry the current operation.
/// </summary>
public class RestartException : Exception
{
}

/// <summary>
/// Marker exception indicating the user intentionally cancelled the current operation.
/// </summary>
public class CancelledException : Exception
{
}
