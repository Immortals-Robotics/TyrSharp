using System.Buffers.Binary;
using System.Collections.Concurrent;
using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Sender;

[Configurable]
public sealed partial class RobotStatusPublisher : IDisposable
{
    [ConfigEntry] private static int StatusPort { get; set; } = 5670;
    
    private readonly ConcurrentDictionary<int, ZmqReceiver<StatusUpdate>> _receivers = new();
    private readonly HashSet<int> _activeIds = [];
    private readonly Subscriber<IReadOnlyList<DiscoveredRobot>> _discoverySubscriber;
    private readonly RunnerSync _managementRunner;

    public RobotStatusPublisher()
    {
        _discoverySubscriber = Hub.DiscoveredRobots.Subscribe(Mode.Latest);
        _managementRunner = new RunnerSync(ManageConnections, 1, "StatusManager");
        _managementRunner.Start();

        Log.ZLogInformation($"Robot Status publisher initialized (watching discovery on port {StatusPort})");
    }

    private bool ManageConnections()
    {
        if (!_discoverySubscriber.Reader.TryRead(out var robots))
            return false;

        _activeIds.Clear();
        foreach (var robot in robots)
        {
            if (!robot.IsOnline) continue;
            _activeIds.Add(robot.Id);

            if (!_receivers.TryGetValue(robot.Id, out var receiver))
            {
                var address = new Address { Ip = robot.Ip, Port = StatusPort };
                Log.ZLogInformation($"Connecting to status stream for Robot {robot.Id} at {address}");
                
                var robotId = robot.Id; // Capture for closure
                receiver = new ZmqReceiver<StatusUpdate>(
                    address, 
                    update => OnData(robotId, update), 
                    $"Status{robotId}",
                    deserializer: frames => Deserialize(robotId, frames));
                
                _receivers[robot.Id] = receiver;
            }
            else if (receiver.CurrentEndpoint != $"tcp://{robot.Ip}:{StatusPort}")
            {
                // IP changed? Re-connect
                Log.ZLogInformation($"Robot {robot.Id} moved to {robot.Ip}, updating connection");
                receiver.ChangeAddress(new Address { Ip = robot.Ip, Port = StatusPort });
            }
        }

        // Clean up timed-out robots
        foreach (var pair in _receivers)
        {
            var id = pair.Key;
            if (!_activeIds.Contains(id) && _receivers.TryRemove(id, out var receiver))
            {
                Log.ZLogInformation($"Closing status stream for Robot {id}");
                receiver.Dispose();
            }
        }

        return true;
    }

    // Multipart frame layout: frame[0] = 1-byte Topic, frame[1] = protobuf payload
    private static StatusUpdate Deserialize(int robotId, IReadOnlyList<byte[]> frames)
    {
        if (frames.Count < 2 || frames[0].Length < 1)
            throw new InvalidDataException($"Unexpected ZMQ frame count or topic size: {frames.Count}, {frames[0].Length}");

        var topic = (StatusTopic)frames[0][0];
        var payload = new ReadOnlySpan<byte>(frames[1]);

        var update = topic switch
        {
            StatusTopic.StatusTopicImu    => new StatusUpdate { Imu    = Serializer.Deserialize<ImuStatus>(payload) },
            StatusTopic.StatusTopicMotors => new StatusUpdate { Motors = Serializer.Deserialize<MotorStatus>(payload) },
            StatusTopic.StatusTopicPower  => new StatusUpdate { Power  = Serializer.Deserialize<PowerStatus>(payload) },
            StatusTopic.StatusTopicMikona => new StatusUpdate { Mikona = Serializer.Deserialize<MikonaStatus>(payload) },
            StatusTopic.StatusTopicInfo   => new StatusUpdate { Info   = Serializer.Deserialize<RobotInfo>(payload) },
            StatusTopic.StatusTopicDiag   => new StatusUpdate { Diag   = Serializer.Deserialize<DiagStatus>(payload) },
            StatusTopic.StatusTopicIrSensor => new StatusUpdate { IrSensor = Serializer.Deserialize<IrSensorStatus>(payload) },
            StatusTopic.StatusTopicDribblerFeedback => new StatusUpdate { DribblerFeedback = Serializer.Deserialize<DribblerFeedbackStatus>(payload) },
            _ => throw new InvalidDataException($"Unknown StatusTopic: {topic}")
        };

        return update with { RobotId = robotId };
    }

    private void OnData(int robotId, StatusUpdate data)
    {
        Hub.RobotStatus.Publish(data);
    }

    public void Dispose()
    {
        _managementRunner.Stop();
        foreach (var receiver in _receivers.Values)
            receiver.Dispose();
        _receivers.Clear();
    }
}
