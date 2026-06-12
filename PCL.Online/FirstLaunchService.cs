// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Reflection;
using PCL.Core.App;

namespace PCL.Online;

/// <summary>
/// 首次启动协议同意服务。
/// </summary>
public static class FirstLaunchService
{
    private const string LegalDocDir = "Legal";
    private const string CurrentVersion = "v2.0";

    private static string GetLegalDirectory()
    {
        var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)
            ?? AppDomain.CurrentDomain.BaseDirectory;
        return Path.Combine(assemblyDir, LegalDocDir);
    }

    public static bool IsAccepted()
    {
        var acceptedVersion = States.Online.LegalAcceptedVersion;
        return acceptedVersion == CurrentVersion;
    }

    public static void Accept()
    {
        States.Online.LegalAcceptedVersion = CurrentVersion;
    }

    /// <summary>
    /// 读取用户协议（优先中文）。
    /// </summary>
    public static string LoadTerms()
    {
        var path = Path.Combine(GetLegalDirectory(), "TERMS_ZH.md");
        if (File.Exists(path)) return File.ReadAllText(path);
        path = Path.Combine(GetLegalDirectory(), "TERMS_EN.md");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    /// <summary>
    /// 读取隐私政策（优先中文）。
    /// </summary>
    public static string LoadPrivacy()
    {
        var path = Path.Combine(GetLegalDirectory(), "PRIVACY_ZH.md");
        if (File.Exists(path)) return File.ReadAllText(path);
        path = Path.Combine(GetLegalDirectory(), "PRIVACY_EN.md");
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    /// <summary>
    /// 获取完整的法律文档内容。
    /// </summary>
    public static string LoadFullText()
    {
        return LoadTerms() + "\n\n---\n\n" + LoadPrivacy();
    }
}
