using Tyr.Common.Network;

namespace Tyr.Common.Module;

public abstract class UdpRunner<T> : Runner where T : class
{
    protected abstract Address Address { get; }

    protected abstract void OnData(T data);

    private UdpClient _client = null!;

    protected override void OnStart()
    {
        _client = new UdpClient(Address);
    }

    protected override void OnStop()
    {
    }

    protected override void Tick()
    {
        if (!_client.IsDataAvailable())
        {
            return;
        }

        var packet = _client.Receive<T>();
        if (packet == null)
        {
            Logger.ZLogError($"Received null {Name} packet");
            return;
        }

        Logger.ZLogTrace($"Received {Name} packet from {_client.GetLastReceiveEndpoint()}");

        OnData(packet);
    }
}