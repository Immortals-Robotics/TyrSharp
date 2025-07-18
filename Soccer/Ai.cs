using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Dataflow;
using Tyr.Common.Math;
using Tyr.Common.Sender.Data;
using Tyr.Soccer.Robot;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;

namespace Tyr.Soccer;

public class Ai
{
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
                    Id = new RobotId() {Team = Context.Color, Id = (uint)i},
                }
            });
        }
    }
    
    public void UpdateContext(Vision.FilteredFrame vision, Referee.State referee, FieldSize field)
    {
        foreach (var robot in Context.OwnRobots)
        {
            robot.Filtered = robot.Filtered with { Quality = 0f };
        }

        foreach (var filtered in vision.Robots.Where(robot => robot.Id.Team == Context.Color))
        {
            Context.OwnRobots[(int)filtered.Id.Id!.Value].Filtered = filtered;
        }
        
        var oppRobots = vision.Robots.Where(robot => robot.Id.Team != Context.Color);

        Context.Data.Value = Context.Data.Value! with
        {
            Ball = vision.Ball,
            OppRobots = oppRobots.ToList(),
            Referee = referee,
            Field = field,
        };
    }

    public void PublishCommands()
    {
        var commands = new CommandsWrapper()
        {
            Time = Context.Timer.Time,
            Color = Context.Color,
        };
        
        foreach (var robot in Context.OwnRobots)
        {
            robot.WaitForNavigationJob();
            
            if (!robot.Seen)  continue;
            commands.Commands.Add(robot.CurrentCommand);
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

        foreach (var robot in Context.OwnRobots)
        {
            if (!robot.Seen)
            {
                robot.Halt();
            }
            else
            {
                robot.TargetAngle = Angle.Zero;
                var x = Context.SideSign * robot.Id * 500f;
                var y = Angle.FromRad((float)Context.Timer.Time.Seconds + robot.Id).Sin() * 1000f;
                robot.Navigate(new Vector2(x, y), VelocityProfile.Mamooli);
            }
        }
    }
}