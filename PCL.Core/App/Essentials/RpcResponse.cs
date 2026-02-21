using System;
using System.IO;

namespace PCL.Core.App.Essentials;

public enum RpcResponseStatus
{
    Success,
    Failure,
    Err
}

public enum RpcResponseType
{
    Empty,
    Text,
    Json,
    Base64
}

/// <summary>
/// Pipe RPC 响应
/// </summary>
public class RpcResponse
{
    public RpcResponseStatus Status { get; }

    public RpcResponseType Type { get; }

    public string? Name { get; }

    public string? Content { get; }

    public RpcResponse(RpcResponseStatus status, RpcResponseType type = RpcResponseType.Empty, string? content = null,
        string? name = null)
    {
        if (content != null && type == RpcResponseType.Empty)
            throw new ArgumentException("Empty response with non-null content");
        Status = status;
        Type = type;
        Content = content;
        Name = name;
    }

    // STATUS type [name]
    // [content]
    public void Response(StreamWriter writer)
    {
        var nameArea = Name == null ? "" : $" {Name}";
        writer.WriteLine($"{Status.ToString().ToUpperInvariant()} {Type.ToString().ToLowerInvariant()}{nameArea}");
        if (Content != null)
            writer.WriteLine(Content);
    }

    public static readonly RpcResponse EmptySuccess = new RpcResponse(RpcResponseStatus.Success);

    public static readonly RpcResponse EmptyFailure = new RpcResponse(RpcResponseStatus.Failure);

    public static RpcResponse Err(string content, string? name = null)
    {
        return new RpcResponse(RpcResponseStatus.Err, RpcResponseType.Text, content, name);
    }

    public static RpcResponse Success(RpcResponseType type, string content, string? name = null)
    {
        return new RpcResponse(RpcResponseStatus.Success, type, content, name);
    }

    public static RpcResponse Failure(RpcResponseType type, string content, string? name = null)
    {
        return new RpcResponse(RpcResponseStatus.Failure, type, content, name);
    }
}
