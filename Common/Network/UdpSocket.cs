using System.Collections.Concurrent;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Time;

namespace Tyr.Common.Network;

[Configurable]
public sealed partial class UdpSocket : IDisposable
{
    [ConfigEntry] private static int MaxPacketSize { get; set; } = 64 * 1024;
    private const int SioUdpConnReset = -1744830452;

    private Socket? _socket;
    private readonly byte[] _buffer = new byte[MaxPacketSize];
    private readonly ConcurrentDictionary<Address, IPEndPoint> _endpointCache = new();
    private EndPoint? _lastReceiveEndpoint;

    public void Bind(Address address)
    {
        if (_socket?.IsBound == true)
            return;

        var endpoint = GetBindEndpoint(address);
        var socket = EnsureSocket(endpoint.AddressFamily);

        socket.Bind(endpoint);
        _lastReceiveEndpoint = endpoint.AddressFamily == AddressFamily.InterNetworkV6
            ? new IPEndPoint(IPAddress.IPv6Any, 0)
            : new IPEndPoint(IPAddress.Any, 0);

        if (TryGetMulticastGroup(address, out var multicastAddress))
        {
            JoinMulticastOnAllInterfaces(socket, multicastAddress);
        }
    }

    public Span<byte> GetBuffer()
    {
        return _buffer;
    }

    public bool Poll(DeltaTime timeout)
    {
        if (_socket == null)
            return false;

        try
        {
            return _socket.Poll(timeout.ToTimeSpan(), SelectMode.SelectRead);
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Socket poll error");
            return false;
        }
    }

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

    public bool Send(int size, Address address)
    {
        if (size > _buffer.Length)
        {
            Log.ZLogError($"Send size {size} exceeds buffer length {_buffer.Length}");
            return false;
        }

        try
        {
            var endpoint = GetRemoteEndpoint(address);
            var socket = EnsureSocketForSend(endpoint.AddressFamily);
            socket.SendTo(_buffer.AsSpan(0, size), SocketFlags.None, endpoint);
            return true;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Send failed to {address}: {ex.Message}");
            return false;
        }
    }

