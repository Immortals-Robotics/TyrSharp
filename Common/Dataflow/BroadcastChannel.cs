using System.Threading.Channels;
using Tyr.Common.Debug;
using ZLogger;

namespace Tyr.Common.Dataflow;

public class BroadcastChannel<T>
{
    private readonly List<Channel<T>> _subscribers = [];

    public ChannelReader<T> Subscribe()
    {
        var channel = Channel.CreateUnbounded<T>(new UnboundedChannelOptions()
        {
            SingleWriter = true,
            SingleReader = true,
        });

        lock (_subscribers)
        {
            _subscribers.Add(channel);
        }

        return channel.Reader;
    }

    public void Publish(T item)
    {
        lock (_subscribers)
        {
            foreach (var subscriber in _subscribers)
            {
                var result = subscriber.Writer.TryWrite(item);
                if (!result) Log.Logger.ZLogError($"Failed to publish item to channel");
            }
        }
    }
}