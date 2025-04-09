using ZLogger;
using Microsoft.Extensions.Logging;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Config;

namespace Tyr.Cli;

internal static class Program
{
    private static void Main(string[] args)
    {
        var configPath = args[0];
        Configs.Load(configPath);

        var client = new Common.Network.UdpClient(Configs.Network.VisionSim);
        while (true)
        {
            if (!client.IsDataAvailable())
            {
                Thread.Yield();
                continue;
            }

            var packet = client.Receive<WrapperPacket>();
            if (packet == null)
            {
                Logger.ZLogError($"Received null packet");
                continue;
            }

            if (packet.Detection != null)
            {
                DateTime captureTime = packet.Detection.CaptureTime;
                DateTime sentTime = packet.Detection.SentTime;
                DateTime now = DateTime.UtcNow;

                var processingTime = sentTime - captureTime;
                var networkDelay = now - sentTime;
                var totalDelay = now - captureTime;

                Logger.ZLogDebug(
                    $"delays: process: {processingTime.TotalMilliseconds}ms, network: {networkDelay.TotalMilliseconds}ms, total: {totalDelay.TotalMilliseconds}ms");
            }

            Logger.ZLogDebug(
                $"received detection: {packet.Detection != null}, geometry: {packet.Geometry != null}");
        }
    }
}