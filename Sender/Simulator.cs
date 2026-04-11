using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Simulation;
using Tyr.Common.Math;
using Tyr.Common.Network;
using Tyr.Common.Sender.Data;

namespace Tyr.Sender;

[Configurable]
public sealed partial class Simulator : ISender
{
    [ConfigEntry] private static bool Enabled { get; set; } = false;

    [ConfigEntry] private static Address BlueAddress { get; set; } = new() { Ip = "127.0.0.1", Port = 10301 };
    [ConfigEntry] private static Address YellowAddress { get; set; } = new() { Ip = "127.0.0.1", Port = 10302 };

    [ConfigEntry] private static Angle ChipAngle { get; set; } = Angle.FromDeg(45f);

    private readonly UdpServer _udp = new();

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
                DribblerSpeed = command.Dribbler,
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

    public void Dispose()
    {
        _udp.Dispose();
    }
}