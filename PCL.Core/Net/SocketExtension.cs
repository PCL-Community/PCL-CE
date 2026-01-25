using System.Net.Sockets;

namespace PCL.Core.Net;

public static class SocketExtensions
{
    public static void SafeClose(this Socket? socket)
    {
        if (socket is null) return;

        try
        {
            if (socket.Connected)
            {
                socket.Shutdown(SocketShutdown.Both);
            }
            socket.Close();
        }
        catch { /* 忽略关闭时的任何错误 */ }
    }
}