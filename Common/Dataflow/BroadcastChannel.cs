using System.Collections.Concurrent;
using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public class BroadcastChannel<T>
{
    private readonly ConcurrentBag<Channel<T>> _subscribers = [];

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

        _subscribers.Add(channel);

        return new Subscriber<T>(this, channel);
    }

    // This is not thread-safe
    public void Unsubscribe(Subscriber<T> subscriber)
    {
        List<Channel<T>> temp = new(_subscribers.Count);

        // drain
        while (_subscribers.TryTake(out var item))
        {
            if (item != subscriber.Channel)
                temp.Add(item);
        }

        // then refill
        foreach (var item in temp)
            _subscribers.Add(item);
    }

    public void Publish(T item)
    {
        foreach (var subscriber in _subscribers)
        {
            var result = subscriber.Writer.TryWrite(item);
            if (!result) Logger.ZLogError($"Failed to publish item to channel");
        }
    }
}