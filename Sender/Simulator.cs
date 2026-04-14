using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Simulation;
using Tyr.Common.Dataflow;
using Tyr.Common.Math;
using Tyr.Common.Network;
using Tyr.Common.Sender.Data;
using Tyr.Common.Runner;

using DeltaTime = Tyr.Common.Time.DeltaTime;

namespace Tyr.Sender;

[Configurable]
public sealed partial class Simulator : ISender
{
    [ConfigEntry] private static bool Enabled { get; set; } = false;

    [ConfigEntry] private static Address BlueAddress { get; set; } = new() { Ip = "127.0.0.1", Port = 10301 };
    [ConfigEntry] private static Address YellowAddress { get; set; } = new() { Ip = "127.0.0.1", Port = 10302 };

    [ConfigEntry] private static Angle ChipAngle { get; set; } = Angle.FromDeg(45f);
    [ConfigEntry] private static DeltaTime FeedbackPollTimeout { get; set; } = DeltaTime.FromMilliseconds(10);

    private readonly UdpSocket _udp = new();
    private readonly RunnerSync _runner;

    public Simulator()
    {
        _udp.Bind(new Address { Ip = "0.0.0.0", Port = 0 });
        _runner = new RunnerSync(TickFeedback, 0, nameof(Simulator));
        _runner.Start();
    }

    public bool Send(CommandsWrapper commands)
    {
        if (!Enabled) return false;

        var pbControl = new RobotControl()
        {
            RobotCommands = []
        };

        foreach (var command in commands.Commands)
        {
            var localVel = command.Motion.Rotated(Angle.FromDeg(90.0f) - command.CurrentAngle);
            // Use the shortest signed angle delta so wraparound near +/-180 does not cause a long spin.
            var w = (command.TargetAngle - command.CurrentAngle).DegNormalized / 10.0f;

            var kickSpeed = 0f;
            var kickAngle = Angle.Zero;

            if (command.Shoot > 0)
            {
                kickSpeed = command.Shoot;
            }
            else if (command.Chip > 0)
            {
                kickSpeed = command.Chip * 5;
                kickAngle = ChipAngle;
            }

            var pbCommand = new RobotCommand()
            {
                Id = (uint)command.VisionId,
                MoveCommand = new RobotMoveCommand()
                {
                    LocalVelocity = new MoveLocalVelocity()
                    {
                        Forward = localVel.Y / 1000.0f,
                        Left = -localVel.X / 1000.0f,
                        AngularRad = w,
                    },
                },
                KickSpeed = kickSpeed,
                KickAngle = kickAngle,
                DribblerSpeed = command.DribblerSpeed,
            };

            pbControl.RobotCommands.Add(pbCommand);
        }

        var address = commands.Color switch
        {
            TeamColor.Blue => BlueAddress,
            TeamColor.Yellow => YellowAddress,
            _ => throw new ArgumentOutOfRangeException()
        };

        _udp.Send(pbControl, address);
        return true;
    }

    private bool TickFeedback()
    {
        if (!Enabled)
        {
            Thread.Sleep(500);
            return false;
        }

        if (!_udp.Poll(FeedbackPollTimeout))
            return false;

        var data = _udp.Receive<RobotControlResponse>();
        // TODO: for whatever reason grsim sometimes sends a SimulationSyncResponse back
        if (data is null)
            return false;

        Hub.SimFeedback.Publish(data);
        return true;
    }

    public void Dispose()
    {
        _runner.Stop();
        _udp.Dispose();
    }
}
