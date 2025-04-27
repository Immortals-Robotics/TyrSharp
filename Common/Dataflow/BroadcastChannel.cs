using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public class BroadcastChannel<T>
{
    private readonly List<Channel<T>> _subscribers = [];
    private readonly Lock _lock = new();

    public Subscriber<T> Subscribe(Mode mode)
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

        lock (_lock)
        {
            _subscribers.Add(channel);
        }

        return new Subscriber<T>
        {
            BroadcastChannel = this,
            Channel = channel,
            Mode = mode
        };
    }

    public void Unsubscribe(Subscriber<T> subscriber)
    {
        lock (_lock)
        {
            _subscribers.Remove(subscriber.Channel);
        }
    }

    public void Publish(T item)
    {
        lock (_lock)
        {
            foreach (var subscriber in _subscribers)
            {
                var result = subscriber.Writer.TryWrite(item);
                if (!result) Log.ZLogError($"Failed to publish item to channel");
            }
        }
    }
}