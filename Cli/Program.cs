using ZLogger;
using Microsoft.Extensions.Logging;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Debug;
using Tyr.Common.Time;

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
            if (!client.IsDataAvailable())
            {
                Thread.Yield();
                continue;
            }

            var packet = client.Receive<WrapperPacket>();
            if (packet == null)
            {
                Debug.Logger.ZLogError($"Received null packet");
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

                Debug.Logger.ZLogDebug(
                    $"delays: process: {processingTime.TotalMilliseconds}ms, network: {networkDelay.TotalMilliseconds}ms, total: {totalDelay.TotalMilliseconds}ms");
            }

            Debug.Logger.ZLogDebug(
                $"received detection: {packet.Detection != null}, geometry: {packet.Geometry != null}");
        }
    }
}