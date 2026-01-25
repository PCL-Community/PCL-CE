using System;

namespace PCL.Core.Link.Scaffolding.Exceptions;

/// <summary>
/// Exception thrown when a scaffolding request fails with a non-success status code.
/// </summary>
public class ScaffoldingRequestException(byte statusCode, string? serverMessage = null) : Exception(_BuildMessage(
    statusCode,
    serverMessage))
{
    /// <summary>
    /// The status code returned by the scaffolding server.
    /// </summary>
    public byte StatusCode { get; } = statusCode;

    /// <summary>
    /// The error message or details returned by the server, if any.
    /// </summary>
    public string? ServerMessage { get; } = serverMessage;

    private static string _BuildMessage(byte statusCode, string? serverMessage)
    {
        var errorType = statusCode switch
        {
            >= 32 and < 64 => "Protocol-defined error",
            255 => "Unknow error",
            _ => "Generic error"
        };

        return string.IsNullOrEmpty(serverMessage)
            ? $"Scaffolding request failed with status code {statusCode} ({errorType})."
            : $"Scaffolding request failed with status code {statusCode} ({errorType}): {serverMessage}";
    }
}