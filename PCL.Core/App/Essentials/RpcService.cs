using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using PCL.Core.App.IoC;
using PCL.Core.IO;

namespace PCL.Core.App.Essentials;

/// <summary>
/// 用于终止 Pipe RPC 执行过程并返回错误信息的异常<br/>
/// 当抛出该异常时 RPC 服务端将会返回内容为 <c>Reason</c> 的 <c>ERR</c> 响应
/// </summary>
public class RpcException(string reason) : Exception
{
    public string Reason => reason;
}

/// <summary>
/// 标记一个方法或属性，使其成为 RPC 函数/属性。<br/>
/// 方法签名需兼容 <see cref="RpcFunction"/> 委托，属性仅支持 <see langword="string"/> 类型。
/// </summary>
/// <param name="name">该 RPC 函数/属性的名称</param>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Method)]
[DependencyCollector<RpcFunction>("rpc-function", AttributeTargets.Method)]
[DependencyCollector<string>("rpc-property", AttributeTargets.Property)]
public sealed class RegisterRpc(string name) : Attribute;

/// <summary>
/// RPC 服务项
/// </summary>
[LifecycleService(LifecycleState.Loaded)]
[LifecycleScope("rpc", "远程执行服务")]
public sealed partial class RpcService
{
    private NamedPipeServerStream? _pipe;

    [LifecycleStart]
    private void _Start()
    {
        _pipe = PipeComm.StartPipeServer("Echo", _EchoPipeName, _EchoPipeCallback);
    }

    [LifecycleStop]
    private async Task _Stop()
    {
        if (_pipe != null) await _pipe.DisposeAsync();
    }

    [LifecycleDependencyInjection("rpc-function", AttributeTargets.Method)]
    private static void _CollectRpcFunctionRegistry(ImmutableList<(RpcFunction func, string name)> items)
    {
        foreach (var (func, name) in items)
        {
            AddFunction(name, func);
        }
    }

    [LifecycleDependencyInjection("rpc-property", AttributeTargets.Property)]
    private static void _CollectRpcPropertyRegistry(ImmutableList<(PropertyAccessor<string> prop, string name)> items)
    {
        foreach (var (prop, name) in items)
        {
            AddProperty(new RpcProperty(
                name,
                () => prop.Value,
                value => prop.Value = value ?? "",
                prop.CanSet
            ));
        }
    }

    public const string PipePrefix = "PCLCE_RPC";
    
    private static readonly string _EchoPipeName = $"{PipePrefix}@{Basics.CurrentProcess.Id}";
    private static readonly string[] _RequestTypeArray = ["GET", "SET", "REQ"];
    private static readonly HashSet<string> _RequestType = [.._RequestTypeArray];

    #region Property
    
    private static readonly Dictionary<string, RpcProperty> _PropertyMap = new();

    /// <summary>
    /// 添加一个新的 RPC 属性，若有多个使用 foreach 即可
    /// </summary>
    /// <param name="prop">要添加的属性</param>
    /// <returns>是否成功添加（若已存在相同名称的属性则无法添加）</returns>
    public static bool AddProperty(RpcProperty prop) => _PropertyMap.TryAdd(prop.Name, prop);

    /// <summary>
    /// 通过指定的名称删除已存在的 RPC 属性
    /// </summary>
    /// <param name="name">属性名称</param>
    /// <returns>是否成功删除（若不存在该名称则无法删除）</returns>
    public static bool RemoveProperty(string name)
    {
        return _PropertyMap.Remove(name);
    }

    /// <summary>
    /// 删除已存在的 RPC 属性，实质上仍然是通过属性的名称删除，但会检查是否是同一个对象
    /// </summary>
    /// <param name="prop">要删除的属性</param>
    /// <returns></returns>
    public static bool RemoveProperty(RpcProperty prop)
    {
        var key = prop.Name;
        var result = _PropertyMap.TryGetValue(key, out var value);
        if (!result || value != prop) return false;
        _PropertyMap.Remove(key);
        return true;
    }

    #endregion

    #region Function

    private static readonly Dictionary<string, RpcFunction> _FunctionMap = new() {
        ["ping"] = ((_, _, _) => RpcResponse.EmptySuccess),
        ["activate"] = ((_, _, _) =>
        {
            if (Lifecycle.CurrentState >= LifecycleState.WindowCreated) ActivateMainWindow();
            else Lifecycle.When(LifecycleState.WindowCreated, ActivateMainWindow);
            return RpcResponse.EmptySuccess;

            void ActivateMainWindow()
            {
                var app = Lifecycle.CurrentApplication;
                app.Dispatcher.BeginInvoke(() =>
                {
                    var window = app.MainWindow!;
                    if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
                    if (!window.Topmost)
                    {
                        window.Topmost = true;
                        window.Topmost = false;
                    }
                    window.Activate();
                });
            }
        })
    };

