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

    public bool ReceiveRaw(out ReadOnlySpan<byte> data)
    {
        data = default;

        try
        {
            _lastReceiveEndpoint = new IPEndPoint(IPAddress.Any, 0);
            data = _socket.Receive(ref _lastReceiveEndpoint);
            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
        {
            return false;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UDP receive error: {ex.Message}");
            return false;
        }
    }

    public bool Receive<T>(out T? message) where T : class
    {
        message = null;

        if (!ReceiveRaw(out var data))
            return false;

        try
        {
            using var ms = new MemoryStream(data.ToArray());
            message = Serializer.Deserialize<T>(ms);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to deserialize Protobuf-net message: {ex.Message}");
            return false;
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