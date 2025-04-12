using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public sealed class Subscriber<T>(BroadcastChannel<T> channel, ChannelReader<T> reader) : IDisposable
{
    public ChannelReader<T> Reader => reader;

    public void Dispose()
    {
        channel.Unsubscribe(this);
    }
}