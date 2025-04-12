using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public class BroadcastChannel<T>
{
    private readonly List<Channel<T>> _subscribers = [];

    public enum Mode
    {
        Latest,
        All,
    }

    public Subscriber<T> Subscribe(Mode mode = Mode.All)
    {
        var channel = mode switch
        {
            Mode.All => Channel.CreateUnbounded<T>(new UnboundedChannelOptions()
            {
                SingleWriter = true, SingleReader = true,
            }),
            Mode.Latest => Channel.CreateBounded<T>(new BoundedChannelOptions(1)
            {
                SingleWriter = true, SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest,
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        lock (_subscribers)
        {
            _subscribers.Add(channel);
        }

        return new Subscriber<T>(this, channel.Reader);
    }

    public void Unsubscribe(Subscriber<T> subscriber)
    {
        lock (_subscribers)
        {
            _subscribers.RemoveAll(channel => channel.Reader == subscriber.Reader);
        }
    }

    public void Publish(T item)
    {
        lock (_subscribers)
        {
            foreach (var subscriber in _subscribers)
            {
                var result = subscriber.Writer.TryWrite(item);
                if (!result) Logger.ZLogError($"Failed to publish item to channel");
            }
        }
    }
}