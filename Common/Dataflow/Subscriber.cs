using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public sealed record Subscriber<T> : IDisposable
{
    public required BroadcastChannel<T> BroadcastChannel { get; init; }
    public required Channel<T> Channel { get; init; }
    public required Mode Mode { get; init; }

    public ChannelReader<T> Reader => Channel.Reader;

    public List<T> All()
    {
        Assert.AreEqual(Mode.All, Mode);

        var list = new List<T>();
        while (Reader.TryRead(out var item))
        {
            list.Add(item);
        }

        return list;
    }

    public T? Latest()
    {
        Assert.AreEqual(Mode.Latest, Mode);

        return Reader.TryRead(out var item) ? item : default;
    }

    public void Dispose()
    {
        BroadcastChannel.Unsubscribe(this);
    }
}