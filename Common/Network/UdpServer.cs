using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using ProtoBuf;
using Tyr.Common.Config;

namespace Tyr.Common.Network;

[Configurable]
public sealed partial class UdpServer : IDisposable
{
    [ConfigEntry] private static int MaxPacketSize { get; set; } = 64 * 1024;

    private readonly Socket _socket;
    private readonly byte[] _buffer;
    private readonly ConcurrentDictionary<Address, IPEndPoint> _endpointCache = new();
    private EndPoint? _lastReceiveEndpoint;

    public UdpServer()
    {
        _buffer = new byte[MaxPacketSize];
        // Create a dual-stack socket if possible, otherwise fallback to IPv4
        _socket = new Socket(SocketType.Dgram, ProtocolType.Udp);
        if (Socket.OSSupportsIPv6)
        {
            try { _socket.DualMode = true; } catch { /* Ignore if not supported */ }
        }
        
        _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
    }

    public void Bind(Address address)
    {
        if (_socket.IsBound)
            return;

        var endpoint = GetEndPoint(address);
        _socket.Bind(endpoint);
        _lastReceiveEndpoint = new IPEndPoint(
            endpoint.AddressFamily == AddressFamily.InterNetworkV6 ? IPAddress.IPv6Any : IPAddress.Any,
            0);
    }

    public Span<byte> GetBuffer()
    {
        return _buffer;
    }

    private IPEndPoint GetEndPoint(Address address)
    {
        if (_endpointCache.TryGetValue(address, out var endpoint))
            return endpoint;

        if (!IPAddress.TryParse(address.Ip, out var ip))
        {
            throw new ArgumentException($"Invalid IP address: {address.Ip}");
        }

        endpoint = new IPEndPoint(ip, address.Port);
        _endpointCache.TryAdd(address, endpoint);
        return endpoint;
    }

    /// <summary>
    /// Serializes the message to the internal buffer and sends it to the specified address.
    /// This method is synchronous.
    /// </summary>
    public bool Send<T>(T message, Address address)
    {
        try
        {
            using var ms = new MemoryStream(_buffer);
            Serializer.Serialize(ms, message);

            return Send((int)ms.Position, address);
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Serialization failed for {typeof(T).Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends the specified number of bytes from the internal buffer to the specified address synchronously.
    /// </summary>
    public bool Send(int size, Address address)
    {
        if (size > _buffer.Length)
        {
            Log.ZLogError($"Send size {size} exceeds buffer length {_buffer.Length}");
            return false;
        }

        try
        {
            var endpoint = GetEndPoint(address);
            _socket.SendTo(_buffer.AsSpan(0, size), SocketFlags.None, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Send failed to {address}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Serializes the message to the internal buffer and sends it to the specified address asynchronously.
    /// Note: This uses the internal shared buffer, so it is NOT thread-safe for concurrent sends.
    /// </summary>
    public async ValueTask<bool> SendAsync<T>(T message, Address address, CancellationToken token = default)
    {
        try
        {
            using var ms = new MemoryStream(_buffer);
            Serializer.Serialize(ms, message);

            return await SendAsync((int)ms.Position, address, token);
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Async serialization failed for {typeof(T).Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sends the specified number of bytes from the internal buffer to the specified address asynchronously.
    /// Note: This uses the internal shared buffer, so it is NOT thread-safe for concurrent sends.
    /// </summary>
    public async ValueTask<bool> SendAsync(int size, Address address, CancellationToken token = default)
    {
        if (size > _buffer.Length)
        {
            Log.ZLogError($"SendAsync size {size} exceeds buffer length {_buffer.Length}");
            return false;
        }

        try
        {
            var endpoint = GetEndPoint(address);
            await _socket.SendToAsync(_buffer.AsMemory(0, size), SocketFlags.None, endpoint, token);
            return true;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Async send failed to {address}: {ex.Message}");
            return false;
        }
    }

    public async Task<T?> ReceiveAsync<T>(CancellationToken token = default) where T : class
    {
        var data = await ReceiveRawAsync(token);
        if (data.IsEmpty)
            return null;

        try
        {
            return Serializer.Deserialize<T>(data.Span);
        }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"UDP receive error");
            return null;
        }
    }

    public async ValueTask<ReadOnlyMemory<byte>> ReceiveRawAsync(CancellationToken token = default)
    {
        if (!_socket.IsBound)
        {
            Log.ZLogWarning($"UDP receive requested before socket was bound");
            return ReadOnlyMemory<byte>.Empty;
        }

        try
        {
            _lastReceiveEndpoint ??= new IPEndPoint(IPAddress.Any, 0);
            var result = await _socket.ReceiveFromAsync(_buffer, SocketFlags.None, _lastReceiveEndpoint, token);
            _lastReceiveEndpoint = result.RemoteEndPoint;
            return _buffer.AsMemory(0, result.ReceivedBytes);
        }
        catch (SocketException ex) when (ex.SocketErrorCode is SocketError.Interrupted or SocketError.OperationAborted)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (OperationCanceledException)
        {
            return ReadOnlyMemory<byte>.Empty;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"UDP receive error");
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    public void Dispose()
    {
        _socket.Dispose();
    }
}