    /// <summary>
    /// 添加一个新的 RPC 函数，若有多个使用 foreach 即可
    /// </summary>
    /// <param name="name">函数名称</param>
    /// <param name="func">函数过程</param>
    /// <returns>是否成功添加（若已存在相同名称的函数则无法添加）</returns>
    public static bool AddFunction(string name, RpcFunction func) => _FunctionMap.TryAdd(name, func);

    /// <summary>
    /// 通过指定的名称删除已存在的 RPC 函数
    /// </summary>
    /// <param name="name">函数名称</param>
    /// <returns>是否成功删除（若不存在该名称则无法删除）</returns>
    public static bool RemoveFunction(string name)
    {
        return _FunctionMap.Remove(name);
    }
    
    #endregion

    private static bool _EchoPipeCallback(StreamReader reader, StreamWriter writer, Process? client)
    {
        try
        {
            // GET/SET/REQ [target]
            // [content]
            var header = reader.ReadLine(); // 读入请求头
            Context.Info($"客户端请求: {header}");

            var args = header?.Split([' '], 2) ?? []; // 分离请求类型和参数
            if (args.Length < 2 || args[1].Length == 0) throw new RpcException("请求参数过少");
            var type = args[0].ToUpperInvariant();
            if (!_RequestType.Contains(type)) throw new RpcException($"请求类型必须为 {string.Join("/", _RequestTypeArray)} 其中之一");
            var target = args[1];

            // 读入请求内容（可能没有）
            var buffer = new StringBuilder();
            var tmp = reader.Read();
            while (tmp != PipeComm.PipeEndingChar)
            {
                buffer.Append((char)tmp);
                tmp = reader.Read();
            }
            var content = buffer.Length == 0 ? null : buffer.ToString();

            switch (type)
            {
                case "GET": case "SET": {
                    target = target.ToLowerInvariant();
                    var result = _PropertyMap.TryGetValue(target, out var prop);
                    if (!result) throw new RpcException($"不存在属性 {target}");
                    RpcResponse response;
                    if (type == "GET")
                    {
                        try
                        {
                            var value = prop!.Value;
                            response = new RpcResponse(RpcResponseStatus.Success, RpcResponseType.Text, value, target);
                            Context.Trace($"返回值: {value}");
                        }
                        catch (RpcPropertyOperationFailedException)
                        {
                            response = RpcResponse.EmptyFailure;
                            Context.Debug("设置失败: 只写属性或请求被拒绝");
                        }
                    }
                    else if (prop!.Settable)
                    {
                        try
                        {
                            prop.Value = content;
                            response = RpcResponse.EmptySuccess;
                            Context.Trace($"设置成功: {content}");
                        }
                        catch (RpcPropertyOperationFailedException)
                        {
                            response = RpcResponse.EmptyFailure;
                            Context.Debug("设置失败: 请求被拒绝");
                        }
                    }
                    else
                    {
                        response = RpcResponse.EmptyFailure;
                        Context.Debug("设置失败: 只读属性");
                    }
                    response.Response(writer);
                    break;
                }

                case "REQ": {
                    var targetArgs = target.Split([' '], 2); // 分离函数名和参数
                    var name = targetArgs[0].ToLowerInvariant();
                    var indent = false; // 检测缩进指示
                    if (name.EndsWith('$'))
                    {
                        indent = true;
                        name = name[..^1];
                    }
                    var result = _FunctionMap.TryGetValue(name, out var func);
                    if (!result) throw new RpcException($"不存在函数 {name}");
                    string? argument = null;
                    if (targetArgs.Length > 1)
                        argument = targetArgs[1];
                    Context.Trace($"正在调用函数 {name} {argument}");
                    var response = func!(argument, content, indent);
                    response.Response(writer);
                    Context.Trace($"函数已退出，返回状态 {response.Status}");
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            if (ex is RpcException rpcEx)
            {
                var reason = rpcEx.Reason;
                RpcResponse.Err(reason).Response(writer);
                Context.Info($"出错: {reason}");
            }
            else
            {
                RpcResponse.Err(ex.ToString(), "stacktrace").Response(writer);
                Context.Error("处理请求时发生异常", ex);
            }
        }
        return true;
    }
}
