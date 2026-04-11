using System.Net;
using System.Net.Sockets;
using ProtoBuf;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Simulation;
using Tyr.Common.Math;
using GeometryData = Tyr.Common.Data.Ssl.Vision.Geometry.Data;

namespace Tyr.Gui.Simulator;

/// <summary>
/// Sends SimulatorCommand packets to the grSim control port and logs any
/// SimulatorResponse errors returned by the simulator.
/// </summary>
[Configurable]
public sealed partial class SimulatorChannel : IDisposable
{
    [ConfigEntry("IP address of the machine running grSim")]
    private static string Host { get; set; } = "127.0.0.1";

    [ConfigEntry("UDP port for the grSim simulator control channel")]
    private static int ControlPort { get; set; } = 10300;

    // ── Robot geometry (SI units: meters, kg) ─────────────────────────────────
    [ConfigEntry("Robot body radius (m)")]              private static float? RobotRadius          { get; set; }
    [ConfigEntry("Robot body height (m)")]              private static float? RobotHeight          { get; set; }
    [ConfigEntry("Robot mass (kg)")]                    private static float? RobotMass            { get; set; }
    [ConfigEntry("Max linear kick speed (m/s)")]        private static float? MaxLinearKickSpeed   { get; set; }
    [ConfigEntry("Max chip kick speed (m/s)")]          private static float? MaxChipKickSpeed     { get; set; }
    [ConfigEntry("Center to dribbler distance (m)")]    private static float? CenterToDribbler     { get; set; }

    // ── Robot motion limits (SI: m/s², rad/s², m/s, rad/s) ───────────────────
    [ConfigEntry("Max linear speedup acceleration (m/s²)")]  private static float? AccSpeedupAbsoluteMax { get; set; }
    [ConfigEntry("Max angular speedup acceleration (rad/s²)")] private static float? AccSpeedupAngularMax { get; set; }
    [ConfigEntry("Max linear brake acceleration (m/s²)")]    private static float? AccBrakeAbsoluteMax   { get; set; }
    [ConfigEntry("Max angular brake acceleration (rad/s²)")] private static float? AccBrakeAngularMax    { get; set; }
    [ConfigEntry("Max linear velocity (m/s)")]               private static float? VelAbsoluteMax        { get; set; }
    [ConfigEntry("Max angular velocity (rad/s)")]            private static float? VelAngularMax         { get; set; }

    // ── Wheel angles (degrees, converted to rad when sending) ─────────────────
    [ConfigEntry("Front-right wheel angle (deg)")]  private static float? WheelFrontRightDeg { get; set; }
    [ConfigEntry("Back-right wheel angle (deg)")]   private static float? WheelBackRightDeg  { get; set; }
    [ConfigEntry("Back-left wheel angle (deg)")]    private static float? WheelBackLeftDeg   { get; set; }
    [ConfigEntry("Front-left wheel angle (deg)")]   private static float? WheelFrontLeftDeg  { get; set; }

    /// <summary>Set by SimulatorView each frame to reflect whether grSim is currently running.</summary>
    public bool SimulatorRunning { get; set; }

    private readonly Socket _socket;
    private readonly byte[] _sendBuf = new byte[65536];
    private readonly byte[] _recvBuf = new byte[65536];
    private readonly Lock _sendLock = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _receiveTask;

