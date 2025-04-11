using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;

namespace Tyr.Vision;

public class SslVisionDataPublisher : IDisposable
{
    private readonly UdpReceiver<WrapperPacket> _udpReceiver = new(Configs.Network.VisionSim, OnData);

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