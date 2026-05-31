using System;
using System.Security.Cryptography;
using System.Text;

namespace PCL.Core.IO.Storage.Cache;

public class CacheKeys
{
    #region Instance

    /// <summary>实例元数据缓存键</summary>
    public static string InstanceMeta(string instancePath)
        => _Build("instance", "meta", _HashSegment(instancePath));

    /// <summary>实例 version.json 缓存键</summary>
    public static string InstanceManifest(string instancePath, string versionId)
        => _Build("instance", "manifest", _HashSegment(instancePath), versionId);

    /// <summary>实例指定类型的组件缓存键（mods/rp/shader/saves）</summary>
    public static string InstanceComponents(string instancePath, string compType)
        => _Build("instance", "components", _HashSegment(instancePath), compType);

    /// <summary>实例中单个组件文件缓存键</summary>
    public static string InstanceComponentFile(string instancePath, string fileHash)
        => _Build("instance", "component", _HashSegment(instancePath), fileHash);


    #endregion

    #region Download

    /// <summary>URL 下载缓存键（去重）</summary>
    public static string Download(string url)
        => _Build("download", _HashSegment(url));

    /// <summary>Library 文件缓存键</summary>
    public static string Library(string mavenGroup, string artifact, string version)
        => _Build("library", mavenGroup, artifact, version);

    /// <summary>Asset 索引 JSON 缓存键</summary>
    public static string AssetIndex(string url)
        => _Build("assets", "index", _HashSegment(url));

    /// <summary>Asset 对象文件缓存键（由哈希定位）</summary>
    public static string AssetObject(string assetHash)
        => _Build("assets", "object", assetHash);
    #endregion

    #region API/Network

    /// <summary>API 响应缓存键</summary>
    public static string ApiResponse(string source, string url)
        => _Build("http", source, _HashSegment(url));

    public static string ApiResponseMeta(string source, string url)
        => _Build("http", "meta", source, _HashSegment(url));

    /// <summary>模组市场搜索缓存键</summary>
    public static string CompSearch(string source, string query, int page)
        => _Build("comp", "search", source, _HashSegment(query), page.ToString());

    /// <summary>皮肤/头像缓存键</summary>
    public static string Skin(string url)
        => _Build("skin", _HashSegment(url));

    /// <summary>图片文件缓存键</summary>
    public static string Image(string url)
        => _Build("image", _HashSegment(url));

    /// <summary>新闻/公告缓存键</summary>
    public static string News(string url)
        => _Build("news", _HashSegment(url));
    #endregion

    #region Account

    /// <summary>OAuth Token 缓存键（10min 滑动过期）</summary>
    public static string AuthToken(string accountId)
        => _Build("auth", "token", accountId);

    /// <summary>账户列表缓存键（短 TTL）</summary>
    public static string AccountList()
        => "accounts:list";  // 固定键，无需哈希
    #endregion

    private static string _Build(params string[] segments)
        => string.Join(':', segments);

    /// <summary>
    /// 对动态内容（路径、URL、长字符串）做 SHA256 哈希，取前 32 字符。
    /// 静态已知数据（枚举值、类型名）不哈希，保持可读性。
    /// </summary>
    private static string _HashSegment(string raw)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..32];
    }
}