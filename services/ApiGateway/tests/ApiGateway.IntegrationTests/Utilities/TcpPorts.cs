using System.Net;
using System.Net.Sockets;

namespace ApiGateway.IntegrationTests.Utilities;

internal static class TcpPorts
{
    /// <summary>
    /// A local port nothing listens on, so connecting to it is refused.
    /// </summary>
    public static int GetUnused()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);

        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();

        return port;
    }
}
