using System.Threading.Channels;
using ZLogger;

namespace Tyr.Common.Dataflow;

public class BroadcastChannel<T>
{
    private readonly List<Channel<T>> _subscribers = [];

    public enum Mode
    {
        Latest,
        All,
    }

    public ChannelReader<T> Subscribe(Mode mode = Mode.All)
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

        return channel.Reader;
    }

    public Subscription Subscribe(Action<T> onData, Mode mode = Mode.All)
    {
        var reader = Subscribe(mode);
        var cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            await foreach (var item in reader.ReadAllAsync(cts.Token))
            {
                onData(item);
            }
        }, cts.Token);

        return new Subscription(cts);
    }

    public Subscription Subscribe(Func<T, Task> onDataAsync, Mode mode = Mode.All)
    {
        var reader = Subscribe(mode);
        var cts = new CancellationTokenSource();

        _ = Task.Run(async () =>
        {
            await foreach (var item in reader.ReadAllAsync(cts.Token))
            {
                await onDataAsync(item);
            }
        }, cts.Token);

        return new Subscription(cts);
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