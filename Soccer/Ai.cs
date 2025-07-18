using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
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
            Context.OwnRobots.Add(new Robot.Robot());
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

    public void Process()
    {
        Log.ZLogDebug($"fps: {Context.Timer.FpsSmooth}");

        foreach (var robot in Context.OwnRobots)
        {
            if (!robot.Seen) continue;

            robot.Navigate(Context.Ball.State.Position.Xy(), VelocityProfile.Mamooli);
        }

        foreach (var robot in Context.OwnRobots)
        {
            robot.WaitForNavigationJob();
            robot.Reset();
        }
    }
}