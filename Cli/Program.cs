using Tyr.Common.Ssl.Vision;

namespace Tyr.Cli;

internal static class Program
{
    private static void Main(string[] args)
    {
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

            Console.WriteLine($"received detection: {packet.Detection != null}, geometry: {packet.Geometry != null}");
        }
    }
}