    public ReadOnlySpan<byte> ReceiveRaw()
    {
        if (_socket?.IsBound != true)
        {
            Log.ZLogWarning($"UDP receive requested before socket was bound");
            return default;
        }

        try
        {
            _lastReceiveEndpoint ??= _socket.AddressFamily == AddressFamily.InterNetworkV6
                ? new IPEndPoint(IPAddress.IPv6Any, 0)
                : new IPEndPoint(IPAddress.Any, 0);

            EndPoint remoteEndpoint = _lastReceiveEndpoint;
            var received = _socket.ReceiveFrom(_buffer, _buffer.Length, SocketFlags.None, ref remoteEndpoint);
            _lastReceiveEndpoint = remoteEndpoint;

            return _buffer.AsSpan(0, received);
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
        {
            return default;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
        {
            return default;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"UDP receive error");
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
            return Serializer.Deserialize<T>(data);
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to deserialize Protobuf-net message");
            return null;
        }
    }

    public Address? GetLastReceiveEndpoint()
    {
        if (_lastReceiveEndpoint is not IPEndPoint endpoint)
            return null;

        return new Address
        {
            Ip = endpoint.Address.ToString(),
            Port = endpoint.Port
        };
    }

    public Address? GetLocalEndpoint()
    {
        if (_socket?.LocalEndPoint is not IPEndPoint endpoint)
            return null;

        return new Address
        {
            Ip = endpoint.Address.ToString(),
            Port = endpoint.Port
        };
    }

    private Socket EnsureSocketForSend(AddressFamily endpointFamily)
    {
        if (_socket != null)
            return _socket;

        if (endpointFamily == AddressFamily.InterNetworkV6)
            return EnsureSocket(AddressFamily.InterNetworkV6);

        if (Socket.OSSupportsIPv6)
            return EnsureSocket(AddressFamily.InterNetworkV6, dualMode: true);

        return EnsureSocket(AddressFamily.InterNetwork);
    }

    private Socket EnsureSocket(AddressFamily family, bool dualMode = false)
    {
        if (_socket != null)
            return _socket;

        var socket = new Socket(family, SocketType.Dgram, ProtocolType.Udp)
        {
            Blocking = false
        };

        socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        DisableUdpConnReset(socket);

        if (family == AddressFamily.InterNetworkV6 && Socket.OSSupportsIPv6)
        {
            try
            {
                socket.DualMode = dualMode;
            }
            catch
            {
                // Keep going even if the platform does not allow changing dual mode.
            }
        }

        _socket = socket;
        return socket;
    }

    private IPEndPoint GetBindEndpoint(Address address)
    {
        if (!IPAddress.TryParse(address.Ip, out var ip))
            throw new ArgumentException($"Invalid IP address: {address.Ip}");

        if (IsMulticast(ip))
        {
            return ip.AddressFamily == AddressFamily.InterNetworkV6
                ? new IPEndPoint(IPAddress.IPv6Any, address.Port)
                : new IPEndPoint(IPAddress.Any, address.Port);
        }

        return new IPEndPoint(ip, address.Port);
    }

    private IPEndPoint GetRemoteEndpoint(Address address)
    {
        if (_endpointCache.TryGetValue(address, out var endpoint))
            return endpoint;

        if (!IPAddress.TryParse(address.Ip, out var ip))
            throw new ArgumentException($"Invalid IP address: {address.Ip}");

        endpoint = new IPEndPoint(ip, address.Port);
        _endpointCache.TryAdd(address, endpoint);
        return endpoint;
    }

    private static bool TryGetMulticastGroup(Address address, out IPAddress multicastAddress)
    {
        multicastAddress = IPAddress.None;

        if (!IPAddress.TryParse(address.Ip, out var ip))
            return false;

        if (!IsMulticast(ip))
            return false;

        multicastAddress = ip;
        return true;
    }

    private static bool IsMulticast(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6Multicast;

        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && (bytes[0] & 0xF0) == 0xE0;
    }

    private static void JoinMulticastOnAllInterfaces(Socket socket, IPAddress address)
    {
        var joinedCount = 0;
        var interfaces = NetworkInterface.GetAllNetworkInterfaces();
        Log.ZLogTrace($"Attempting to join multicast group {address} on {interfaces.Length} interfaces...");

        foreach (var nic in interfaces)
        {
            try
            {
                if (nic.OperationalStatus != OperationalStatus.Up) continue;
                if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                if (!nic.SupportsMulticast) continue;

                var props = nic.GetIPProperties();
                if (!props.UnicastAddresses.Any(ua => ua.Address.AddressFamily == address.AddressFamily))
                    continue;

                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    IPv4InterfaceProperties? ipv4Props;
                    try { ipv4Props = props.GetIPv4Properties(); }
                    catch (NetworkInformationException) { continue; }

                    if (ipv4Props == null) continue;
                    socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
                        new MulticastOption(address, ipv4Props.Index));
                    Log.ZLogInformation(
                        $"Successfully joined multicast group {address} on interface {nic.Name} ({nic.Description}, Index: {ipv4Props.Index})");
                }
                else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    IPv6InterfaceProperties? ipv6Props;
                    try { ipv6Props = props.GetIPv6Properties(); }
                    catch (NetworkInformationException) { continue; }

                    if (ipv6Props == null) continue;
                    socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                        new IPv6MulticastOption(address, ipv6Props.Index));
                    Log.ZLogInformation(
                        $"Successfully joined multicast group {address} on interface {nic.Name} ({nic.Description}, Index: {ipv6Props.Index})");
                }
                else
                {
                    continue;
                }

                joinedCount++;
            }
            catch (Exception ex)
            {
                Log.ZLogTrace(ex, $"Failed to join multicast group {address} on interface {nic.Name}");
            }
        }

        if (joinedCount > 0)
        {
            Log.ZLogTrace($"Multicast join complete for {address}. Total interfaces joined: {joinedCount}");
            return;
        }

        Log.ZLogWarning($"No interfaces successfully joined multicast group {address} via specific binding. Trying default join...");

        try
        {
            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership, new MulticastOption(address));
            }
            else if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                socket.SetSocketOption(SocketOptionLevel.IPv6, SocketOptionName.AddMembership,
                    new IPv6MulticastOption(address));
            }

            Log.ZLogInformation($"Joined multicast group {address} on default interface");
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to join multicast group {address} on default interface");
        }
    }

    private static void DisableUdpConnReset(Socket socket)
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            socket.IOControl((IOControlCode)SioUdpConnReset, [0, 0, 0, 0], null);
        }
        catch (NotSupportedException)
        {
            // Some platforms and socket types do not support this Windows-specific control code.
        }
        catch (SocketException ex)
        {
            Log.ZLogTrace(ex, $"Failed to disable UDP connection reset notifications");
        }
    }

    public void Dispose()
    {
        _socket?.Dispose();
    }
}
