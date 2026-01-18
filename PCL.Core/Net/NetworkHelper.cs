using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;

namespace PCL.Core.Net;

public static class NetworkHelper
{
    public static int NewTcpPort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
    
    public static bool IsNetworkAvailable()
    {
        return NetworkInterface.GetIsNetworkAvailable();
    }
}
