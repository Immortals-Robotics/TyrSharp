using System.Text.Json;
using System.Text.Json.Serialization;
using Tyr.Common.Data;
using Tyr.Common.Dataflow;
using Tyr.Common.Referee.Data;
using Tyr.Common.Sender.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;

namespace Tyr.Cli;

/// <summary>
/// Samples the tracker output, referee state and the commands each team publishes at a fixed rate,
/// and records referee transitions as events. Everything is kept in memory and written once as
/// summary.json; the full-resolution record is the debug-db session written alongside it.
/// </summary>
public sealed class WorldSampler : IDisposable
{
    // Mode.All so every frame is counted; the sampler drains it at least at the sample rate.
    private readonly Subscriber<FilteredFrame> _vision = Hub.Vision.Subscribe(Mode.All);
    private readonly Subscriber<State> _referee = Hub.Referee.Subscribe(Mode.Latest);
    private readonly Subscriber<CommandsWrapper> _commands = Hub.Commands.Subscribe(Mode.All);

    private readonly List<Sample> _samples = [];
    private readonly List<Event> _events = [];
    private readonly Lock _sync = new();

    private FilteredFrame? _latestFrame;
    private State? _latestReferee;
    private readonly Dictionary<TeamColor, CommandsWrapper> _latestCommands = new();

    private Thread? _thread;
    private CancellationTokenSource? _cts;
    private Timestamp _start;
    private bool _started;
    private long _framesSeen;

    public long FramesSeen => Interlocked.Read(ref _framesSeen);

    /// <summary>Latest tracker frame, updated by <see cref="Poll"/> and by the sampling thread.</summary>
    public FilteredFrame? LatestFrame
    {
        get
        {
            lock (_sync) return _latestFrame;
        }
    }

    public State? LatestReferee
    {
        get
        {
            lock (_sync) return _latestReferee;
        }
    }

    /// <summary>Drains the channels without recording a sample. Used while waiting for readiness.</summary>
    public void Poll()
    {
        lock (_sync) Drain();
    }

    public void Start(float sampleHz)
    {
        _start = Timestamp.Now;
        _started = true;
        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var period = TimeSpan.FromSeconds(1.0 / System.Math.Max(sampleHz, 0.1f));

        _thread = new Thread(() =>
        {
            var next = DateTime.UtcNow;
            while (!token.IsCancellationRequested)
            {
                lock (_sync)
                {
                    Drain();
                    _samples.Add(Capture());
                }

                next += period;
                var delay = next - DateTime.UtcNow;
                if (delay > TimeSpan.Zero) token.WaitHandle.WaitOne(delay);
            }
        }) { IsBackground = true, Name = "WorldSampler" };
        _thread.Start();
    }

    public void Stop()
    {
        _cts?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(2));
        _thread = null;
    }

    public void AddEvent(string type, string text)
    {
        lock (_sync) _events.Add(new Event(Elapsed(), type, text));
    }

    public Summary BuildSummary(string scenarioName, string sessionDirectory, string configPath, float sampleHz)
    {
        lock (_sync)
        {
            return new Summary(
                scenarioName,
                _start.ToDateTime(),
                Elapsed(),
                sampleHz,
                sessionDirectory,
                configPath,
                FramesSeen,
                _samples.Count > 0 ? _samples[^1] : null,
                _samples.ToArray(),
                _events.ToArray());
        }
    }

    public static void WriteSummary(Summary summary, string path)
    {
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(summary, options));
    }

    private void Drain()
    {
        while (_vision.Reader.TryRead(out var frame))
        {
            _latestFrame = frame;
            Interlocked.Increment(ref _framesSeen);
        }

        while (_referee.Reader.TryRead(out var referee))
        {
            if (_latestReferee is null || _latestReferee.GameState != referee.GameState ||
                _latestReferee.Color != referee.Color || _latestReferee.Ready != referee.Ready)
            {
                _events.Add(new Event(Elapsed(), "referee", referee.ToString()));
            }

            _latestReferee = referee;
        }

        while (_commands.Reader.TryRead(out var commands))
        {
            _latestCommands[commands.Color] = commands;
        }
    }

    private double Elapsed() => _started ? (Timestamp.Now - _start).Seconds : 0;

    private Sample Capture()
    {
        BallSample? ball = null;
        RobotSample[] robots = [];

        if (_latestFrame is { } frame)
        {
            var b = frame.Ball.State;
            ball = new BallSample(b.Position3D.X, b.Position3D.Y, b.Position3D.Z, b.Velocity.X, b.Velocity.Y);
            robots = frame.Robots
                .Select(r => new RobotSample(
                    r.Id.Team?.ToString() ?? "Unknown", r.Id.Id,
                    r.State.Position.X, r.State.Position.Y, r.State.Angle.Deg,
                    r.State.Velocity.X, r.State.Velocity.Y))
                .ToArray();
        }

        RefereeSample? referee = _latestReferee is { } state
            ? new RefereeSample(state.GameState.ToString(), state.Color.ToString(), state.Ready, state.Gc.Command.ToString(),
                state.Gc.BlueTeamOnPositiveHalf)
            : null;

        var commands = new Dictionary<string, CommandSample[]>();
        foreach (var (color, wrapper) in _latestCommands)
        {
            commands[color.ToString()] = wrapper.Commands
                .Select(c => new CommandSample(c.VisionId, c.Halted, c.Motion.X, c.Motion.Y, c.TargetAngle.Deg, c.Shoot, c.Chip, c.DribblerSpeed))
                .ToArray();
        }

        return new Sample(Elapsed(), ball, robots, referee, commands);
    }

    public void Dispose()
    {
        Stop();
        _vision.Dispose();
        _referee.Dispose();
        _commands.Dispose();
    }

    // ── result model (summary.json) ─────────────────────────────────────────

    public sealed record Summary(
        string Scenario,
        DateTime StartedUtc,
        double DurationSeconds,
        float SampleHz,
        string SessionDirectory,
        string ConfigPath,
        long VisionFrames,
        Sample? Final,
        Sample[] Samples,
        Event[] Events);

    /// <summary>All positions in mm, velocities in mm/s, angles in degrees; t is seconds since the run started.</summary>
    public sealed record Sample(double T, BallSample? Ball, RobotSample[] Robots, RefereeSample? Referee, Dictionary<string, CommandSample[]> Commands);

    public sealed record BallSample(float X, float Y, float Z, float Vx, float Vy);

    public sealed record RobotSample(string Team, uint? Id, float X, float Y, float AngleDeg, float Vx, float Vy);

    public sealed record RefereeSample(string GameState, string Color, bool Ready, string? GcCommand, bool? BlueTeamOnPositiveHalf);

    public sealed record CommandSample(int Id, bool Halted, float Vx, float Vy, float TargetAngleDeg, float Shoot, float Chip, float Dribbler);

    public sealed record Event(double T, string Type, string Text);
}
