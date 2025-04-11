using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;
using Gc = Tyr.Common.Data.Ssl.Gc;

namespace Tyr.Referee;

public class GcDataPublisher : IDisposable
{
    private readonly UdpReceiver<Gc.Referee> _udpReceiver = new(Configs.Network.Referee, OnData);

    private static void OnData(Gc.Referee data)
    {
        Hub.RawReferee.Publish(data);
    }

    public void Dispose()
    {
        _udpReceiver.Dispose();
    }
}