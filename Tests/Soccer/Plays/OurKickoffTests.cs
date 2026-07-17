using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Gc;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Extensions;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Soccer;
using Tyr.Soccer.Plays;
using Tyr.Soccer.Role;
using Vision = Tyr.Common.Vision.Data;
using Xunit;

namespace Tests.Soccer.Plays;

public class OurKickoffTests
{
    [Fact]
    public void OurKickoff_ReturnsCorrectFormation()
    {
        // Arrange
        var field = FieldSize.DivisionB;
        var ball = new Vision.FilteredBall { State = new Vision.BallState { Position3D = Vector2.Zero.Xyz() } };
        var referee = new State
        {
            GameState = GameState.Kickoff,
            Color = TeamColor.Blue,
            Gc = new Referee { BlueTeamOnPositiveHalf = false } // Left
        };

        Context.Data.Value = new ContextData
        {
            Color = TeamColor.Blue,
            VisionTime = Timestamp.Zero,
            Ball = ball,
            OppRobots = [],
            OwnRobots = [],
            Referee = referee,
            Field = field,
            Timer = new Tyr.Common.Time.Timer(),
            Knowledge = null!,
            RoleAssignment = null!
        };

        var play = new OurKickoff();

        // Act
        var formation = play.Tick();

        // Assert
        Assert.Equal(4, formation.RequiredRoles.Count);
        Assert.Equal(4, formation.DesiredRoles.Count);

        Assert.Contains(formation.RequiredRoles, r => r is Goalie);
        Assert.Contains(formation.RequiredRoles, r => r is Defender { DefId: 1 });
        Assert.Contains(formation.RequiredRoles, r => r is Defender { DefId: 2 });
        Assert.Contains(formation.RequiredRoles, r => r is CircleBall);

        var attacker = (CircleBall)formation.RequiredRoles.First(r => r is CircleBall);
        // side = -Context.SideSign. Left is -1, so side = 1.
        // Receivers wait 150 mm inside our own half (kickoff rules):
        // mid5Pos = (0 - 1 * 150, 3000 - 300) = (-150, 2700)
        Assert.Equal(new Vector2(-150f, 2700f), attacker.TargetPosition);
        Assert.Equal(0f, attacker.ShootPower);

        var mid5 = (Waiter)formation.DesiredRoles[0];
        Assert.Equal(new Vector2(-150f, 2700f), mid5.Target);

        var mid1 = (Waiter)formation.DesiredRoles[1];
        Assert.Equal(new Vector2(-150f, -2700f), mid1.Target);

        var mid2 = (Waiter)formation.DesiredRoles[2];
        // mid2Pos = ballPos.PointOnConnectingLine(OwnGoal, 1000f)
        // OwnGoal is (-4500, 0)
        // ball is (0,0)
        // direction is (-4500, 0) - (0,0) = (-4500, 0)
        // normalized is (-1, 0)
        // target is (0,0) + (-1,0) * 1000 = (-1000, 0)
        Assert.Equal(new Vector2(-1000f, 0f), mid2.Target);

        var mid3 = (Waiter)formation.DesiredRoles[3];
        // mid3Pos = ballPos.PointOnConnectingLine(OwnGoal, 2000f) = (-2000, 0)
        Assert.Equal(new Vector2(-2000f, 0f), mid3.Target);
    }

    [Fact]
    public void OurKickoff_KicksWhenReady()
    {
        // Arrange
        var field = FieldSize.DivisionB;
        var ball = new Vision.FilteredBall { State = new Vision.BallState { Position3D = Vector2.Zero.Xyz() } };
        var referee = new State
        {
            GameState = GameState.Kickoff,
            Color = TeamColor.Blue,
            Gc = new Referee { BlueTeamOnPositiveHalf = false },
            Timestamp = Timestamp.Zero,
            Ready = true
        };

        Context.Data.Value = new ContextData
        {
            Color = TeamColor.Blue,
            VisionTime = Timestamp.FromSeconds(3),
            Ball = ball,
            OppRobots = [],
            OwnRobots = [],
            Referee = referee,
            Field = field,
            Timer = new Tyr.Common.Time.Timer(),
            Knowledge = null!,
            RoleAssignment = null!
        };

        var play = new OurKickoff();

        // Act
        var formation = play.Tick();

        // Assert
        var attacker = (CircleBall)formation.RequiredRoles.First(r => r is CircleBall);
        Assert.Equal(3000f, attacker.ShootPower);
    }
}
