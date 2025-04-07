using System.Net;
using System.Net.Sockets;
using ProtoBuf;

namespace Tyr.Common.Network;

public class UdpServer
{
    private readonly System.Net.Sockets.UdpClient _socket = new(AddressFamily.InterNetwork);
    private readonly byte[] _buffer = new byte[Config.Network.MaxUdpPacketSize];

    public Span<byte> GetBuffer()
    {
        return _buffer;
    }

    public bool Send<T>(T message, Address address) where T : class
    {
        try
        {
            using var ms = new MemoryStream(_buffer);
            Serializer.Serialize(ms, message);

            return Send((int)ms.Position, address);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UdpServer] Serialization failed: {ex.Message}");
            return false;
        }
    }

    public bool Send(int size, Address address)
    {
        try
        {
            var ip = IPAddress.Parse(address.Ip);
            var endpoint = new IPEndPoint(ip, address.Port);
            _socket.Send(_buffer, size, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[UdpServer] Send failed: {ex.Message}");
            return false;
        }
    }
}