using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public sealed record Subscriber<T>(BroadcastChannel<T> BroadcastChannel, Channel<T> Channel) : IDisposable
{
    public ChannelReader<T> Reader => Channel.Reader;

    public void Dispose()
    {
        BroadcastChannel.Unsubscribe(this);
    }
}