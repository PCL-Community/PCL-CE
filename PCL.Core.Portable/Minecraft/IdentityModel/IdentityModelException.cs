// Copyright (c) MUXUE1230. All rights reserved.
// Modifications Copyright (c) 2026 PCL N contributors.
// Licensed under the Apache License, Version 2.0.

using System;

namespace PCL.Core.Minecraft.IdentityModel;

/// <summary>
/// IdentityModel 模块异常基类。
/// </summary>
public class IdentityModelException(string message, Exception? innerException = null) : Exception(message, innerException);

/// <summary>
/// IdentityModel 配置或元数据不满足当前认证流程要求时抛出的异常。
/// </summary>
public class IdentityModelConfigurationException(string message, Exception? innerException = null)
    : IdentityModelException(message, innerException);

/// <summary>
/// 认证服务器返回 OAuth/Yggdrasil 协议错误时抛出的异常。
/// </summary>
/// <param name="error">协议错误代码。</param>
/// <param name="errorDescription">协议错误描述。</param>
/// <param name="innerException">内部异常。</param>
public class IdentityModelAuthenticationException(
    string? error,
    string? errorDescription,
    Exception? innerException = null)
    : IdentityModelException(_BuildMessage(error, errorDescription), innerException)
{
    /// <summary>
    /// 协议错误代码。
    /// </summary>
    public string? Error { get; } = error;

    /// <summary>
    /// 协议错误描述。
    /// </summary>
    public string? ErrorDescription { get; } = errorDescription;

    private static string _BuildMessage(string? error, string? errorDescription)
    {
        if (!string.IsNullOrWhiteSpace(error) && !string.IsNullOrWhiteSpace(errorDescription))
            return $"Authentication failed ({error}): {errorDescription}";

        if (!string.IsNullOrWhiteSpace(errorDescription))
            return $"Authentication failed: {errorDescription}";
        if (!string.IsNullOrWhiteSpace(error))
            return $"Authentication failed: {error}";
        return "Authentication failed.";
    }
}
