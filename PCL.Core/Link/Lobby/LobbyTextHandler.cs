using PCL.Core.Link.EasyTier;

namespace PCL.Core.Link.Lobby;

public static class LobbyTextHandler
{
    public static string GetNatTypeChinese(string type)
    {
        if (type.Contains("Open") || type.Contains("NoP")) return "开放";
        if (type.Contains("FullCone")) return "中等 (完全圆锥)";
        if (type.Contains("PortRestricted")) return "中等 (端口受限圆锥)";
        if (type.Contains("Restricted")) return "中等 (受限圆锥)";
        if (type.Contains("SymmetricEasy")) return "严格 (宽松对称)";
        if (type.Contains("Symmetric")) return "严格 (对称)";
        return "未知";
    }

    public static string GetConnectTypeChinese(ETConnectionType type) => type switch
    {
        ETConnectionType.Local => "本机",
        ETConnectionType.P2P => "P2P",
        ETConnectionType.Relay => "中继",
        _ => "未知"
    };

    /// <summary>
    /// 依据网络质量指数获取大厅连接状况文本
    /// </summary>
    public static (string Keyword, string Desc) GetQualityDesc(int quality) => quality switch
    {
        >= 3 => ("优秀", "当前网络环境不会影响联机体验\n该网络环境适合作为大厅创建者"),
        >= 2 => ("一般", "当前网络环境可能会影响您的联机体验"),
        _ => ("较差", "部分路由器和防火墙设置可能会影响您的联机体验")
    };
}
