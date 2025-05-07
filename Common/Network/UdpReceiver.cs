using Tyr.Common.Config;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Network;

// need a separate class as UdpReceiver is generic,
// and we don't want a different config per type T 
[Configurable]
public static partial class UdpReceiverConfigs
{
    [ConfigEntry] public static DeltaTime PollTimeout { get; set; } = DeltaTime.FromMilliseconds(1);
}

public sealed class UdpReceiver<T> : IDisposable where T : class
{
    private readonly Action<T> _onData;

    public UdpClient Client { get; }
    public RunnerSync Runner { get; }

    public UdpReceiver(Address address, Action<T> onData, string? callingModule = null)
    {
        _onData = onData;
        Client = new UdpClient(address);

        Runner = new RunnerSync(Tick, 0, callingModule);
        Runner.Start();
    }

    private bool Tick()
    {
        if (!Client.PollData(UdpReceiverConfigs.PollTimeout)) return false;

        var packet = Client.Receive<T>();
        if (packet == null)
        {
            Log.ZLogError($"Received null {typeof(T).Name}");
            return false;
        }

        Log.ZLogTrace($"Received {typeof(T).Name} from {Client.GetLastReceiveEndpoint()}");
        _onData(packet);

        return true;
    }

    public void Dispose()
    {
        Runner.Stop();
        Client.Dispose();
    }
}