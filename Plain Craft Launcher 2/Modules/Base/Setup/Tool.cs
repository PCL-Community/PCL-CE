using PCL.Network;

namespace PCL;

public partial class Setup
{
    public static void ToolDownloadThread(int value)
    {
        NetFacade.NetTaskConnectionLimit = Math.Clamp(
            value + 1,
            1,
            NetFacade.NetTaskConnectionLimitMax);
        DownloadResourceManager.OnConnectionLimitChanged();
    }

    public static void ToolDownloadFileConnection(int value)
    {
        NetFacade.NetTaskSingleFileConnectionLimit = Math.Clamp(
            value + 1,
            1,
            NetFacade.NetTaskSingleFileConnectionLimitMax);
    }

    public static void ToolDownloadSpeed(int value)
    {
        NetFacade.NetTaskSpeedLimitHigh = value switch
        {
            <= 14 => (long)Math.Round((value + 1) * 0.1d * 1024d * 1024d),
            <= 31 => (long)Math.Round((value - 11) * 0.5d * 1024d * 1024d),
            <= 41 => (value - 21) * 1024 * 1024L,
            _ => -1
        };
    }
}
