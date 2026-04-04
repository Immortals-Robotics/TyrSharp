using NetMQ;
using NetMQ.Sockets;
using ProtoBuf;
using Tyr.Common.Runner;

namespace Tyr.Common.Network;

// Protobuf-based ZMQ subscriber — mirrors ZmqReceiver<T> but deserializes via protobuf-net
// instead of raw binary marshaling. Use this for proto3 messages (class types).
// Supply a custom deserializer to handle non-standard framing (e.g. a leading topic byte).
public sealed class ZmqProtoReceiver<T> : IDisposable where T : class
{
    private readonly Action<T> _onData;
    private readonly Func<byte[][], T> _deserializer;
    private SubscriberSocket? _socket;
    private Address _currentAddress;
    private volatile Address? _newAddress;

    public RunnerSync Runner { get; }

    public ZmqProtoReceiver(Address address, Action<T> onData, string? callingModule = null,
        Func<byte[][], T>? deserializer = null)
    {
        _onData = onData;
        _deserializer = deserializer ?? (frames => Serializer.Deserialize<T>(new ReadOnlySpan<byte>(frames[0])));
        _currentAddress = address;

        Connect(address);

        Runner = new RunnerSync(Tick, 0, callingModule);
        Runner.Start();
    }

    public void ChangeAddress(Address address)
    {
        _newAddress = address;
    }

    private void Connect(Address address)
    {
        _socket?.Dispose();
        _socket = new SubscriberSocket();
        _socket.Connect($"tcp://{address}");
        _socket.SubscribeToAnyTopic();
        Log.ZLogInformation($"ZMQ (proto) connected to {address}");
    }

    private bool Tick()
    {
        if (_newAddress != null)
        {
            Connect(_newAddress);
            _currentAddress = _newAddress;
            _newAddress = null;
            return false;
        }

        if (_socket == null) return false;

        List<byte[]> frames = [];
        if (!_socket.TryReceiveMultipartBytes(ZmqReceiverConfigs.PollTimeout.ToTimeSpan(), ref frames))
            return false;

        try
        {
            var data = _deserializer(frames.ToArray());
            Log.ZLogTrace($"Received {typeof(T).Name} from {_currentAddress}");
            _onData(data);
            return true;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to deserialize {typeof(T).Name}");
            return false;
        }
    }

    public void Dispose()
    {
        Runner.Stop();
        _socket?.Dispose();
    }
}
