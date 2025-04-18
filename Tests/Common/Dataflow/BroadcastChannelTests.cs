using Tyr.Common.Dataflow;

namespace Tyr.Tests.Common.Dataflow;

public class BroadcastChannelTests
{
    [Fact]
    public async Task AllSubscribers_Receive_AllPublishedMessages()
    {
        var channel = new BroadcastChannel<int>();
        var subscriber1 = channel.Subscribe(Mode.All);
        var subscriber2 = channel.Subscribe(Mode.All);

        channel.Publish(42);
        channel.Publish(99);

        Assert.Equal(42, await subscriber1.Reader.ReadAsync());
        Assert.Equal(99, await subscriber1.Reader.ReadAsync());

        Assert.Equal(42, await subscriber2.Reader.ReadAsync());
        Assert.Equal(99, await subscriber2.Reader.ReadAsync());
    }

    [Fact]
    public async Task LatestSubscriber_Receives_OnlyLatestValue()
    {
        var channel = new BroadcastChannel<string>();
        var subscriber = channel.Subscribe(Mode.Latest);

        channel.Publish("old");
        channel.Publish("new");

        var result = await subscriber.Reader.ReadAsync();
        Assert.Equal("new", result); // "old" was dropped
    }

    [Fact]
    public async Task MixedSubscribers_RespectModes()
    {
        var channel = new BroadcastChannel<int>();
        var allSubscriber = channel.Subscribe(Mode.All);
        var latestSubscriber = channel.Subscribe(Mode.Latest);

        channel.Publish(1);
        channel.Publish(2);
        channel.Publish(3);

        Assert.Equal(1, await allSubscriber.Reader.ReadAsync());
        Assert.Equal(2, await allSubscriber.Reader.ReadAsync());
        Assert.Equal(3, await allSubscriber.Reader.ReadAsync());

        Assert.Equal(3, await latestSubscriber.Reader.ReadAsync()); // only latest survives
    }

    [Fact]
    public void Subscribe_Adds_NewSubscriber()
    {
        var channel = new BroadcastChannel<int>();

        var reader = channel.Subscribe(Mode.All);
        Assert.NotNull(reader);
    }

    [Fact]
    public async Task Unsubscribe_removes_channel_and_stops_delivery()
    {
        var channel = new BroadcastChannel<int>();
        var subscriber = channel.Subscribe(Mode.All);
        var reader = subscriber.Reader;

        // Confirm initial delivery works
        channel.Publish(42);
        var received = await reader.ReadAsync();
        Assert.Equal(42, received);

        // Unsubscribe and publish again
        subscriber.Dispose(); // calls Unsubscribe internally

        // There should be no more items
        channel.Publish(99);

        // Add a short delay to account for async delivery
        await Task.Delay(10);

        Assert.False(reader.TryRead(out _));
    }
}