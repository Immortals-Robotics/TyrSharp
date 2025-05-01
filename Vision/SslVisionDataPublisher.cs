using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;

namespace Tyr.Vision;

[Configurable]
public sealed partial class SslVisionDataPublisher : IDisposable
{
    [ConfigEntry] private static Address VisionAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10006 };
    [ConfigEntry] private static Address SimulatorAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10025 };
    [ConfigEntry(StorageType.User)] private static bool UseSimulator { get; set; } = false;

    private static Address Address => UseSimulator ? SimulatorAddress : VisionAddress;

    private readonly UdpReceiver<WrapperPacket> _udpReceiver = new(Address, OnData, ModuleName);

    public SslVisionDataPublisher()
    {
        Log.ZLogInformation($"SSL Vision Data publisher initialized on {Address}.");
    }

    private static void OnData(WrapperPacket data)
    {
        if (data.Detection != null)
            Hub.RawDetection.Publish(data.Detection);

        if (data.Geometry != null)
        {
            Hub.FieldSize.Publish(data.Geometry.Field);

            foreach (var calibration in data.Geometry.Calibrations)
                Hub.CameraCalibration.Publish(calibration);

            if (data.Geometry.BallModels.HasValue)
                Hub.BallModels.Publish(data.Geometry.BallModels.Value);
        }
    }

    public void Dispose()
    {
        _udpReceiver.Dispose();
    }
}