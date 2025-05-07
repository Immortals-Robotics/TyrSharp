using System.Net;
using System.Net.Sockets;
using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Time;

namespace Tyr.Common.Network;

/// <summary>
/// A UDP client that can receive both synchronously and asynchronously using Protobuf serialization.
/// Supports multicast groups and non-blocking operations.
/// </summary>
[Configurable]
public sealed partial class UdpClient : IDisposable
{
    [ConfigEntry] private static int MaxPacketSize { get; set; } = 64 * 1024;

    private readonly System.Net.Sockets.UdpClient _socket;
    private readonly IPEndPoint _listenEndpoint;
    private IPEndPoint? _lastReceiveEndpoint;

    private readonly byte[] _buffer = new byte[MaxPacketSize];

    public UdpClient(Address address)
    {
        _listenEndpoint = new IPEndPoint(IPAddress.Any, address.Port);

        _socket = new System.Net.Sockets.UdpClient();
        _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Client.Bind(_listenEndpoint);

        var multicastAddress = IPAddress.Parse(address.Ip);
        // TODO: c++ code checks if the address is multicast
        _socket.JoinMulticastGroup(multicastAddress);

        _socket.Client.Blocking = false;
    }

    /// <summary>
    /// Polls the socket to check if there is data available to read
    /// </summary>
    /// <param name="timeout">Time to wait for data</param>
    /// <returns>True if data is available to read, false otherwise</returns>
    public bool PollData(DeltaTime timeout)
    {
        return _socket.Client.Poll(timeout.ToTimeSpan(), SelectMode.SelectRead);
    }

    private ReadOnlySpan<byte> ReceiveRaw()
    {
        try
        {
            // don't use UdpClient's "receive" as it allocates a new byte[] every time :\
            _lastReceiveEndpoint ??= new IPEndPoint(IPAddress.Any, 0);
            EndPoint tempRemoteEp = _lastReceiveEndpoint;

            var received = _socket.Client.ReceiveFrom(_buffer, MaxPacketSize, 0, ref tempRemoteEp);
            _lastReceiveEndpoint = (IPEndPoint)tempRemoteEp;

            return _buffer.AsSpan(0, received);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
        {
            return default;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"UDP receive error");
            return default;
        }
    }

    /// <summary>
    /// Receives a single message from the UDP socket and attempts to deserialize it to type T using Protobuf serialization
    /// </summary>
    /// <typeparam name="T">The type to deserialize the message into</typeparam>
    /// <returns>The deserialized message, or null if no data was available or deserialization failed</returns>
    public T? Receive<T>() where T : class
    {
        var data = ReceiveRaw();
        if (data.IsEmpty)
            return null;

        try
        {
            return Serializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to deserialize Protobuf-net message");
            return null;
        }
    }

    private async Task<ReadOnlyMemory<byte>> ReceiveRawAsync(CancellationToken token = default)
    {
        try
        {
            // TODO: fix the allocation inside this
            var result = await _socket.ReceiveAsync(token);
            _lastReceiveEndpoint = result.RemoteEndPoint;
            return result.Buffer;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.Interrupted)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"UDP async receive error");
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    /// <summary>
    /// Receives a single message from the UDP socket and attempts to deserialize it to type T using Protobuf serialization asynchronously
    /// </summary>
    /// <typeparam name="T">The type to deserialize the message into</typeparam>
    /// <param name="token">Cancellation token to cancel the receive operation</param>
    /// <returns>The deserialized message, or null if no data was available or deserialization failed</returns>
    public async Task<T?> ReceiveAsync<T>(CancellationToken token) where T : class
    {
        var data = await ReceiveRawAsync(token);
        if (data.IsEmpty)
            return null;

        try
        {
            return Serializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to deserialize Protobuf-net message");
            return null;
        }
    }

    /// <summary>
    /// Gets the endpoint that this client is listening on.
    /// </summary>
    /// <returns>An Address object containing the IP address and port that the client is bound to</returns> 
    public Address GetListenEndpoint()
    {
        return new Address
        {
            Ip = _listenEndpoint.Address.ToString(),
            Port = _listenEndpoint.Port
        };
    }

    /// <summary>
    /// Gets the endpoint that the last received message came from.
    /// </summary>
    /// <returns>An Address object containing the IP address and port of the last message sender, or null if no message has been received yet</returns>
    public Address? GetLastReceiveEndpoint()
    {
        if (_lastReceiveEndpoint == null)
            return null;

        return new Address
        {
            Ip = _lastReceiveEndpoint.Address.ToString(),
            Port = _lastReceiveEndpoint.Port
        };
    }

    public void Dispose()
    {
        _socket.Dispose();
    }
}