using System.Net;
using System.Net.NetworkInformation;
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

    private readonly byte[] _buffer;

    public UdpClient(Address address)
    {
        _buffer = new byte[MaxPacketSize];
        _listenEndpoint = new IPEndPoint(IPAddress.Any, address.Port);

        _socket = new System.Net.Sockets.UdpClient();
        _socket.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        _socket.Client.Bind(_listenEndpoint);

        if (IPAddress.TryParse(address.Ip, out var ipAddress))
        {
            if (IsMulticast(ipAddress))
            {
                JoinMulticastOnAllInterfaces(ipAddress);
            }
        }
        else
        {
            Log.ZLogWarning($"Failed to parse IP address: {address.Ip}");
        }

        _socket.Client.Blocking = false;
    }

    private static bool IsMulticast(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6Multicast;
        }

        var bytes = address.GetAddressBytes();
        return bytes.Length == 4 && (bytes[0] & 0xF0) == 0xE0;
    }

    private void JoinMulticastOnAllInterfaces(IPAddress address)
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
                
                // If the interface has no unicast addresses of the target family, it's not useful
                if (!props.UnicastAddresses.Any(ua => ua.Address.AddressFamily == address.AddressFamily))
                {
                    continue;
                }

                int index;
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    IPv4InterfaceProperties? ipv4Props;
                    try { ipv4Props = props.GetIPv4Properties(); }
                    catch (NetworkInformationException) { continue; }

                    if (ipv4Props == null) continue;
                    index = ipv4Props.Index;
                    _socket.JoinMulticastGroup(address, index);
                }
                else if (address.AddressFamily == AddressFamily.InterNetworkV6)
                {
                    IPv6InterfaceProperties? ipv6Props;
                    try { ipv6Props = props.GetIPv6Properties(); }
                    catch (NetworkInformationException) { continue; }

                    if (ipv6Props == null) continue;
                    index = ipv6Props.Index;
                    _socket.JoinMulticastGroup(index, address);
                }
                else
                {
                    continue;
                }
                
                joinedCount++;
                Log.ZLogInformation($"Successfully joined multicast group {address} on interface {nic.Name} ({nic.Description}, Index: {index})");
            }
            catch (Exception ex)
            {
                // Silently ignore failures on specific interfaces as some might not support the group
                Log.ZLogTrace(ex, $"Failed to join multicast group {address} on interface {nic.Name}");
            }
        }

        if (joinedCount == 0)
        {
            Log.ZLogWarning($"No interfaces successfully joined multicast group {address} via specific binding. Trying default join...");
            try
            {
                _socket.JoinMulticastGroup(address);
                Log.ZLogInformation($"Joined multicast group {address} on default interface");
            }
            catch (Exception ex)
            {
                Log.ZLogError(ex, $"Failed to join multicast group {address} on default interface");
            }
        }
        else
        {
            Log.ZLogTrace($"Multicast join complete for {address}. Total interfaces joined: {joinedCount}");
        }
    }

    /// <summary>
    /// Polls the socket to check if there is data available to read
    /// </summary>
    /// <param name="timeout">Time to wait for data</param>
    /// <returns>True if data is available to read, false otherwise</returns>
    public bool PollData(DeltaTime timeout)
    {
        try
        {
            return _socket.Client.Poll(timeout.ToTimeSpan(), SelectMode.SelectRead);
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Socket poll error");
            return false;
        }
    }

    /// <summary>
    /// Receives a raw packet from the UDP socket.
    /// </summary>
    /// <returns>A span containing the received data, or an empty span if no data was available.</returns>
    public ReadOnlySpan<byte> ReceiveRaw()
    {
        try
        {
            // don't use UdpClient's "receive" as it allocates a new byte[] every time :\
            _lastReceiveEndpoint ??= new IPEndPoint(IPAddress.Any, 0);
            EndPoint tempRemoteEp = _lastReceiveEndpoint;

            var received = _socket.Client.ReceiveFrom(_buffer, _buffer.Length, SocketFlags.None, ref tempRemoteEp);
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

    private async ValueTask<ReadOnlyMemory<byte>> ReceiveRawAsync(CancellationToken token = default)
    {
        try
        {
            _lastReceiveEndpoint ??= new IPEndPoint(IPAddress.Any, 0);
            var result = await _socket.Client.ReceiveFromAsync(_buffer, SocketFlags.None, _lastReceiveEndpoint, token);
            _lastReceiveEndpoint = (IPEndPoint)result.RemoteEndPoint;
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