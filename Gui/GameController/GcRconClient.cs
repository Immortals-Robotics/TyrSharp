using System.Collections.Concurrent;
using System.Net.Sockets;
using ProtoBuf;
using Tyr.Common.Data;

namespace Tyr.Gui.GameController;

internal sealed class GcRconClient : IDisposable
{
    public enum ConnectionState { Disconnected, Connecting, Connected, Error }

    private volatile ConnectionState _state = ConnectionState.Disconnected;
    private volatile RemoteControlTeamState? _teamState;
    private string _lastError = "";

    private readonly ConcurrentQueue<RemoteControlToController> _outgoing = new();
    private CancellationTokenSource? _cts;
    private Thread? _thread;

    public ConnectionState State => _state;
    public RemoteControlTeamState? TeamState => _teamState;
    public string LastError => _lastError;

    public void Connect(string host, int port, TeamColor team)
    {
        Disconnect();
        _teamState = null;
        _lastError = "";
        _state = ConnectionState.Connecting;

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        _thread = new Thread(() => RunClient(host, port, team, ct))
        {
            IsBackground = true,
            Name = "GcRcon",
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

    public void Send(RemoteControlToController msg) => _outgoing.Enqueue(msg);

    private void RunClient(string host, int port, TeamColor team, CancellationToken ct)
    {
        using var tcpClient = new TcpClient();
        // Close the socket when cancellation is requested so blocking reads unblock
        using var reg = ct.Register(() => tcpClient.Close());

        try
        {
            tcpClient.Connect(host, port);
            var stream = tcpClient.GetStream();

            // Step 1: read initial greeting (ControllerToRemoteControl with optional token)
            _ = ReadMessage<ControllerToRemoteControl>(stream);

            // Step 2: register as a team (insecure mode — no signature)
            WriteMessage(stream, new RemoteControlRegistration { Team = team });

            // Step 3: read registration reply
            var regReply = ReadMessage<ControllerToRemoteControl>(stream);
            if (regReply?.ControllerReply?.StatusCode == ControllerStatusCode.Rejected)
            {
                _lastError = regReply.ControllerReply.Reason ?? "Registration rejected";
                _state = ConnectionState.Error;
                return;
            }

            _state = ConnectionState.Connected;

            // Step 4: run reader and writer concurrently on the full-duplex stream
            using var loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var readTask = Task.Run(() => ReadLoop(stream, loopCts.Token), loopCts.Token);
            WriteLoop(stream, loopCts.Token);   // blocks until cancelled or error
            loopCts.Cancel();
            readTask.Wait(TimeSpan.FromSeconds(1));
        }
        catch (Exception ex) when (ct.IsCancellationRequested ||
                                   ex is ObjectDisposedException ||
                                   ex is SocketException { SocketErrorCode: SocketError.Interrupted or SocketError.OperationAborted })
        {
            // normal shutdown
        }
        catch (Exception ex)
        {
            _lastError = ex.Message;
            _state = ConnectionState.Error;
            return;
        }

        if (_state == ConnectionState.Connected)
            _state = ConnectionState.Disconnected;
    }

    private void ReadLoop(NetworkStream stream, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var msg = ReadMessage<ControllerToRemoteControl>(stream);
                if (msg?.State != null)
                    _teamState = msg.State;
            }
            catch { return; }
        }
    }

    private void WriteLoop(NetworkStream stream, CancellationToken ct)
    {
        var lastPing = DateTime.UtcNow;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                if (_outgoing.TryDequeue(out var msg))
                {
                    WriteMessage(stream, msg);
                    lastPing = DateTime.UtcNow;
                }
                else if (DateTime.UtcNow - lastPing >= TimeSpan.FromMilliseconds(500))
                {
                    WriteMessage(stream, RemoteControlToController.Ping());
                    lastPing = DateTime.UtcNow;
                }
                else
                {
                    Thread.Sleep(10);
                }
            }
            catch { return; }
        }
    }

    // ── framing helpers ──────────────────────────────────────────────────────

    private static T? ReadMessage<T>(Stream stream) where T : class
    {
        var length = (int)ReadVarint(stream);
        var buffer = new byte[length];
        stream.ReadExactly(buffer);
        return Serializer.Deserialize<T>(buffer.AsSpan());
    }

    private static void WriteMessage<T>(Stream stream, T message)
    {
        using var ms = new MemoryStream();
        Serializer.Serialize(ms, message);
        var bytes = ms.GetBuffer().AsSpan(0, (int)ms.Length);
        WriteVarint(stream, (ulong)bytes.Length);
        stream.Write(bytes);
        stream.Flush();
    }

    private static ulong ReadVarint(Stream stream)
    {
        ulong result = 0;
        int shift = 0;
        while (true)
        {
            int b = stream.ReadByte();
            if (b < 0) throw new EndOfStreamException("Stream closed mid-varint");
            result |= (ulong)(b & 0x7F) << shift;
            if ((b & 0x80) == 0) return result;
            shift += 7;
            if (shift >= 64) throw new InvalidDataException("Varint too large");
        }
    }

    private static void WriteVarint(Stream stream, ulong value)
    {
        Span<byte> buf = stackalloc byte[10];
        int pos = 0;
        do
        {
            buf[pos++] = (byte)((value & 0x7F) | (value > 0x7F ? 0x80u : 0));
            value >>= 7;
        } while (value > 0);
        stream.Write(buf[..pos]);
    }

    public void Dispose() => Disconnect();
}
