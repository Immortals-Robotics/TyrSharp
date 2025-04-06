using ZLogger;
using Microsoft.Extensions.Logging;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Debug;

namespace Tyr.Cli;

internal static class Program
{
    private static void Main(string[] args)
    {
        Debug.InitLogging(LogLevel.Debug);

        var configPath = args[0];
        var config = Common.Config.Config.Load(configPath);

        var client = new Common.Network.UdpClient(config.Network.VisionSim);
        while (true)
        {
            var packet = client.Receive<WrapperPacket>();
            if (packet == null)
            {
                Thread.Sleep(1);
                continue;
            }

            Debug.Logger.ZLogDebug(
                $"received detection: {packet.Detection != null}, geometry: {packet.Geometry != null}");
        }
    }
}