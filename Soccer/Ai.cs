using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Sender.Data;
using Tyr.Common.Time;
using Tyr.Soccer.Tactics;
using Command = Tyr.Common.Sender.Data.Command;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

[Configurable]
public partial class Ai
{
    [ConfigEntry] internal static DeltaTime VisionPredictionTime { get; set; } = DeltaTime.FromMilliseconds(120);

    private readonly Dictionary<int, LinkedList<(Timestamp, Command)>> _commandHistories = [];

    private readonly ManualControlState _manualControl;

    private Dictionary<int, Role.IRole?> _roleMapping = [];
    private Dictionary<int, Role.IRole?> _nextRoleMapping = [];
    private Dictionary<int, ITactic?> _tacticMapping = [];

    private readonly List<Role.IRole> _activeRoles = [];
    private static readonly Role.Attacker _attackerRole = new();

    internal Ai(ManualControlState manualControl)
    {
        _manualControl = manualControl;
    }

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

        // Bolt: eliminates ~2 enumerator & closure allocs/frame by avoiding LINQ Where()
        Context.OppRobots.Clear();
        foreach (var filtered in vision.Robots)
        {
            if (filtered.Id.Team == Context.Color)
            {
                var id = (int)filtered.Id.Id!.Value;
                var predicted = filtered.Extrapolate(now, _commandHistories[id]);
                Context.OwnRobots[id].Filtered = predicted;

                Draw.DrawRobot(predicted.State.Position, predicted.State.Angle, filtered.Id,
                    options: Options.Outline());
            }
            else
            {
                var predicted = filtered.Extrapolate(now);
                Context.OppRobots.Add(predicted);

                Draw.DrawRobot(predicted.State.Position, predicted.State.Angle, filtered.Id,
                    options: Options.Outline());
            }
        }

        Context.Data.Value = Context.Data.Value! with
        {
            VisionTime = vision.Timestamp,
            Ball = vision.Ball.Extrapolate(now),
            Referee = referee,
            Field = field,
        };

        // Draw ball trajectory for next 5 seconds
        var ballTrajectoryDuration = DeltaTime.FromSeconds(5);
        var trajectorySteps = 50;
        var stepTime = ballTrajectoryDuration / trajectorySteps;

        for (var i = 0; i < trajectorySteps; i++)
        {
            var futureTime = now + stepTime * i;
            var futureBall = vision.Ball.Extrapolate(futureTime);
            Draw.DrawCircle(futureBall.State.Position, 20f, Color.Red, options: Options.Outline());
        }

        var touchdown = vision.Ball.GetNextTouchdown();
        if (touchdown.HasValue)
        {
            Draw.DrawPoint(touchdown.Value.Position, Color.Black);
        }
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

            _commandHistories[robot.Id].AddLast((Context.Time, command));
        }

        Hub.Commands.Publish(commands);
    }

    public void Process()
    {
        Log.ZLogDebug($"fps: {Context.Timer.FpsSmooth}");
        Plot.Plot("fps", Context.Timer.Fps);

        foreach (var robot in Context.OwnRobots)
        {
            robot.Reset();
        }


        foreach (var zone in Context.Knowledge.Zones)
        {
            zone.UpdateScore(false);
            zone.DrawZone();
        }

        if (_manualControl.TryExecute())
        {
            return;
        }

        // this resembles a play emitting a role
        // Bolt: reuse pre-allocated list to prevent `new List<IRole>()` alloc/frame
        _activeRoles.Clear();
        if (Context.Referee.Running())
        {
            _activeRoles.Add(_attackerRole);
        }

        // Sort roles by importance descending (allocation-free sort)
        _activeRoles.Sort(static (r1, r2) => r2.Importance.CompareTo(r1.Importance));

        // Bolt: eliminate LINQ Where/OrderBy/FirstOrDefault allocation chains
        _nextRoleMapping.Clear();
        foreach (var role in _activeRoles)
        {
            Robot.Robot? best = null;
            var bestCost = float.MaxValue;

            foreach (var r in Context.OwnRobots)
            {
                if (r.Seen && !_nextRoleMapping.ContainsKey(r.Id))
                {
                    var cost = role.CostFor(r);
                    if (cost < bestCost)
                    {
                        bestCost = cost;
                        best = r;
                    }
                }
            }

            if (best != null)
            {
                _nextRoleMapping[best.Id] = role;
            }
        }

        // we then re-create tactics for robots who have changed role
        foreach (var robot in Context.OwnRobots)
        {
            var currentRole = _roleMapping.GetValueOrDefault(robot.Id);
            var newRole = _nextRoleMapping.GetValueOrDefault(robot.Id);

            if (!currentRole?.Equals(newRole) ?? newRole is not null)
            {
                Log.ZLogInformation(
                    $"Switching robot {robot.Id}'s role fro {currentRole?.GetType().Name} to {newRole?.GetType().Name}");
                _tacticMapping[robot.Id] = newRole?.CreateTactic(Context.OwnRobots[0]);
            }
        }

        // Bolt: swap dictionaries to prevent `new Dictionary` allocation next frame
        (_roleMapping, _nextRoleMapping) = (_nextRoleMapping, _roleMapping);

        // and execute tactics for each robot
        foreach (var robot in Context.OwnRobots)
        {
            var tactic = _tacticMapping.GetValueOrDefault(robot.Id);
            var skill = tactic?.Tick();
            if (skill != null)
                skill.Execute(robot);
            else
                robot.Halt();
        }
    }
}
