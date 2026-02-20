namespace PCL.Core.App.Essentials;

/// <summary>
/// RPC 函数<br/>
/// 接收参数并返回响应内容
/// </summary>
/// <param name="argument">参数</param>
/// <returns>响应内容</returns>
public delegate RpcResponse RpcFunction(string? argument, string? content, bool indent);
