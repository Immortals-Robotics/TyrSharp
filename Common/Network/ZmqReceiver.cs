using System.Runtime.InteropServices;
using NetMQ;
using NetMQ.Sockets;
using Tyr.Common.Config;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Network;

[Configurable]
public static partial class ZmqReceiverConfigs
{
    [ConfigEntry] public static DeltaTime PollTimeout { get; set; } = DeltaTime.FromMilliseconds(10);
}

public sealed class ZmqReceiver<T> : IDisposable where T : struct
{
    private readonly Action<T> _onData;
    private SubscriberSocket? _socket;
    private Address _currentAddress;
    private volatile Address? _newAddress;

    public RunnerSync Runner { get; }

    public ZmqReceiver(Address address, Action<T> onData, string? callingModule = null)
    {
        _onData = onData;
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
        Log.ZLogInformation($"ZMQ connected to {address}");
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

        if (!_socket.TryReceiveFrameBytes(ZmqReceiverConfigs.PollTimeout.ToTimeSpan(), out var bytes))
            return false;

        try
        {
            var data = BytesToStruct(bytes);
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

    private static T BytesToStruct(byte[] bytes)
    {
        var expected = Marshal.SizeOf<T>();
        if (bytes.Length < expected)
            throw new InvalidDataException(
                $"Expected {expected} bytes for {typeof(T).Name}, got {bytes.Length}");

        var handle = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            return Marshal.PtrToStructure<T>(handle.AddrOfPinnedObject());
        }
        finally
        {
            handle.Free();
        }
    }

    public void Dispose()
    {
        Runner.Stop();
        _socket?.Dispose();
    }
}