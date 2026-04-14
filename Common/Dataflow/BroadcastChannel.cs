using System.Threading.Channels;

namespace Tyr.Common.Dataflow;

public class BroadcastChannel<T>
{
    private Channel<T>[] _subscribers = [];

    public Subscriber<T> Subscribe(Mode mode)
    {
        var channel = mode switch
        {
            Mode.All => Channel.CreateUnbounded<T>(new UnboundedChannelOptions()
            {
                SingleWriter = false, SingleReader = true,
            }),
            Mode.Latest => Channel.CreateBounded<T>(new BoundedChannelOptions(1)
            {
                SingleWriter = false, SingleReader = true, FullMode = BoundedChannelFullMode.DropOldest,
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null)
        };

        while (true)
        {
            var current = Volatile.Read(ref _subscribers);
            var next = new Channel<T>[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[^1] = channel;

            if (Interlocked.CompareExchange(ref _subscribers, next, current) == current)
            {
                break;
            }
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
        while (true)
        {
            var current = Volatile.Read(ref _subscribers);
            var index = Array.IndexOf(current, subscriber.Channel);
            if (index < 0)
            {
                return;
            }

            var next = new Channel<T>[current.Length - 1];
            Array.Copy(current, 0, next, 0, index);
            Array.Copy(current, index + 1, next, index, current.Length - index - 1);

            // Best effort only: one or more concurrent publishers may have already captured
            // the old snapshot and can still deliver after this unsubscribe succeeds.
            if (Interlocked.CompareExchange(ref _subscribers, next, current) == current)
            {
                return;
            }
        }
    }

    public void Publish(T item)
    {
        var subscribers = Volatile.Read(ref _subscribers);
        foreach (var subscriber in subscribers)
        {
            var result = subscriber.Writer.TryWrite(item);
            if (!result) Log.ZLogError($"Failed to publish item to channel");
        }
    }
}