    public SimulatorChannel()
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, 0));
        _receiveTask = Task.Run(ReceiveLoopAsync);
    }

    // ── public API ────────────────────────────────────────────────────────────

    public void TeleportBall(TeleportBall ball) =>
        Send(new Command { Control = new Control { TeleportBall = ball } });

    public void TeleportRobots(params ReadOnlySpan<TeleportRobot> robots) =>
        Send(new Command { Control = new Control { TeleportRobot = [.. robots] } });

    public void TeleportBallAndRobots(TeleportBall ball, params ReadOnlySpan<TeleportRobot> robots) =>
        Send(new Command { Control = new Control { TeleportBall = ball, TeleportRobot = [.. robots] } });

    public void SetSimulationSpeed(float speed) =>
        Send(new Command { Control = new Control { SimulationSpeed = speed } });

    /// <summary>Sends a SimulatorConfig for geometry and/or vision port. RealismConfig intentionally excluded.</summary>
    public void SendConfig(GeometryData? geometry = null, uint? visionPort = null) =>
        Send(new Command
        {
            Config = new Config
            {
                Geometry   = geometry,
                VisionPort = visionPort,
            }
        });

    /// <summary>
    /// Sends robot specs (built from config entries) for all robots on both teams.
    /// Only fields with non-null config values are included; grSim keeps its defaults for the rest.
    /// </summary>
    public void SendRobotSpecs(int robotCount = 11)
    {
        var specs = new List<RobotSpecs>(robotCount * 2);
        foreach (var team in (ReadOnlySpan<TeamColor>)[TeamColor.Yellow, TeamColor.Blue])
            for (uint id = 0; id < robotCount; id++)
                specs.Add(BuildRobotSpecs(team, id));

        Send(new Command { Config = new Config { RobotSpecs = specs } });
    }

    // ── convenience overloads ─────────────────────────────────────────────────

    /// <summary>Teleports the ball to (x, y, z) in meters with zero velocity.</summary>
    public void TeleportBall(float xMeters, float yMeters, float zMeters = 0f) =>
        TeleportBall(new TeleportBall { X = xMeters, Y = yMeters, Z = zMeters });

    /// <summary>Teleports a single robot to (x, y) in meters with an optional orientation.</summary>
    public void TeleportRobot(TeamColor team, uint id, float xMeters, float yMeters, Angle? orientation = null) =>
        TeleportRobots(new TeleportRobot
        {
            Id          = new RobotId { Team = team, Id = id },
            X           = xMeters,
            Y           = yMeters,
            Orientation = orientation,
            Present     = true,
        });

    /// <summary>Removes a robot from the field.</summary>
    public void RemoveRobot(TeamColor team, uint id) =>
        TeleportRobots(new TeleportRobot
        {
            Id      = new RobotId { Team = team, Id = id },
            Present = false,
        });

    // ── internals ─────────────────────────────────────────────────────────────

    private void Send(Command command)
    {
        try
        {
            var endpoint = new IPEndPoint(IPAddress.Parse(Host), ControlPort);
            lock (_sendLock)
            {
                using var ms = new MemoryStream(_sendBuf);
                Serializer.Serialize(ms, command);
                _socket.SendTo(_sendBuf, 0, (int)ms.Position, SocketFlags.None, endpoint);
            }
        }
        catch (Exception ex)
        {
            Log.ZLogError(ex, $"SimulatorChannel send failed");
        }
    }

    private static RobotSpecs BuildRobotSpecs(TeamColor team, uint id)
    {
        var hasLimits = AccSpeedupAbsoluteMax.HasValue || AccSpeedupAngularMax.HasValue ||
                        AccBrakeAbsoluteMax.HasValue   || AccBrakeAngularMax.HasValue   ||
                        VelAbsoluteMax.HasValue        || VelAngularMax.HasValue;

        var allWheelAngles = WheelFrontRightDeg.HasValue && WheelBackRightDeg.HasValue &&
                             WheelBackLeftDeg.HasValue   && WheelFrontLeftDeg.HasValue;

        return new RobotSpecs
        {
            Id                = new RobotId { Team = team, Id = id },
            Radius            = RobotRadius,
            Height            = RobotHeight,
            Mass              = RobotMass,
            MaxLinearKickSpeed = MaxLinearKickSpeed,
            MaxChipKickSpeed  = MaxChipKickSpeed,
            CenterToDribbler  = CenterToDribbler,
            Limits = hasLimits ? new RobotLimits
            {
                AccSpeedupAbsoluteMax = AccSpeedupAbsoluteMax,
                AccSpeedupAngularMax  = AccSpeedupAngularMax,
                AccBrakeAbsoluteMax   = AccBrakeAbsoluteMax,
                AccBrakeAngularMax    = AccBrakeAngularMax,
                VelAbsoluteMax        = VelAbsoluteMax,
                VelAngularMax         = VelAngularMax,
            } : null,
            WheelAngles = allWheelAngles ? new RobotWheelAngles
            {
                FrontRightRad = WheelFrontRightDeg!.Value * MathF.PI / 180f,
                BackRightRad  = WheelBackRightDeg!.Value  * MathF.PI / 180f,
                BackLeftRad   = WheelBackLeftDeg!.Value   * MathF.PI / 180f,
                FrontLeftRad  = WheelFrontLeftDeg!.Value  * MathF.PI / 180f,
            } : null,
        };
    }

    private async Task ReceiveLoopAsync()
    {
        EndPoint from = new IPEndPoint(IPAddress.Any, 0);
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var result = await _socket
                    .ReceiveFromAsync(new ArraySegment<byte>(_recvBuf), SocketFlags.None, from)
                    .WaitAsync(_cts.Token);

                var response = Serializer.Deserialize<Response>(
                    new ReadOnlySpan<byte>(_recvBuf, 0, result.ReceivedBytes));

                if (response.Errors is { Count: > 0 })
                    foreach (var err in response.Errors)
                        Log.ZLogWarning($"[grSim] {err.Code}: {err.Message}");
            }
            catch (OperationCanceledException) { break; }
            catch (SocketException ex) when (ex.SocketErrorCode is SocketError.Interrupted or SocketError.OperationAborted) { break; }
            catch (Exception ex)
            {
                Log.ZLogError(ex, $"SimulatorChannel receive error");
            }
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _socket.Dispose();
        _receiveTask.Wait(TimeSpan.FromSeconds(1));
        _cts.Dispose();
    }
}
