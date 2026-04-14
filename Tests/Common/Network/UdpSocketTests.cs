using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ProtoBuf;
using Tyr.Common.Network;
using Tyr.Common.Time;

namespace Tyr.Tests.Common.Network;

public sealed class UdpSocketTests
{
    [Fact]
    public void SendAndReceiveRoundTripAcrossTwoSockets()
    {
        using var receiver = new UdpSocket();
        receiver.Bind(new Address { Ip = IPAddress.Loopback.ToString(), Port = GetFreePort() });

        using var sender = new UdpSocket();
        var message = new TestMessage { Id = 7, Text = "hello" };

        var sent = sender.Send(message, receiver.GetLocalEndpoint()!);

        Assert.True(sent);
        var received = ReceiveWithin<TestMessage>(receiver, TimeSpan.FromSeconds(1));

        Assert.NotNull(received);
        Assert.Equal(message.Id, received!.Id);
        Assert.Equal(message.Text, received.Text);
    }

    [Fact]
    public void BoundSocketCanSendAndReceiveOnTheSamePort()
    {
        using var socket = new UdpSocket();
        socket.Bind(new Address { Ip = IPAddress.Loopback.ToString(), Port = GetFreePort() });

        var localEndpoint = socket.GetLocalEndpoint();
        Assert.NotNull(localEndpoint);

        var sent = socket.Send(new TestMessage { Id = 11, Text = "loopback" }, localEndpoint!);

        Assert.True(sent);
        var received = ReceiveWithin<TestMessage>(socket, TimeSpan.FromSeconds(1));

        Assert.NotNull(received);
        Assert.Equal(11, received!.Id);
        Assert.Equal("loopback", received.Text);
    }

    [Fact]
    public void PollZeroReportsPendingPacketState()
    {
        using var receiver = new UdpSocket();
        receiver.Bind(new Address { Ip = IPAddress.Loopback.ToString(), Port = GetFreePort() });

        using var sender = new UdpSocket();

        Assert.False(receiver.Poll(DeltaTime.Zero));

        Assert.True(sender.Send(new TestMessage { Id = 1, Text = "ready" }, receiver.GetLocalEndpoint()!));
        Assert.True(WaitUntil(() => receiver.Poll(DeltaTime.Zero), TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void InvalidPayloadReturnsNull()
    {
        using var receiver = new UdpSocket();
        receiver.Bind(new Address { Ip = IPAddress.Loopback.ToString(), Port = GetFreePort() });

        using var sender = new UdpSocket();
        var buffer = sender.GetBuffer();
        buffer[0] = 0xFF;
        buffer[1] = 0x01;
        buffer[2] = 0x02;

        Assert.True(sender.Send(3, receiver.GetLocalEndpoint()!));
        Assert.True(WaitUntil(() => receiver.Poll(DeltaTime.Zero), TimeSpan.FromSeconds(1)));
        Assert.Null(receiver.Receive<TestMessage>());
    }

    [Fact]
    public void UdpReceiverInvokesCallbackAndCanChangeAddress()
    {
        var firstPort = GetFreePort();
        var secondPort = GetFreePort();

        using var sender = new UdpSocket();
        using var firstReceived = new ManualResetEventSlim(false);
        using var secondReceived = new ManualResetEventSlim(false);

        var receivedPorts = new List<int>();

        using var receiver = new UdpReceiver<TestMessage>(
            new Address { Ip = IPAddress.Loopback.ToString(), Port = firstPort },
            message =>
            {
                lock (receivedPorts)
                {
                    receivedPorts.Add(message.Id);
                }

                if (message.Id == firstPort) firstReceived.Set();
                if (message.Id == secondPort) secondReceived.Set();
            },
            "UdpSocketTests");

        Assert.True(sender.Send(
            new TestMessage { Id = firstPort, Text = "first" },
            new Address { Ip = IPAddress.Loopback.ToString(), Port = firstPort }));
        Assert.True(firstReceived.Wait(TimeSpan.FromSeconds(2)));

        receiver.ChangeAddress(new Address { Ip = IPAddress.Loopback.ToString(), Port = secondPort });

        Assert.True(WaitUntil(() =>
            sender.Send(
                new TestMessage { Id = secondPort, Text = "second" },
                new Address { Ip = IPAddress.Loopback.ToString(), Port = secondPort }) && secondReceived.Wait(25),
            TimeSpan.FromSeconds(2)));

        lock (receivedPorts)
        {
            Assert.Contains(firstPort, receivedPorts);
            Assert.Contains(secondPort, receivedPorts);
        }
    }

    private static T? ReceiveWithin<T>(UdpSocket socket, TimeSpan timeout) where T : class
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (socket.Poll(DeltaTime.FromMilliseconds(10)))
            {
                return socket.Receive<T>();
            }

            Thread.Sleep(1);
        }

        return null;
    }

    private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = Stopwatch.StartNew();
        while (deadline.Elapsed < timeout)
        {
            if (condition())
                return true;

            Thread.Sleep(1);
        }

        return false;
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    [ProtoContract]
    private sealed class TestMessage
    {
        [ProtoMember(1)] public int Id { get; set; }
        [ProtoMember(2)] public string Text { get; set; } = "";
    }
}
