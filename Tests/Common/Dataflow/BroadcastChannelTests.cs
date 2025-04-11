using Tyr.Common.Dataflow;

namespace Tyr.Tests.Common.Dataflow;

public class BroadcastChannelTests
{
    [Fact]
    public async Task AllSubscribers_Receive_AllPublishedMessages()
    {
        var channel = new BroadcastChannel<int>();
        var reader1 = channel.Subscribe(BroadcastChannel<int>.Mode.All);
        var reader2 = channel.Subscribe(BroadcastChannel<int>.Mode.All);

        channel.Publish(42);
        channel.Publish(99);

        Assert.Equal(42, await reader1.ReadAsync());
        Assert.Equal(99, await reader1.ReadAsync());

        Assert.Equal(42, await reader2.ReadAsync());
        Assert.Equal(99, await reader2.ReadAsync());
    }

    [Fact]
    public async Task LatestSubscriber_Receives_OnlyLatestValue()
    {
        var channel = new BroadcastChannel<string>();
        var reader = channel.Subscribe(BroadcastChannel<string>.Mode.Latest);

        channel.Publish("old");
        channel.Publish("new");

        var result = await reader.ReadAsync();
        Assert.Equal("new", result); // "old" was dropped
    }

    [Fact]
    public async Task MixedSubscribers_RespectModes()
    {
        var channel = new BroadcastChannel<int>();
        var allReader = channel.Subscribe(BroadcastChannel<int>.Mode.All);
        var latestReader = channel.Subscribe(BroadcastChannel<int>.Mode.Latest);

        channel.Publish(1);
        channel.Publish(2);
        channel.Publish(3);

        Assert.Equal(1, await allReader.ReadAsync());
        Assert.Equal(2, await allReader.ReadAsync());
        Assert.Equal(3, await allReader.ReadAsync());

        Assert.Equal(3, await latestReader.ReadAsync()); // only latest survives
    }

    [Fact]
    public void Subscribe_Adds_NewSubscriber()
    {
        var channel = new BroadcastChannel<int>();

        var reader = channel.Subscribe();
        Assert.NotNull(reader);
    }
}