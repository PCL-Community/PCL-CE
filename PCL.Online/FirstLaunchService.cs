// Copyright (c) MUXUE1230. All rights reserved.
// Licensed under the MIT License.

using System;
using System.IO;
using System.Text;

namespace PCL.Online;

/// <summary>
/// 首次启动协议同意服务。
/// </summary>
public static class FirstLaunchService
{
    private const string LegalDocDir = "Legal";
    private const string LegalResourcePrefix = "PCL.Online.Legal.";
    private const string CurrentVersion = "v2.0";

    private static string GetLegalDirectory()
    {
        return Path.Combine(AppContext.BaseDirectory, LegalDocDir);
    }

    public static bool IsAccepted()
    {
        var acceptedVersion = OnlineRuntime.Host.GetString("Online.LegalAcceptedVersion");
        return acceptedVersion == CurrentVersion;
    }

    public static void Accept()
    {
        OnlineRuntime.Host.SetString("Online.LegalAcceptedVersion", CurrentVersion);
        OnlineRuntime.Host.Flush();
    }

    /// <summary>
    /// 读取用户协议（优先中文）。
    /// </summary>
    public static string LoadTerms()
    {
        return LoadDocument("TERMS_ZH.md", "TERMS_EN.md");
    }

    /// <summary>
    /// 读取隐私政策（优先中文）。
    /// </summary>
    public static string LoadPrivacy()
    {
        return LoadDocument("PRIVACY_ZH.md", "PRIVACY_EN.md");
    }

    /// <summary>
    /// 获取完整的法律文档内容。
    /// </summary>
    public static string LoadFullText()
    {
        return LoadTerms() + "\n\n---\n\n" + LoadPrivacy();
    }

    private static string LoadDocument(params string[] fileNames)
    {
        foreach (var fileName in fileNames)
        {
            var path = Path.Combine(GetLegalDirectory(), fileName);
            if (File.Exists(path))
                return File.ReadAllText(path, Encoding.UTF8);
        }

        var assembly = typeof(FirstLaunchService).Assembly;
        foreach (var fileName in fileNames)
        {
            using var stream = assembly.GetManifestResourceStream(LegalResourcePrefix + fileName);
            if (stream is null)
                continue;

            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }

        return string.Empty;
    }
}
