using Tyr.Common.Data.Ssl.Vision.Detection;

namespace Tyr.Vision;

public class Camera(uint id)
{
    public uint Id { get; } = id;

    public void OnFrame(Frame frame)
    {
        DateTime now = DateTime.UtcNow;

        var processingTime = frame.SentTime - frame.CaptureTime;
        var networkDelay = now - frame.SentTime;
        var totalDelay = now - frame.CaptureTime;

        Logger.ZLogTrace(
            $"cam {Id} delays: process: {processingTime.TotalMilliseconds}ms, network: {networkDelay.TotalMilliseconds}ms, total: {totalDelay.TotalMilliseconds}ms");
    }
}