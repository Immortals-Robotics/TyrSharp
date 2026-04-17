using System.Collections.Concurrent;
using Tyr.Common.Config;
using Tyr.Common.Data.Robot;
using Tyr.Common.Dataflow;
using Tyr.Common.Network;
using Tyr.Common.Sender.Data;

namespace Tyr.Sender;

[Configurable]
public sealed partial class ZmqRobotSender : ISender
{
    [ConfigEntry] private static bool Enabled { get; set; } = true;
    [ConfigEntry] private static int CmdPort { get; set; } = 5671;
    [ConfigEntry] private static float DefaultDribblerForce { get; set; } = 5.0f;
    [ConfigEntry] private static CommandType RobotCommandTopic { get; set; } = CommandType.CommandTypeRobot;

    private readonly ConcurrentDictionary<int, ZmqSender> _senders = new();
    private readonly Subscriber<IReadOnlyList<DiscoveredRobot>> _discoverySubscriber;
    private readonly ConcurrentDictionary<int, string> _robotIps = new();

    public ZmqRobotSender()
    {
        _discoverySubscriber = Hub.DiscoveredRobots.Subscribe(Mode.Latest);
    }

    private void UpdateConnections()
    {
        if (!_discoverySubscriber.Reader.TryRead(out var robots))
            return;

        var activeIds = new HashSet<int>();
        foreach (var robot in robots)
        {
            if (!robot.IsOnline) continue;
            activeIds.Add(robot.Id);

            if (!_senders.TryGetValue(robot.Id, out var sender))
            {
                var address = new Address { Ip = robot.Ip, Port = CmdPort };
                Log.ZLogInformation($"Connecting to command stream for Robot {robot.Id} at {address}");
                
                _robotIps[robot.Id] = robot.Ip;
                _senders[robot.Id] = new ZmqSender(address, ZmqMode.Connect);
            }
            else if (_robotIps.TryGetValue(robot.Id, out var currentIp) && currentIp != robot.Ip)
            {
                Log.ZLogInformation($"Robot {robot.Id} moved to {robot.Ip}, updating connection");
                _robotIps[robot.Id] = robot.Ip;
                sender.ChangeAddress(new Address { Ip = robot.Ip, Port = CmdPort });
            }
        }

        // Clean up timed-out robots
        var toRemove = _senders.Keys.Where(id => !activeIds.Contains(id));
        foreach (var id in toRemove)
        {
            if (_senders.TryRemove(id, out var sender))
            {
                Log.ZLogInformation($"Closing command stream for Robot {id}");
                sender.Dispose();
                _robotIps.TryRemove(id, out _);
            }
        }
    }

    public bool Send(CommandsWrapper commands)
    {
        if (!Enabled) return false;

        UpdateConnections();

        var anySent = false;
        foreach (var cmd in commands.Commands)
        {
            if (!_senders.TryGetValue(cmd.VisionId, out var sender))
                continue;

            var mikonaCmd = new MikonaCommand
            {
                Charge = !cmd.Halted,
                Discharge = cmd.Halted
            };

            var robotCmd = new RobotCommand
            {
                Halted = cmd.Halted,
                Vx = cmd.Motion.X / 1000.0f, // mm/s -> m/s
                Vy = cmd.Motion.Y / 1000.0f,
                CurrentAngle = (float)cmd.CurrentAngle.Deg,
                TargetAngle = (float)cmd.TargetAngle.Deg,
                Shoot = GetShootPower(cmd),
                Chip = GetChipPower(cmd),
                DribblerSpeed = cmd.DribblerSpeed,
                DribblerForce = cmd.DribblerForce > 0f ? cmd.DribblerForce : DefaultDribblerForce
            };

            var mikonaSent = sender.Send(CommandType.CommandTypeMikona, mikonaCmd);
            var robotSent = sender.Send(RobotCommandTopic, robotCmd);

            if (mikonaSent || robotSent)
            {
                Log.ZLogTrace($"Dispatched command to Robot {cmd.VisionId} at {_robotIps[cmd.VisionId]}");
                anySent = true;
            }
        }

        return anySent;
    }

    private static float GetShootPower(Command cmd)
    {
        if (cmd.Shoot <= 0f)
        {
            return 0f;
        }

        var raw = Math.Clamp(cmd.Shoot, 0.0f, 6500.0f) / 1000.0f;
        return ShootCalibration.GetCalibratedPower(raw, ShootCalibration.ShootType.Shoot, cmd.VisionId);
    }

    private static float GetChipPower(Command cmd)
    {
        if (cmd.Chip <= 0f)
        {
            return 0f;
        }

        var raw = Math.Clamp(cmd.Chip, 0.0f, 150.0f);
        return ShootCalibration.GetCalibratedPower(raw, ShootCalibration.ShootType.Chip, cmd.VisionId);
    }

    public void Dispose()
    {
        foreach (var sender in _senders.Values)
            sender.Dispose();
        _senders.Clear();
    }
}
