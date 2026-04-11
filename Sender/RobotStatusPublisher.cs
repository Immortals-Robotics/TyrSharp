using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;

namespace Tyr.Sender;

[Configurable]
public sealed partial class RobotStatusPublisher : IDisposable
{
    [ConfigEntry] private static Address RobotStatusAddress { get; set; } = new() { Ip = "localhost", Port = 5555 };
    [ConfigEntry] private static int TickRateHz { get; set; } = 100;

    private readonly ZmqReceiver<StatusUpdate> _zmqReceiver;

    public RobotStatusPublisher()
    {
        _zmqReceiver = new ZmqReceiver<StatusUpdate>(RobotStatusAddress, OnData, "RobotStatus",
            deserializer: Deserialize);

        Configurable.OnUpdated += _ =>
        {
            _zmqReceiver?.ChangeAddress(RobotStatusAddress);
        };

        Log.ZLogInformation($"Robot Status publisher initialized on {RobotStatusAddress}.");
    }

    // Multipart frame layout: frame[0] = 1-byte StatusTopic, frame[1] = protobuf payload
    private static StatusUpdate Deserialize(IReadOnlyList<byte[]> frames)
    {
        if (frames.Count < 2 || frames[0].Length < 1)
            throw new InvalidDataException($"Unexpected ZMQ frame count or topic size: {frames.Count}, {frames[0].Length}");

        var topic = (StatusTopic)frames[0][0];
        var payload = new ReadOnlySpan<byte>(frames[1]);

        return topic switch
        {
            StatusTopic.StatusTopicImu    => new StatusUpdate { Imu    = Serializer.Deserialize<ImuStatus>(payload) },
            StatusTopic.StatusTopicMotors => new StatusUpdate { Motors = Serializer.Deserialize<MotorStatus>(payload) },
            StatusTopic.StatusTopicPower  => new StatusUpdate { Power  = Serializer.Deserialize<PowerStatus>(payload) },
            StatusTopic.StatusTopicMikona => new StatusUpdate { Mikona = Serializer.Deserialize<MikonaStatus>(payload) },
            StatusTopic.StatusTopicInfo   => new StatusUpdate { Info   = Serializer.Deserialize<RobotInfo>(payload) },
            StatusTopic.StatusTopicDiag   => new StatusUpdate { Diag   = Serializer.Deserialize<DiagStatus>(payload) },
            _ => throw new InvalidDataException($"Unknown StatusTopic: {topic}")
        };
    }

    private void OnData(StatusUpdate data)
    {
        Hub.RobotStatus.Publish(data);
    }

    public void Dispose()
    {
        _zmqReceiver.Dispose();
    }
}
