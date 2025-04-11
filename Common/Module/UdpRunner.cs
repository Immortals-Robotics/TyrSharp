using Tyr.Common.Network;

namespace Tyr.Common.Module;

public abstract class UdpRunner<T> : AsyncRunner where T : class
{
    protected abstract Address Address { get; }

    protected abstract void OnData(T data);

    private readonly UdpClient _client;

    protected UdpRunner()
    {
        _client = new UdpClient(Address);
    }

    protected override async Task Tick(CancellationToken token)
    {
        var packet = await _client.Receive<T>(token);
        if (packet == null)
        {
            Logger.ZLogError($"Received null {typeof(T).Name} packet");
            return;
        }

        Logger.ZLogTrace($"Received {typeof(T).Name} packet from {_client.GetLastReceiveEndpoint()}");

        OnData(packet);
    }
}