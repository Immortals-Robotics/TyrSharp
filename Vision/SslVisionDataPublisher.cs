using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;

namespace Tyr.Vision;

public enum SslVisionPublisherTransport
{
    Udp,
    Zmq
}

[Configurable]
public sealed partial class SslVisionDataPublisher : IDisposable
{
    [ConfigEntry] private static partial SslVisionPublisherTransport Transport { get; set; } = SslVisionPublisherTransport.Udp;
    [ConfigEntry] private static partial Address VisionAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10006 };
    [ConfigEntry(StorageType.User)] private static partial Address SimulatorAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10025 };
    [ConfigEntry] private static partial string ZmqEndpoint { get; set; } = "tcp://127.0.0.1:5570";
    [ConfigEntry(StorageType.User)] private static partial bool UseSimulator { get; set; } = false;

    internal static Address CurrentUdpAddress => UseSimulator ? SimulatorAddress : VisionAddress;
    internal static string CurrentZmqEndpoint => ZmqEndpoint;

    public static int SimulatorVisionPort => SimulatorAddress.Port;

    private readonly IDisposable _receiver;
    private readonly SslVisionPublisherTransport _transport;

    public SslVisionDataPublisher()
    {
        _transport = Transport;
        _receiver = _transport switch
        {
            SslVisionPublisherTransport.Udp => new UdpSslVisionDataPublisher(),
            SslVisionPublisherTransport.Zmq => new ZmqSslVisionDataPublisher(),
            _ => throw new ArgumentOutOfRangeException()
        };

        Configurable.OnUpdated += OnConfigUpdated;
        Log.ZLogInformation($"SSL Vision Data publisher initialized using {_transport} transport.");
    }

    private void OnConfigUpdated(StorageType _)
    {
        if (Transport != _transport)
        {
            Log.ZLogWarning($"SSL Vision transport changed from {_transport} to {Transport}. Restart required to switch transports.");
        }
    }

    public void Dispose()
    {
        Configurable.OnUpdated -= OnConfigUpdated;
        _receiver.Dispose();
    }
}

internal static class SslVisionPacketPublisher
{
    public static void Publish(WrapperPacket data)
    {
        if (data.Detection != null)
            Hub.RawDetection.Publish(data.Detection);

        if (data.Geometry == null)
            return;

        Hub.FieldSize.Publish(data.Geometry.Field);

        foreach (var calibration in data.Geometry.Calibrations)
            Hub.CameraCalibration.Publish(calibration);

        if (data.Geometry.BallModels.HasValue)
        {
            Hub.BallModels.Publish(data.Geometry.BallModels.Value);
        }
    }
}

public sealed class UdpSslVisionDataPublisher : IDisposable
{
    private readonly UdpReceiver<WrapperPacket> _udpReceiver;

    public UdpSslVisionDataPublisher()
    {
        SslVisionDataPublisher.Configurable.OnUpdated += OnConfigUpdated;

        _udpReceiver = new UdpReceiver<WrapperPacket>(SslVisionDataPublisher.CurrentUdpAddress, SslVisionPacketPublisher.Publish, "SslVision");
        Log.ZLogInformation($"UDP SSL Vision Data publisher initialized on {SslVisionDataPublisher.CurrentUdpAddress}.");
    }

    private void OnConfigUpdated(StorageType _)
    {
        _udpReceiver.ChangeAddress(SslVisionDataPublisher.CurrentUdpAddress);
    }

    public void Dispose()
    {
        SslVisionDataPublisher.Configurable.OnUpdated -= OnConfigUpdated;
        _udpReceiver.Dispose();
    }
}

public sealed class ZmqSslVisionDataPublisher : IDisposable
{
    private readonly ZmqReceiver<WrapperPacket> _zmqReceiver;

    public ZmqSslVisionDataPublisher()
    {
        SslVisionDataPublisher.Configurable.OnUpdated += OnConfigUpdated;

        _zmqReceiver = new ZmqReceiver<WrapperPacket>(
            SslVisionDataPublisher.CurrentZmqEndpoint,
            SslVisionPacketPublisher.Publish,
            "SslVision",
            deserializer: Deserialize);
        Log.ZLogInformation($"ZMQ SSL Vision Data publisher initialized on {SslVisionDataPublisher.CurrentZmqEndpoint}.");
    }

    private static WrapperPacket Deserialize(IReadOnlyList<byte[]> frames)
    {
        if (frames.Count == 0)
            throw new InvalidDataException("Expected at least one ZMQ frame for SSL Vision data.");

        var payload = frames[^1];
        return Serializer.Deserialize<WrapperPacket>(new ReadOnlySpan<byte>(payload));
    }

    private void OnConfigUpdated(StorageType _)
    {
        _zmqReceiver.ChangeEndpoint(SslVisionDataPublisher.CurrentZmqEndpoint);
    }

    public void Dispose()
    {
        SslVisionDataPublisher.Configurable.OnUpdated -= OnConfigUpdated;
        _zmqReceiver.Dispose();
    }
}
