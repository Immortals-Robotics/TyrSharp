using Tyr.Common.Network;

namespace Tyr.Sender;

public sealed class RobotDiscoveryPublisher : IDisposable
{
    private readonly RobotDiscovery _discovery = new();

    public void Dispose()
    {
        _discovery.Dispose();
    }
}
