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

    public ReadOnlySpan<byte> ReceiveRaw()
    {
        if (_socket.Available == 0)
            return default;

        try
        {
            // don't use UdpClient's receive as it allocates a new byte[] every time :\

            _lastReceiveEndpoint ??= new IPEndPoint(IPAddress.Any, 0);
            EndPoint tempRemoteEp = _lastReceiveEndpoint;

            var received = _socket.Client.ReceiveFrom(_buffer, Config.Network.MaxUdpPacketSize, 0, ref tempRemoteEp);
            _lastReceiveEndpoint = (IPEndPoint)tempRemoteEp;

            return _buffer.AsSpan(0, received);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
        {
            return default;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"UDP receive error: {ex.Message}");
            return default;
        }
    }

    public T? Receive<T>() where T : class
    {
        var data = ReceiveRaw();
        if (data.IsEmpty)
            return null;

        try
        {
            using var ms = new MemoryStream(data.ToArray());
            return Serializer.Deserialize<T>(ms);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Failed to deserialize Protobuf-net message: {ex.Message}");
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