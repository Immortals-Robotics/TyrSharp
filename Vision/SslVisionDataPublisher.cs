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

    private readonly UdpReceiver<WrapperPacket> _udpReceiver;

    public SslVisionDataPublisher()
    {
        Configurable.OnUpdated += _ => { _udpReceiver?.ChangeAddress(Address); };

        _udpReceiver = new UdpReceiver<WrapperPacket>(Address, OnData, "SslVision");
        Log.ZLogInformation($"SSL Vision Data publisher initialized on {Address}.");
    }

    private void OnData(WrapperPacket data)
    {
        if (data.Detection != null)
        {
            data.Detection.Timestamp = data.Detection.CaptureTime;
            data.Detection.Meta = Common.Debug.Meta.GetOrCreate("Vision", "SslDetection");
            Hub.RawDetection.Publish(data.Detection);
        }

        if (data.Geometry != null)
        {
            var timestamp = data.Detection?.CaptureTime ?? Tyr.Common.Time.Timestamp.Now;
            var meta = Common.Debug.Meta.GetOrCreate("Vision", "SslGeometry");

            var field = data.Geometry.Field;
            field.Timestamp = timestamp;
            field.Meta = meta;
            Hub.FieldSize.Publish(field);

            foreach (var calib in data.Geometry.Calibrations)
            {
                var calibration = calib;
                calibration.Timestamp = timestamp;
                calibration.Meta = meta;
                Hub.CameraCalibration.Publish(calibration);
            }

            if (data.Geometry.BallModels.HasValue)
            {
                var ballModels = data.Geometry.BallModels.Value;
                ballModels.Timestamp = timestamp;
                ballModels.Meta = meta;
                Hub.BallModels.Publish(ballModels);
            }
        }
    }

    public void Dispose()
    {
        _udpReceiver.Dispose();
    }
}
