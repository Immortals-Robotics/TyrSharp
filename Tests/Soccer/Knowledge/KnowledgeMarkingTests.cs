using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.RoleAssignment;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using TyrTimer = Tyr.Common.Time.Timer;

namespace Tyr.Tests.Soccer.KnowledgeTests;

public sealed class KnowledgeMarkingTests : IDisposable
{
    private readonly ContextData? _previous;

    public KnowledgeMarkingTests()
    {
        _previous = Context.Data.Value;
        Context.Data.Value = new ContextData
        {
            Color = TeamColor.Blue,
            VisionTime = Timestamp.Zero - Ai.VisionPredictionTime,
            Ball = new FilteredBall
            {
                Timestamp = Timestamp.Zero,
                LastVisibleTimestamp = Timestamp.Zero,
                State = new BallState
                {
                    Position3D = Vector3.Zero
                }
            },
            OppRobots = [],
            OwnRobots = [],
            Referee = new State
            {
                Color = TeamColor.Blue,
                GameState = GameState.Running,
                Gc = new SslReferee
                {
                    BlueTeamOnPositiveHalf = true
                }
            },
            Field = FieldSize.DivisionA,
            Timer = new TyrTimer(),
            Knowledge = new Tyr.Soccer.Knowledge.Knowledge(),
            RoleAssignment = RoleAssignmentResult.Empty,
        };
    }

    [Fact]
    public void CalculateOpponentThreat_DoesNotDropThreatWhenQualityIsZero()
    {
        var opponent = new FilteredRobot
        {
            Id = new RobotId { Id = 1, Team = TeamColor.Yellow },
            Timestamp = Timestamp.Zero,
            Quality = 0f,
            State = new RobotState
            {
                Position = new Vector2(2000f, 0f),
                Velocity = Vector2.Zero
            }
        };

        var threat = Tyr.Soccer.Knowledge.Knowledge.CalculateOpponentThreat(opponent);

        Assert.True(threat > 0f);
    }

    public void Dispose()
    {
        Context.Data.Value = _previous!;
    }
}
