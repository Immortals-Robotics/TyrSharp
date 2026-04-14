using System.Buffers.Binary;
using System.Net;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;
using Tyr.Common.Dataflow;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Network;

[Configurable]
public static partial class RobotDiscoveryConfigs
{
    [ConfigEntry] public static int Port { get; set; } = 5669;
    [ConfigEntry] public static DeltaTime Timeout { get; set; } = DeltaTime.FromSeconds(3);
    [ConfigEntry] public static DeltaTime PollTimeout { get; set; } = DeltaTime.FromMilliseconds(100);
}

public sealed class RobotDiscovery : IDisposable
{
    private const uint BeaconMagic = 0x5242434E; // "RBCN"

    private readonly UdpSocket _udpSocket;
    private readonly RunnerSync _runner;
    private readonly Dictionary<int, DiscoveredRobot> _activeRobots = new();
    private readonly object _lock = new();

    public RobotDiscovery()
    {
        var address = new Address { Ip = "0.0.0.0", Port = RobotDiscoveryConfigs.Port };
        _udpSocket = new UdpSocket();
        _udpSocket.Bind(address);
        _runner = new RunnerSync(Tick, 0, "Discovery");
        _runner.Start();
        
        Log.ZLogInformation($"Robot Discovery started on port {RobotDiscoveryConfigs.Port}");
    }

    private bool Tick()
    {
        var anyChanged = false;

        // 1. Receive any pending beacons
        while (_udpSocket.Poll(DeltaTime.Zero))
        {
            var data = _udpSocket.ReceiveRaw();
            if (data.Length < 5) continue;

            // Magic is sent in network byte order (Big Endian)
            var magic = BinaryPrimitives.ReadUInt32BigEndian(data[..4]);
            if (magic != BeaconMagic) continue;

            var robotId = data[4];
            var endpoint = _udpSocket.GetLastReceiveEndpoint();
            if (endpoint == null) continue;

            lock (_lock)
            {
                var now = Timestamp.Now;
                if (!_activeRobots.TryGetValue(robotId, out var existing) ||
                    existing.Ip != endpoint.Ip ||
                    !existing.IsOnline)
                {
                    anyChanged = true;
                    Log.ZLogInformation($"Robot {robotId} discovered at {endpoint.Ip}");
                }

                _activeRobots[robotId] = new DiscoveredRobot
                {
                    Id = robotId,
                    Ip = endpoint.Ip,
                    LastSeen = now,
                    IsOnline = true
                };
            }
        }

        // 2. Check for timeouts
        lock (_lock)
        {
            var now = Timestamp.Now;
            var toRemove = new List<int>();

            foreach (var kvp in _activeRobots)
            {
                if (kvp.Value.IsOnline && (now - kvp.Value.LastSeen) > RobotDiscoveryConfigs.Timeout)
                {
                    _activeRobots[kvp.Key] = kvp.Value with { IsOnline = false };
                    anyChanged = true;
                    toRemove.Add(kvp.Key);
                    Log.ZLogWarning($"Robot {kvp.Key} ({kvp.Value.Ip}) timed out");
                }
            }

            foreach (var id in toRemove)
            {
                _activeRobots.Remove(id);
            }
        }

        if (anyChanged)
        {
            PublishList();
        }

        // Wait a bit to not peg the CPU if no data is coming in
        Thread.Sleep(RobotDiscoveryConfigs.PollTimeout.ToTimeSpan());
        
        return anyChanged;
    }

    private void PublishList()
    {
        List<DiscoveredRobot> list;
        lock (_lock)
        {
            list = _activeRobots.Values.OrderBy(r => r.Id).ToList();
        }

        Hub.DiscoveredRobots.Publish(list);
    }

    public void Dispose()
    {
        _runner.Stop();
        _udpSocket.Dispose();
    }
}
