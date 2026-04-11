using Tyr.Common.Config;
using Tyr.Common.Runner;
using Tyr.Common.Time;

namespace Tyr.Common.Network;

// need a separate class as UdpReceiver is generic,
// and we don't want a different config per type T 
[Configurable]
public static partial class UdpReceiverConfigs
{
    [ConfigEntry] public static DeltaTime PollTimeout { get; set; } = DeltaTime.FromMilliseconds(1);
    [ConfigEntry] public static DeltaTime WatchdogTimeout { get; set; } = DeltaTime.FromSeconds(5);
}

public sealed class UdpReceiver<T> : IDisposable where T : class
{
    private readonly Action<T> _onData;

    public UdpClient Client { get; private set; }
    public RunnerSync Runner { get; }
    
    private volatile Address? _newAddress;
    private Address _currentAddress;
    private Timestamp _lastReceivedTime;
    private bool _isRecovering;
    private bool _hasReceivedFirstPacket;

    public UdpReceiver(Address address, Action<T> onData, string? callingModule = null)
    {
        _onData = onData;
        _currentAddress = address;
        Log.ZLogDebug($"Initializing UdpReceiver<{typeof(T).Name}> for {address}");
        Client = new UdpClient(address);
        _lastReceivedTime = Timestamp.Now;
        _hasReceivedFirstPacket = false;

        Runner = new RunnerSync(Tick, 0, callingModule);
        Runner.Start();
    }

    public void ChangeAddress(Address address)
    {
        Log.ZLogDebug($"Changing UdpReceiver<{typeof(T).Name}> address from {_currentAddress} to {address}");
        _newAddress = address;
    }

    private void ResetClient()
    {
        Log.ZLogTrace($"Resetting UdpClient for {typeof(T).Name} on {_currentAddress}");
        Client.Dispose();
        Client = new UdpClient(_currentAddress);
        _lastReceivedTime = Timestamp.Now;
        // Keep _hasReceivedFirstPacket as is; if we were already receiving, we want the watchdog to stay active
    }

    private bool Tick()
    {
        try
        {
            if (_newAddress != null)
            {
                _currentAddress = _newAddress;
                ResetClient();
                _newAddress = null;
                _hasReceivedFirstPacket = false; // Reset for the new address
                return false;
            }

            if (!Client.PollData(UdpReceiverConfigs.PollTimeout))
            {
                // Only trigger watchdog if we have received at least one packet from this source
                if (_hasReceivedFirstPacket)
                {
                    var timeSinceLastPacket = Timestamp.Now - _lastReceivedTime;
                    if (timeSinceLastPacket > UdpReceiverConfigs.WatchdogTimeout)
                    {
                        if (!_isRecovering)
                        {
                            Log.ZLogWarning($"UDP Watchdog triggered for {typeof(T).Name}. No data received for {timeSinceLastPacket.Seconds:F2}s. Re-joining group on {_currentAddress}");
                            _isRecovering = true;
                        }
                        else
                        {
                            Log.ZLogTrace($"UDP Watchdog still active for {typeof(T).Name} ({timeSinceLastPacket.Seconds:F2}s). Re-joining...");
                        }
                        
                        ResetClient();
                    }
                }
                return false;
            }

            var packet = Client.Receive<T>();
            if (packet == null)
            {
                Log.ZLogError($"Received null {typeof(T).Name} from {Client.GetLastReceiveEndpoint()}");
                return false;
            }

            if (!_hasReceivedFirstPacket)
            {
                Log.ZLogInformation($"UDP Stream started for {typeof(T).Name}. First packet received from {Client.GetLastReceiveEndpoint()}");
                _hasReceivedFirstPacket = true;
            }

            if (_isRecovering)
            {
                Log.ZLogInformation($"UDP Watchdog recovered for {typeof(T).Name}. Data is flowing again from {Client.GetLastReceiveEndpoint()}");
                _isRecovering = false;
            }

            _lastReceivedTime = Timestamp.Now;
            Log.ZLogTrace($"Received {typeof(T).Name} from {Client.GetLastReceiveEndpoint()}");
            _onData(packet);

            return true;
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"Error in UdpReceiver<{typeof(T).Name}> Tick");
            // If we hit an exception, wait a bit before trying again to avoid spamming the log
            Thread.Sleep(100);
            return false;
        }
    }

    public void Dispose()
    {
        Runner.Stop();
        Client.Dispose();
    }
}