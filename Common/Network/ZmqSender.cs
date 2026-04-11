using NetMQ;
using NetMQ.Sockets;
using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;

namespace Tyr.Common.Network;

[Configurable]
public sealed partial class ZmqSender : IDisposable
{
    [ConfigEntry] private static int MaxPacketSize { get; set; } = 64 * 1024;

    private readonly PublisherSocket _socket;
    private readonly byte[] _buffer;
    private readonly byte[] _topicBuffer = new byte[1];

    public ZmqSender(Address address)
    {
        _buffer = new byte[MaxPacketSize];
        _socket = new PublisherSocket();
        _socket.Bind($"tcp://{address}");
        Log.ZLogInformation($"ZMQ (proto) publisher bound to {address}");
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
