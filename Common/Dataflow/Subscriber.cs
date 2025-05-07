using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public sealed record Subscriber<T> : IDisposable
{
    public required BroadcastChannel<T> BroadcastChannel { get; init; }
    public required Channel<T> Channel { get; init; }
    public required Mode Mode { get; init; }

    public ChannelReader<T> Reader => Channel.Reader;

    private readonly List<T> _list = [];

    public IReadOnlyList<T> All()
    {
        Assert.AreEqual(Mode.All, Mode);

        _list.Clear();
        while (Reader.TryRead(out var item))
        {
            _list.Add(item);
        }

        return _list;
    }

    public void Dispose()
    {
        BroadcastChannel.Unsubscribe(this);
    }
}