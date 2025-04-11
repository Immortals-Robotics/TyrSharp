using System.Net;
using System.Net.Sockets;
using ProtoBuf;

namespace Tyr.Common.Network;

public class UdpClient
{
    private readonly System.Net.Sockets.UdpClient _socket;
    private IPEndPoint _listenEndpoint;
    private IPEndPoint? _lastReceiveEndpoint;
    private IPAddress _multicastAddress;

    private readonly byte[] _buffer = new byte[Config.Network.MaxUdpPacketSize];

    public UdpClient(Address address)
    {
        _listenEndpoint = new IPEndPoint(IPAddress.Any, address.Port);

        _socket = new System.Net.Sockets.UdpClient();
        _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Client.Bind(_listenEndpoint);

        _multicastAddress = IPAddress.Parse(address.Ip);
        // TODO: c++ code checks if the address is multicast
        _socket.JoinMulticastGroup(_multicastAddress);

        _socket.Client.Blocking = false;
    }

    public bool IsDataAvailable(int timeoutMicroSeconds = 1000)
    {
        return _socket.Client.Poll(1000, SelectMode.SelectRead);
    }

    public async Task<ReadOnlyMemory<byte>> ReceiveRaw(CancellationToken token = default)
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
            Logger.ZLogError(ex, $"UDP async receive error");
            return ReadOnlyMemory<byte>.Empty;
        }
    }

    public async Task<T?> Receive<T>(CancellationToken token) where T : class
    {
        var data = await ReceiveRaw(token);
        if (data.IsEmpty)
            return null;

        try
        {
            return Serializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            Logger.ZLogError(ex, $"Failed to deserialize Protobuf-net message");
            return null;
        }
    }

    public Address GetListenEndpoint()
    {
        return new Address
        {
            Ip = _listenEndpoint.Address.ToString(),
            Port = _listenEndpoint.Port
        };
    }

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

    public bool IsConnected => _socket.Client?.Connected ?? false;
}