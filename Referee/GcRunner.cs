using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Module;
using Tyr.Common.Network;
using Gc = Tyr.Common.Data.Ssl.Gc;

namespace Tyr.Referee;

public class GcRunner : UdpRunner<Gc.Referee>
{
    protected override string Name => "Game Controller";

    protected override Address Address => Configs.Network.Referee;

    protected override void OnData(Gc.Referee data)
    {
        Hub.Gc.Publish(data);
    }
}