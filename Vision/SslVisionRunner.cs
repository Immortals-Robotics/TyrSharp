using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Dataflow;
using Tyr.Common.Module;
using Tyr.Common.Network;

namespace Tyr.Vision;

public class SslVisionRunner : UdpRunner<WrapperPacket>
{
    protected override Address Address => Configs.Network.VisionSim;

    protected override void OnData(WrapperPacket data)
    {
        if (data.Detection != null)
            Hub.RawDetection.Publish(data.Detection);

        if (data.Geometry != null)
            Hub.RawGeometry.Publish(data.Geometry);
    }
}