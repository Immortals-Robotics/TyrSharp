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
    [ConfigEntry] public static partial DeltaTime PollTimeout { get; set; } = DeltaTime.FromMilliseconds(10);
    [ConfigEntry] public static partial DeltaTime WatchdogTimeout { get; set; } = DeltaTime.FromSeconds(5);
}

// Protobuf-based ZMQ subscriber. Use this for proto3 messages (class types).
// Supply a custom deserializer to handle non-standard framing (e.g. a leading topic byte).
public sealed class ZmqReceiver<T> : IDisposable where T : class
{
    private readonly Action<T> _onData;
    private readonly Func<IReadOnlyList<byte[]>, T> _deserializer;
    private SubscriberSocket? _socket;
    public string CurrentEndpoint => _currentEndpoint;
    private string _currentEndpoint;
    private volatile string? _newEndpoint;
    private List<byte[]> _frames = [];

    private Timestamp _lastReceivedTime;
    private bool _isRecovering;
    private bool _hasReceivedFirstPacket;

    private RunnerSync Runner { get; }

    public ZmqReceiver(Address address, Action<T> onData, string? callingModule = null,
        Func<IReadOnlyList<byte[]>, T>? deserializer = null)
        : this($"tcp://{address}", onData, callingModule, deserializer)
    {
    }

    public ZmqReceiver(string endpoint, Action<T> onData, string? callingModule = null,
        Func<IReadOnlyList<byte[]>, T>? deserializer = null)
    {
        _onData = onData;
        _deserializer = deserializer ?? (frames =>
        {
            // Default deserializer assumes frame 0 is the topic and frame 1 is the protobuf payload.
            return Serializer.Deserialize<T>(new ReadOnlySpan<byte>(frames[1]));
        });
        _currentEndpoint = endpoint;

        Log.ZLogDebug($"Initializing ZmqReceiver<{typeof(T).Name}> for {endpoint}");
        _lastReceivedTime = Timestamp.Now;
        _hasReceivedFirstPacket = false;

        Connect(endpoint);

        Runner = new RunnerSync(Tick, 0, callingModule);
        Runner.Start();
    }

    public void ChangeAddress(Address address)
    {
        ChangeEndpoint($"tcp://{address}");
    }

    public void ChangeEndpoint(string endpoint)
    {
        Log.ZLogDebug($"Changing ZmqReceiver<{typeof(T).Name}> endpoint from {_currentEndpoint} to {endpoint}");
        _newEndpoint = endpoint;
    }

    private void Connect(string endpoint)
    {
        _socket?.Dispose();
        _socket = new SubscriberSocket();
        _socket.Connect(endpoint);
        _socket.SubscribeToAnyTopic();
        Log.ZLogInformation($"ZMQ (proto) connected to {endpoint}");
    }

    private void ResetSocket()
    {
        Log.ZLogTrace($"Resetting SubscriberSocket for {typeof(T).Name} on {_currentEndpoint}");
        Connect(_currentEndpoint);
        _lastReceivedTime = Timestamp.Now;
    }

    private bool Tick()
    {
        try
        {
            if (_newEndpoint != null)
            {
                _currentEndpoint = _newEndpoint;
                ResetSocket();
                _newEndpoint = null;
                _hasReceivedFirstPacket = false;
                return false;
            }

            if (_socket == null) return false;

            _frames.Clear();
            if (!_socket.TryReceiveMultipartBytes(ZmqReceiverConfigs.PollTimeout.ToTimeSpan(), ref _frames))
            {
                if (_hasReceivedFirstPacket)
                {
                    var timeSinceLastPacket = Timestamp.Now - _lastReceivedTime;
                    if (timeSinceLastPacket > ZmqReceiverConfigs.WatchdogTimeout)
                    {
                        if (!_isRecovering)
                        {
                            Log.ZLogWarning($"ZMQ Watchdog triggered for {typeof(T).Name}. No data received for {timeSinceLastPacket.Seconds:F2}s. Reconnecting to {_currentEndpoint}");
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
                    Log.ZLogInformation($"ZMQ Stream started for {typeof(T).Name}. First packet received from {_currentEndpoint}");
                    _hasReceivedFirstPacket = true;
                }

                if (_isRecovering)
                {
                    Log.ZLogInformation($"ZMQ Watchdog recovered for {typeof(T).Name}. Data is flowing again from {_currentEndpoint}");
                    _isRecovering = false;
                }

                _lastReceivedTime = Timestamp.Now;
                Log.ZLogTrace($"Received {typeof(T).Name} from {_currentEndpoint}");
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
