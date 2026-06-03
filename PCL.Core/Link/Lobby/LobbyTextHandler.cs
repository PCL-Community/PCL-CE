using System;
using PCL.Core.App.Localization;
using PCL.Core.Link.EasyTier;

namespace PCL.Core.Link.Lobby;

public static class LobbyTextHandler
{
    public static string GetNatTypeName(string type)
    {
        return Lang.Text(type switch
        {
            _ when type.Contains("Open") || type.Contains("NoP") => "Link.Nat.Type.Open",
            _ when type.Contains("FullCone") => "Link.Nat.Type.FullCone",
            _ when type.Contains("PortRestricted") => "Link.Nat.Type.PortRestricted",
            _ when type.Contains("Restricted") => "Link.Nat.Type.Restricted",
            _ when type.Contains("SymmetricEasy") => "Link.Nat.Type.SymmetricEasy",
            _ when type.Contains("Symmetric") => "Link.Nat.Type.Symmetric",
            _ => "Link.Nat.Type.Unknown"
        });
    }

    public static string GetConnectTypeName(ETConnectionType type)
    {
        return Lang.Text(type switch
        {
            ETConnectionType.Local => "Link.Connection.Local",
            ETConnectionType.P2P => "Link.Connection.P2P",
            ETConnectionType.Relay => "Link.Connection.Relay",
            _ => "Link.Connection.Unknown"
        });
    }

    /// <summary>
    ///     依据网络质量指数获取大厅连接状况文本。
    /// </summary>
    public static (string Keyword, string Desc) GetQualityDesc(int quality)
    {
        var keySuffix = quality switch
        {
            >= 3 => "Good",
            >= 2 => "Normal",
            _ => "Poor"
        };

        return (
            Lang.Text($"Link.Quality.{keySuffix}"),
            Lang.Text($"Link.Quality.{keySuffix}Description")
        );
    }
}