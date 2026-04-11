using Tyr.Common.Config;
using Tyr.Common.Dataflow;
using Tyr.Common.Time;
using Tyr.Common.Data.Ssl.Vision;
using Tyr.Common.Network;

namespace Tyr.Vision;

[Configurable]
public sealed partial class SslLogPlayer : IDisposable
{
    public static string FilePath { get; set; } = "";
    public static bool IsPlaying { get; set; } = false;
    public static double PlaybackSpeed { get; set; } = 1.0;
    
    [ConfigEntry("Restart playback from start when finished", StorageType.User)] 
    public static bool Loop { get; set; } = true;
    
    [ConfigEntry("Publish data to internal Hub for processing", StorageType.User)] 
    public static bool PublishToHub { get; set; } = true;
    
    [ConfigEntry("Broadcast data over UDP network", StorageType.User)] 
    public static bool PublishToUdp { get; set; } = false;
    
    [ConfigEntry("Target IP for UDP (127.0.0.1 for local, 0.0.0.0 for multicast)", StorageType.User)] 
    public static string UdpAddress { get; set; } = "127.0.0.1";

    // Elapsed time since start of log
    public static double CurrentTimeSeconds { get; set; } = 0;

    private SslLogFile? _logFile;
    private string _loadedPath = "";
    private int _lastIndex = -1;
    private long _lastTickTimestamp;
    private readonly UdpServer _udpServer = new();

    public SslLogPlayer()
    {
        _lastTickTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
    }

    private void CheckFileChange()
    {
        if (FilePath == _loadedPath) return;

        Close();

        if (string.IsNullOrWhiteSpace(FilePath)) return;

        if (!File.Exists(FilePath))
        {
            Log.ZLogWarning($"SSL Log file does not exist: {FilePath}");
            return;
        }

        try
        {
            _logFile = new SslLogFile(FilePath);
            _loadedPath = FilePath;
            Log.ZLogInformation($"Loaded SSL Log: {FilePath} ({_logFile.Duration})");
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Failed to load SSL Log: {FilePath}");
            _loadedPath = "";
        }
    }

    public void Close()
    {
        _logFile?.Dispose();
        _logFile = null;
        _loadedPath = "";
        _lastIndex = -1;
        CurrentTimeSeconds = 0;
    }

    public void Tick()
    {
        CheckFileChange();

        var now = System.Diagnostics.Stopwatch.GetTimestamp();
        var elapsed = (now - _lastTickTimestamp) / (double)System.Diagnostics.Stopwatch.Frequency;
        _lastTickTimestamp = now;

        if (_logFile == null) return;

        if (IsPlaying)
        {
            CurrentTimeSeconds += elapsed * PlaybackSpeed;
            if (CurrentTimeSeconds > _logFile.Duration.Seconds)
            {
                if (Loop)
                {
                    CurrentTimeSeconds = 0;
                    _lastIndex = -1;
                }
                else
                {
                    CurrentTimeSeconds = _logFile.Duration.Seconds;
                    IsPlaying = false;
                }
            }
            else if (CurrentTimeSeconds < 0)
            {
                if (Loop)
                {
                    CurrentTimeSeconds = _logFile.Duration.Seconds;
                    _lastIndex = -1;
                }
                else
                {
                    CurrentTimeSeconds = 0;
                    IsPlaying = false;
                }
            }
        }

        SeekAndPublish(CurrentTimeSeconds);
    }

    private void SeekAndPublish(double timeSeconds)
    {
        if (_logFile == null) return;

        var targetTime = _logFile.StartTime + DeltaTime.FromSeconds(timeSeconds);
        int index = _logFile.FindIndex(targetTime);

        if (index == _lastIndex) return;

        // If we moved forward by a small amount, play all packets in between to ensure consistency.
        // If it's a large jump (scrubbing/looping), just jump to the target packet.
        bool isSequential = _lastIndex != -1 && index > _lastIndex && (index - _lastIndex) < 1000;

        if (isSequential)
        {
            for (int i = _lastIndex + 1; i <= index; i++)
            {
                var packet = _logFile.ReadPacket(i);
                if (packet != null) PublishPacket(packet.Value);
            }
        }
        else
        {
            var packet = _logFile.ReadPacket(index);
            if (packet != null) PublishPacket(packet.Value);
        }

        _lastIndex = index;
    }

    private void PublishPacket((SslLogFile.MessageType Type, object Data) packet)
    {
        if (packet.Data is WrapperPacket vision)
        {
            if (PublishToHub) PublishVision(vision);
            if (PublishToUdp) SendUdpPacket(vision, 10006);
        }
        else if (packet.Data is Tyr.Common.Data.Ssl.Gc.Referee referee)
        {
            if (PublishToHub) Hub.RawReferee.Publish(referee);
            if (PublishToUdp) SendUdpPacket(referee, 10003);
        }
    }

    private void SendUdpPacket<T>(T message, int port)
    {
        string targetIp = UdpAddress;
        if (targetIp == "0.0.0.0")
        {
            targetIp = port switch
            {
                10006 => "224.5.23.2",
                10003 => "224.5.23.1",
                _ => "127.0.0.1"
            };
        }

        _udpServer.Send(message, new Address { Ip = targetIp, Port = port });
    }

    private void PublishVision(WrapperPacket data)
    {
        if (data.Detection != null)
        {
            data.Detection.ExecutionTimestamp = Tyr.Common.Time.Timestamp.Now;
            data.Detection.Meta = Common.Debug.Meta.GetOrCreate("Vision", "SslDetection");
            Hub.RawDetection.Publish(data.Detection);
        }

        if (data.Geometry != null)
        {
            var timestamp = Tyr.Common.Time.Timestamp.Now;
            var meta = Common.Debug.Meta.GetOrCreate("Vision", "SslGeometry");

            var field = data.Geometry.Field;
            field.ExecutionTimestamp = timestamp;
            field.Meta = meta;
            Hub.FieldSize.Publish(field);

            foreach (var calib in data.Geometry.Calibrations)
            {
                var calibration = calib;
                calibration.ExecutionTimestamp = timestamp;
                calibration.Meta = meta;
                Hub.CameraCalibration.Publish(calibration);
            }

            if (data.Geometry.BallModels.HasValue)
            {
                var ballModels = data.Geometry.BallModels.Value;
                ballModels.ExecutionTimestamp = timestamp;
                ballModels.Meta = meta;
                Hub.BallModels.Publish(ballModels);
            }
        }
    }

    public double DurationSeconds => _logFile?.Duration.Seconds ?? 0;

    public void Dispose()
    {
        Close();
        _udpServer.Dispose();
    }
}
