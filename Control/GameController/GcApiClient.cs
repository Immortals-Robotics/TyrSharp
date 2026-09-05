using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Tyr.Control.GameController;

public sealed class GcApiClient : IDisposable
{
    public enum ConnectionState { Disconnected, Connecting, Connected, Error }

    private volatile ConnectionState _state = ConnectionState.Disconnected;
    private string _lastError = "";

    // Latest state received from the GC — written by background task, read by UI
    private volatile GcMatchState? _matchState;
    private volatile GcStateData?  _gcState;

    private readonly ConcurrentQueue<string> _outgoing = new();
    private CancellationTokenSource? _cts;
    private Thread? _thread;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public ConnectionState State      => _state;
    public string          LastError  => _lastError;
    public GcMatchState?   MatchState => _matchState;
    public GcStateData?    GcState    => _gcState;

    public void Connect(string host, int port)
    {
        Disconnect();
        _matchState = null;
        _gcState    = null;
        _lastError  = "";
        _state      = ConnectionState.Connecting;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _thread = new Thread(() => RunAsync(host, port, ct).GetAwaiter().GetResult())
        {
            IsBackground = true,
            Name = "GcApi",
        };
        _thread.Start();
    }

    public void Disconnect()
    {
        _cts?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _cts?.Dispose();
        _cts = null;
        _thread = null;
        if (_state != ConnectionState.Error)
            _state = ConnectionState.Disconnected;
    }

    public void Send(GcInput input)
    {
        var json = JsonSerializer.Serialize(input, JsonOpts);
        _outgoing.Enqueue(json);
    }

    // ── background task ───────────────────────────────────────────────────────

    private async Task RunAsync(string host, int port, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        try
        {
            await ws.ConnectAsync(new Uri($"ws://{host}:{port}/api/control"), ct);
            _state = ConnectionState.Connected;

            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var readTask  = ReadLoop(ws, loopCts.Token);
            var writeTask = WriteLoop(ws, loopCts.Token);
            await Task.WhenAny(readTask, writeTask);
            loopCts.Cancel();
            try { await Task.WhenAll(readTask, writeTask); } catch { /* expected */ }
        }
        catch (Exception ex) when (ct.IsCancellationRequested ||
                                   ex is OperationCanceledException)
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _state     = ConnectionState.Error;
            return;
        }

        if (_state == ConnectionState.Connected)
            _state = ConnectionState.Disconnected;
    }

    private async Task ReadLoop(ClientWebSocket ws, CancellationToken ct)
    {
        var buf = new byte[65536];
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buf, ct);
                    ms.Write(buf, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close) return;

                var json = Encoding.UTF8.GetString(ms.ToArray());
                var output = JsonSerializer.Deserialize<GcOutput>(json);
                if (output?.MatchState != null) _matchState = output.MatchState;
                if (output?.GcState    != null) _gcState    = output.GcState;
            }
            catch (OperationCanceledException) { return; }
            catch { return; }
        }
    }

    private async Task WriteLoop(ClientWebSocket ws, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
        {
            try
            {
                if (_outgoing.TryDequeue(out var json))
                {
                    var bytes = Encoding.UTF8.GetBytes(json);
                    await ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
                }
                else
                {
                    await Task.Delay(50, ct);
                }
            }
            catch (OperationCanceledException) { return; }
            catch { return; }
        }
    }

    public void Dispose() => Disconnect();
}
