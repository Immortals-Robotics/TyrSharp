using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;
using Gc = Tyr.Common.Data.Ssl.Gc;

namespace Tyr.Referee;

[Configurable]
public class GcDataPublisher : IDisposable
{
    [ConfigEntry] private static Address GcAddress { get; set; } = new() { Ip = "224.5.23.1", Port = 10003 };

    private readonly UdpReceiver<Gc.Referee> _udpReceiver = new(GcAddress, OnData, ModuleName);

    private static void OnData(Gc.Referee data)
    {
        Hub.RawReferee.Publish(data);
    }

    public void Dispose()
    {
        _udpReceiver.Dispose();
    }
}