using NetMQ;
using NetMQ.Sockets;
using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;

namespace Tyr.Common.Network;

public enum ZmqMode
{
    Bind,
    Connect
}

[Configurable]
public sealed partial class ZmqSender : IDisposable
{
    [ConfigEntry] private static int MaxPacketSize { get; set; } = 64 * 1024;

    private readonly PublisherSocket _socket;
    private readonly byte[] _buffer;
    private readonly byte[] _topicBuffer = new byte[1];
    private readonly ZmqMode _mode;
    private Address _currentAddress;

    public ZmqSender(Address address, ZmqMode mode = ZmqMode.Bind)
    {
        _buffer = new byte[MaxPacketSize];
        _mode = mode;
        _currentAddress = address;
        _socket = new PublisherSocket();
        
        ApplyAddress(address);
    }

    private void ApplyAddress(Address address)
    {
        var addrString = $"tcp://{address}";
        if (_mode == ZmqMode.Bind)
        {
            _socket.Bind(addrString);
            Log.ZLogInformation($"ZMQ (proto) publisher bound to {address}");
        }
        else
        {
            _socket.Connect(addrString);
            Log.ZLogInformation($"ZMQ (proto) publisher connected to {address}");
        }
    }

    public void ChangeAddress(Address address)
    {
        var oldAddr = $"tcp://{_currentAddress}";
        if (_mode == ZmqMode.Bind) _socket.Unbind(oldAddr);
        else _socket.Disconnect(oldAddr);

        _currentAddress = address;
        ApplyAddress(address);
    }

    /// <summary>
    /// Serializes the message to the internal buffer and sends it as a multipart ZMQ message using a CommandType topic.
    /// </summary>
    public bool Send<T>(CommandType topic, T message) where T : class
    {
        return Send((int)topic, message);
    }

    /// <summary>
    /// Serializes the message to the internal buffer and sends it as a multipart ZMQ message.
    /// Frame 0: 1-byte topic
    /// Frame 1: Protobuf payload
    /// Note: This uses the internal shared buffer, so it is NOT thread-safe for concurrent sends.
    /// </summary>
    public bool Send<T>(int topic, T message) where T : class
    {
        if (topic is < 0 or > 255)
        {
            Log.ZLogError($"Topic {topic} must be a single byte (0-255)");
            return false;
        }

        try
        {
            using var ms = new MemoryStream(_buffer);
            Serializer.Serialize(ms, message);
            var size = (int)ms.Position;

            if (size > _buffer.Length)
            {
                Log.ZLogError($"ZMQ serialization size {size} exceeds buffer length {_buffer.Length}");
                return false;
            }

            // Send multipart message: [Topic (1 byte)][Payload (N bytes)]
            _topicBuffer[0] = (byte)topic;
            _socket.SendMoreFrame(_topicBuffer);
            _socket.SendFrame(_buffer, size);
            
            return true;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"ZMQ Send failed for {typeof(T).Name}: {ex.Message}");
            return false;
        }
    }

    public void Dispose()
    {
        _socket.Dispose();
    }
}
