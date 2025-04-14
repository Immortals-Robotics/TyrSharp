using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;

namespace Tyr.Vision;

[Configurable]
public class SslVisionDataPublisher : IDisposable
{
    [ConfigEntry] public static Address VisionAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10006 };
    [ConfigEntry] public static Address VisionSimAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10025 };

    private readonly UdpReceiver<WrapperPacket> _udpReceiver = new(VisionSimAddress, OnData);

    public SslVisionDataPublisher()
    {
        Logger.ZLogInformation($"SSL Vision Data publisher initialized on {VisionSimAddress}.");
    }

    private static void OnData(WrapperPacket data)
    {
        if (data.Detection != null)
            Hub.RawDetection.Publish(data.Detection);

        if (data.Geometry != null)
            Hub.RawGeometry.Publish(data.Geometry);
    }

    public void Dispose()
    {
        _udpReceiver.Dispose();
    }
}