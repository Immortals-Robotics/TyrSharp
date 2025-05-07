using System.Net;
using System.Net.Sockets;
using ProtoBuf;
using Tyr.Common.Config;

namespace Tyr.Common.Network;

[Configurable]
public sealed partial class UdpServer
{
    [ConfigEntry] private static int MaxPacketSize { get; set; } = 64 * 1024;

    private readonly System.Net.Sockets.UdpClient _socket = new(AddressFamily.InterNetwork);
    private readonly byte[] _buffer = new byte[MaxPacketSize];

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
            Log.ZLogError(ex, $"Serialization failed: {ex.Message}");
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
            Log.ZLogError(ex, $"Send failed: {ex.Message}");
            return false;
        }
    }
}