using NetMQ;
using NetMQ.Sockets;
using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Network;

[Configurable]
public static partial class ZmqReceiverConfigs
{
    [ConfigEntry] public static DeltaTime PollTimeout { get; set; } = DeltaTime.FromMilliseconds(10);
    [ConfigEntry] public static DeltaTime WatchdogTimeout { get; set; } = DeltaTime.FromSeconds(5);
}

// Protobuf-based ZMQ subscriber. Use this for proto3 messages (class types).
// Supply a custom deserializer to handle non-standard framing (e.g. a leading topic byte).
public sealed class ZmqReceiver<T> : IDisposable where T : class
{
    private readonly Action<T> _onData;
    private readonly Func<IReadOnlyList<byte[]>, T> _deserializer;
    private SubscriberSocket? _socket;
    public Address CurrentAddress => _currentAddress;
    private Address _currentAddress;
    private volatile Address? _newAddress;
    private List<byte[]> _frames = [];

    private Timestamp _lastReceivedTime;
    private bool _isRecovering;
    private bool _hasReceivedFirstPacket;

    private RunnerSync Runner { get; }

    public ZmqReceiver(Address address, Action<T> onData, string? callingModule = null,
        Func<IReadOnlyList<byte[]>, T>? deserializer = null)
    {
        _onData = onData;
        _deserializer = deserializer ?? (frames =>
        {
            // Default deserializer assumes frame 0 is a 1-byte topic (which we skip here)
            // and frame 1 is the protobuf payload.
            var payloadFrame = frames.Count > 1 ? 1 : 0;
            return Serializer.Deserialize<T>(new ReadOnlySpan<byte>(frames[payloadFrame]));
        });
        _currentAddress = address;

        Log.ZLogDebug($"Initializing ZmqReceiver<{typeof(T).Name}> for {address}");
        _lastReceivedTime = Timestamp.Now;
        _hasReceivedFirstPacket = false;

        Connect(address);

        Runner = new RunnerSync(Tick, 0, callingModule);
        Runner.Start();
    }

    public void ChangeAddress(Address address)
    {
        Log.ZLogDebug($"Changing ZmqReceiver<{typeof(T).Name}> address from {_currentAddress} to {address}");
        _newAddress = address;
    }

    private void Connect(Address address)
    {
        _socket?.Dispose();
        _socket = new SubscriberSocket();
        // ZMQ SUB sockets need to connect to PUB sockets
        _socket.Connect($"tcp://{address}");
        _socket.SubscribeToAnyTopic();
        Log.ZLogInformation($"ZMQ (proto) connected to {address}");
    }

    private void ResetSocket()
    {
        Log.ZLogTrace($"Resetting SubscriberSocket for {typeof(T).Name} on {_currentAddress}");
        Connect(_currentAddress);
        _lastReceivedTime = Timestamp.Now;
    }

    private bool Tick()
    {
        try
        {
            if (_newAddress != null)
            {
                _currentAddress = _newAddress;
                ResetSocket();
                _newAddress = null;
                _hasReceivedFirstPacket = false; // Reset for the new address
                return false;
            }

            if (_socket == null) return false;

            _frames.Clear();
            if (!_socket.TryReceiveMultipartBytes(ZmqReceiverConfigs.PollTimeout.ToTimeSpan(), ref _frames))
            {
                // Only trigger watchdog if we have received at least one packet from this source
                if (_hasReceivedFirstPacket)
                {
                    var timeSinceLastPacket = Timestamp.Now - _lastReceivedTime;
                    if (timeSinceLastPacket > ZmqReceiverConfigs.WatchdogTimeout)
                    {
                        if (!_isRecovering)
                        {
                            Log.ZLogWarning($"ZMQ Watchdog triggered for {typeof(T).Name}. No data received for {timeSinceLastPacket.Seconds:F2}s. Reconnecting to {_currentAddress}");
                            _isRecovering = true;
                        }
                        else
                        {
                            Log.ZLogTrace($"ZMQ Watchdog still active for {typeof(T).Name} ({timeSinceLastPacket.Seconds:F2}s). Reconnecting...");
                        }
                        
                        ResetSocket();
                    }
                }
                return false;
            }

            try
            {
                var data = _deserializer(_frames);
                
                if (!_hasReceivedFirstPacket)
                {
                    Log.ZLogInformation($"ZMQ Stream started for {typeof(T).Name}. First packet received from {_currentAddress}");
                    _hasReceivedFirstPacket = true;
                }

                if (_isRecovering)
                {
                    Log.ZLogInformation($"ZMQ Watchdog recovered for {typeof(T).Name}. Data is flowing again from {_currentAddress}");
                    _isRecovering = false;
                }

                _lastReceivedTime = Timestamp.Now;
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
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Error in ZmqReceiver<{typeof(T).Name}> Tick");
            Thread.Sleep(100);
            return false;
        }
    }

    public void Dispose()
    {
        Runner.Stop();
        _socket?.Dispose();
    }
}
