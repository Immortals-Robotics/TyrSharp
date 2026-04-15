using System.Numerics;
using Tyr.Common;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Math;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Tactics;
using Tyr.Vision.Trajectory;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using TyrTimer = Tyr.Common.Time.Timer;

namespace Tyr.Tests.Soccer.Tactics;

public sealed class DefTests : IDisposable
{
    private readonly SoccerContextScope _context = new();

    public DefTests()
    {
        ServiceLocator.BallTrajectoryFactory = new BallTrajectoryFactory();
    }

    public void Dispose()
    {
        _context.Dispose();
    }

    [Fact]
    public void CalculateStaticPos_ClampsBallXToPositiveGoalLine()
    {
        _context.SetOurSide(TeamSide.Right);

        var onGoalLine = _context.CalculateStaticPosForBall(new Vector2(Context.Field.Width, 1200f), defId: 1);
        var behindGoalLine = _context.CalculateStaticPosForBall(new Vector2(Context.Field.Width + 700f, 1200f), defId: 1);

        Assert.Equal(onGoalLine, behindGoalLine, Utils.ApproximatelyEqual);
    }

    [Fact]
    public void CalculateStaticPos_ClampsBallXToNegativeGoalLine()
    {
        _context.SetOurSide(TeamSide.Left);

        var onGoalLine = _context.CalculateStaticPosForBall(new Vector2(-Context.Field.Width, -1200f), defId: 2);
        var behindGoalLine = _context.CalculateStaticPosForBall(new Vector2(-Context.Field.Width - 700f, -1200f), defId: 2);

        Assert.Equal(onGoalLine, behindGoalLine, Utils.ApproximatelyEqual);
    }

    private sealed class SoccerContextScope : IDisposable
    {
        private readonly ContextData? _previous;

        public SoccerContextScope()
        {
            _previous = Context.Data.Value;
            Context.Data.Value = new ContextData
            {
                Color = TeamColor.Blue,
                VisionTime = Timestamp.Zero - Ai.VisionPredictionTime,
                Ball = CreateBall(Vector2.Zero),
                OppRobots = [],
                OwnRobots = [],
                Referee = CreateReferee(TeamSide.Right),
                Field = FieldSize.DivisionA,
                Timer = new TyrTimer(),
                Knowledge = new Knowledge(),
            };
        }

        public void SetOurSide(TeamSide side)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                Referee = CreateReferee(side)
            };
        }

        public Vector2 CalculateStaticPosForBall(Vector2 ballPosition, int defId)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                Ball = CreateBall(ballPosition)
            };

            Context.Knowledge.Update();
            return Def.CalculateStaticPos(defId);
        }

        public void Dispose()
        {
            Context.Data.Value = _previous!;
        }

        private static FilteredBall CreateBall(Vector2 position)
        {
            return new FilteredBall
            {
                Timestamp = Timestamp.Zero,
                LastVisibleTimestamp = Timestamp.Zero,
                State = new BallState
                {
                    Position3D = new Vector3(position, 0f),
                    Velocity = Vector3.Zero,
                    Acceleration = Vector3.Zero,
                    SpinRadians = Vector2.Zero,
                }
            };
        }

        private static State CreateReferee(TeamSide side)
        {
            return new State
            {
                Color = TeamColor.Blue,
                Gc = new SslReferee
                {
                    BlueTeamOnPositiveHalf = side == TeamSide.Right
                },
            };
        }
    }
}
