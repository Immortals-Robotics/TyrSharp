using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;
using Gc = Tyr.Common.Data.Ssl.Gc;

namespace Tyr.Referee;

[Configurable]
public sealed partial class GcDataPublisher : IDisposable
{
    [ConfigEntry("Address where referee packets are received from the game controller")]
    public static partial Address GcAddress { get; set; } = new() { Ip = "224.5.23.1", Port = 10003 };

    private readonly UdpReceiver<Gc.Referee> _udpReceiver;

    public GcDataPublisher()
    {
        _udpReceiver = new UdpReceiver<Gc.Referee>(GcAddress, OnData, "Gc");

        Configurable.OnUpdated += _ =>
        {
            _udpReceiver?.ChangeAddress(GcAddress);
        };
    }

    private void OnData(Gc.Referee data)
    {
        Hub.RawReferee.Publish(data);
    }

    public void Dispose()
    {
        _udpReceiver.Dispose();
    }
}
