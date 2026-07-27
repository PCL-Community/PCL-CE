using System;

namespace PCL.Core.Minecraft.Modpack;

/// <summary>
/// 整合包声明的下载地址的准入策略。
/// </summary>
public static class ModpackDownloadPolicy
{
    /// <summary>
    /// 判断下载地址是否可用。
    /// <para>
    /// 只要求 HTTPS：明文 HTTP 让整合包内容可被中途篡改，而校验值同样来自该整合包，
    /// 无法提供保护，因此这类地址一律丢弃。
    /// </para>
    /// <para>
    /// <b>不做域名白名单。</b> Modrinth 官方规范列出的允许域名
    /// （<c>cdn.modrinth.com</c>、<c>github.com</c> 等）是其平台的<i>投稿受理规则</i>，
    /// 约束的是能上传到 modrinth.com 的整合包，而非第三方启动器的安装限制。
    /// 现实中大量 <c>.mrpack</c> 直接引用 CurseForge 的 <c>edge.forgecdn.net</c>
    /// 或作者自建 CDN，按白名单拦截或告警只会产生无法处理的噪音。
    /// </para>
    /// </summary>
    public static bool IsAcceptable(string? url)
        => !string.IsNullOrWhiteSpace(url)
           && Uri.TryCreate(url, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps;
}
