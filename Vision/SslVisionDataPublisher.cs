using Tyr.Common.Config;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;

namespace Tyr.Vision;

[Configurable]
public sealed class SslVisionDataPublisher : IDisposable
{
    [ConfigEntry] public static Address VisionAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10006 };
    [ConfigEntry] public static Address VisionSimAddress { get; set; } = new() { Ip = "224.5.23.2", Port = 10025 };

    private readonly UdpReceiver<WrapperPacket> _udpReceiver = new(VisionSimAddress, OnData, ModuleName);

    public SslVisionDataPublisher()
    {
        Log.ZLogInformation($"SSL Vision Data publisher initialized on {VisionSimAddress}.");
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