﻿﻿﻿using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Math;
using Tyr.Common.Sender.Data;
using Tyr.Common.Time;
using Tyr.Soccer.Robot;
using Command = Tyr.Common.Sender.Data.Command;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

[Configurable]
public partial class Ai
{
    [ConfigEntry] internal static DeltaTime VisionPredictionTime { get; set; } = DeltaTime.FromMilliseconds(120);

    private readonly Dictionary<int, LinkedList<(Timestamp, Command)>> _commandHistories = [];
    private LinkedList<(Timestamp, Command)> CommandHistory(int id) => _commandHistories[id];

    public void Init()
    {
        Assert.IsZero(Context.OwnRobots.Count);
        Context.OwnRobots.EnsureCapacity(CommonConfigs.MaxRobots);
        for (var i = 0; i < CommonConfigs.MaxRobots; i++)
        {
            Context.OwnRobots.Add(new Robot.Robot()
            {
                Filtered = new Vision.FilteredRobot
                {
                    Quality = 0f,
                    Id = new RobotId() { Team = Context.Color, Id = (uint)i },
                }
            });

            _commandHistories[i] = [];
        }

        Context.Knowledge.InitZones();
    }

    public void UpdateContext(Vision.FilteredFrame vision, Referee.State referee, FieldSize field)
    {
        var now = vision.Timestamp + VisionPredictionTime;

        foreach (var robot in Context.OwnRobots)
        {
            robot.Filtered = robot.Filtered with { Quality = 0f };
        }

        foreach (var filtered in vision.Robots.Where(robot => robot.Id.Team == Context.Color))
        {
            var id = (int)filtered.Id.Id!.Value;
            var predicted = filtered.Extrapolate(now, CommandHistory(id));
            Context.OwnRobots[id].Filtered = predicted;

            Draw.DrawRobot(predicted.State.Position, predicted.State.Angle, filtered.Id, options: Options.Outline());
        }

        Context.OppRobots.Clear();
        foreach (var filtered in vision.Robots.Where(robot => robot.Id.Team != Context.Color))
        {
            var predicted = filtered.Extrapolate(now);
            Context.OppRobots.Add(predicted);

            Draw.DrawRobot(predicted.State.Position, predicted.State.Angle, filtered.Id, options: Options.Outline());
        }

        Context.Data.Value = Context.Data.Value! with
        {
            VisionTime = vision.Timestamp,
            Ball = vision.Ball.Extrapolate(now),
            Referee = referee,
            Field = field,
        };
    }

    public void PublishCommands()
    {
        // trim the history buffer
        foreach (var (_, history) in _commandHistories)
        {
            while (history.First != null && history.First.Value.Item1 <= Context.VisionTime)
            {
                history.RemoveFirst();
            }
        }

        var commands = new CommandsWrapper()
        {
            Time = Context.Time,
            Color = Context.Color,
        };

        foreach (var robot in Context.OwnRobots)
        {
            robot.WaitForNavigationJob();

            if (!robot.Seen) continue;

            var command = robot.CurrentCommand;
            commands.Commands.Add(command);

            CommandHistory(robot.Id).AddLast((Context.Time, command));
        }

        Hub.Commands.Publish(commands);
    }

    public void Process()
    {
        Log.ZLogDebug($"fps: {Context.Timer.FpsSmooth}");

        foreach (var robot in Context.OwnRobots)
        {
            robot.Reset();
        }


        foreach (var zone in Context.Knowledge.Zones)
        {
            zone.UpdateScore(false);
            zone.DrawZone();
        }

        foreach (var robot in Context.OwnRobots)
        {
            if (!robot.Seen)
            {
                robot.Halt();
            }
            else
            {
                robot.TargetAngle = Angle.FromDeg(90f);
                var x = -2000f;
                var sin = MathF.Floor(Angle.FromRad((float)Context.Timer.Time.Seconds + robot.Id).Sin());
                var y = (2f * sin + 1f) * 1500f;
                robot.Navigate(new Vector2(x, y), VelocityProfile.Mamooli);
            }
        }
    }
}