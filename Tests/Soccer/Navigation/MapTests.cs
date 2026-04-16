using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Math.Shapes;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Navigation.Obstacle;
using Tyr.Soccer.Navigation.Trajectory;
using Tyr.Soccer.RoleAssignment;
using Tyr.Soccer.Robot;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using TyrTimer = Tyr.Common.Time.Timer;

namespace Tyr.Tests.Soccer.Navigation;

public sealed class MapTests
{
    [Fact]
    public void CollisionDetect_DetectsTangentCircleSegment()
    {
        using var _ = new SoccerContextScope();
        var map = CreateMap();
        map.AddCircle(Vector2.Zero, 100f, Physicality.Physical);

        var collided = map.CollisionDetect(new LineSegment
        {
            Start = new Vector2(-200f, 100f),
            End = new Vector2(200f, 100f),
        }, Physicality.Physical);

        Assert.True(collided);
    }

    [Fact]
    public void CollisionDetect_DetectsRectangleEdgeGrazing()
    {
        using var _ = new SoccerContextScope();
        var map = CreateMap();
        map.Add(Rectangle.FromCornerAndSize(new Vector2(-100f, -50f), 200f, 100f), Physicality.Virtual);

        var collided = map.CollisionDetect(new LineSegment
        {
            Start = new Vector2(-300f, 50f),
            End = new Vector2(300f, 50f),
        }, Physicality.Virtual);

        Assert.True(collided);
    }

    [Fact]
    public void Inside_MatchesBruteForceAcrossRandomQueries()
    {
        using var _ = new SoccerContextScope();
        var map = CreateRepresentativeMap();
        var random = new Random(1234);

        for (var i = 0; i < 400; i++)
        {
            var point = RandomPoint(random);
            var margin = random.NextSingle() * 140f;
            var actual = map.Inside(point, margin);
            var expected = BruteForceInside(map, point, margin, Physicality.All);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void Distance_MatchesBruteForceAcrossRandomQueries()
    {
        using var _ = new SoccerContextScope();
        var map = CreateRepresentativeMap();
        var random = new Random(4321);

        for (var i = 0; i < 300; i++)
        {
            var point = RandomPoint(random);
            var actual = map.Distance(point);
            var expected = BruteForceDistance(map, point, Physicality.All);
            Assert.Equal(expected, actual, 4);
        }
    }

    [Fact]
    public void CollisionDetect_MatchesBruteForceAcrossRandomSegments()
    {
        using var _ = new SoccerContextScope();
        var map = CreateRepresentativeMap();
        var random = new Random(999);

        for (var i = 0; i < 250; i++)
        {
            var segment = new LineSegment
            {
                Start = RandomPoint(random),
                End = RandomPoint(random),
            };

            var actual = map.CollisionDetect(segment);
            var expected = BruteForceCollision(map, segment, Physicality.All);
            Assert.Equal(expected, actual);
        }
    }

    [Fact]
    public void HasCollision_DetectsCrowdedTrajectory()
    {
        using var _ = new SoccerContextScope();
        var map = CreateMap();
        map.BaseMargin = 90f;
        map.AddCircle(new Vector2(0f, 0f), 100f, Physicality.Physical);
        map.Add(Rectangle.FromCenterAndSize(new Vector2(600f, 0f), 200f, 600f), Physicality.Virtual);

        var trajectory = CreateLinearTrajectory(
            new Vector2(-1000f, 0f),
            new Vector2(1000f, 0f),
            duration: 1f);

        var collision = map.HasCollision(trajectory, lookAhead: trajectory.Duration);

        Assert.True(collision.collided);
        Assert.InRange(collision.time, trajectory.StartTime, trajectory.EndTime);
    }

    private static Map CreateMap()
    {
        var map = new Map();
        map.Reset();
        return map;
    }

    private static Map CreateRepresentativeMap()
    {
        var map = CreateMap();
        map.BaseMargin = 90f;

        map.AddCircle(new Vector2(-1200f, -800f), 120f, Physicality.Physical);
        map.AddCircle(new Vector2(800f, 900f), 150f, Physicality.Physical);
        map.AddCircle(new Vector2(1500f, -500f), 110f, Physicality.Virtual);
        map.Add(Rectangle.FromCenterAndSize(new Vector2(-2500f, 0f), 900f, 1600f), Physicality.Virtual);
        map.Add(Rectangle.FromCenterAndSize(new Vector2(2500f, 1200f), 1100f, 700f), Physicality.Physical);
        map.Add(Rectangle.FromCenterAndSize(new Vector2(0f, -2000f), 1400f, 600f), Physicality.Virtual);

        return map;
    }

    private static bool BruteForceInside(Map map, Vector2 point, float margin, Physicality physicality)
    {
        var activePhysicality = map.Physicality & physicality;
        var marginTotal = map.BaseMargin + margin;
        var fieldMargin = Context.Field.BoundaryWidth - Context.Field.RobotRadius + 10f;
        if (MathF.Abs(point.X) > Context.Field.Width + fieldMargin || MathF.Abs(point.Y) > Context.Field.Height + fieldMargin)
            return true;

        foreach (var obstacle in EnumerateRepresentativeObstacles())
        {
            if ((obstacle.Physicality & activePhysicality) == 0)
                continue;
            if (obstacle.Inside(point, marginTotal))
                return true;
        }

        return false;
    }

    private static float BruteForceDistance(Map map, Vector2 point, Physicality physicality)
    {
        var activePhysicality = map.Physicality & physicality;
        var minDistance = float.MaxValue;

        foreach (var obstacle in EnumerateRepresentativeObstacles())
        {
            if ((obstacle.Physicality & activePhysicality) == 0)
                continue;

            minDistance = MathF.Min(minDistance, obstacle.Distance(point));
        }

        return minDistance;
    }

    private static bool BruteForceCollision(Map map, LineSegment segment, Physicality physicality)
    {
        var activePhysicality = map.Physicality & physicality;
        var fieldMargin = Context.Field.BoundaryWidth - Context.Field.RobotRadius + 10f;
        var limitX = Context.Field.Width + fieldMargin;
        var limitY = Context.Field.Height + fieldMargin;
        if (MathF.Abs(segment.Start.X) > limitX || MathF.Abs(segment.Start.Y) > limitY ||
            MathF.Abs(segment.End.X) > limitX || MathF.Abs(segment.End.Y) > limitY)
        {
            return true;
        }

        foreach (var obstacle in EnumerateRepresentativeObstacles())
        {
            if ((obstacle.Physicality & activePhysicality) == 0)
                continue;

            if (obstacle.Collides(segment, map.BaseMargin))
                return true;
        }

        return false;
    }

    private static IEnumerable<TestObstacle> EnumerateRepresentativeObstacles()
    {
        yield return TestObstacle.CreateCircle(new Vector2(-1200f, -800f), 120f, Physicality.Physical);
        yield return TestObstacle.CreateCircle(new Vector2(800f, 900f), 150f, Physicality.Physical);
        yield return TestObstacle.CreateCircle(new Vector2(1500f, -500f), 110f, Physicality.Virtual);
        yield return TestObstacle.CreateRectangle(Rectangle.FromCenterAndSize(new Vector2(-2500f, 0f), 900f, 1600f), Physicality.Virtual);
        yield return TestObstacle.CreateRectangle(Rectangle.FromCenterAndSize(new Vector2(2500f, 1200f), 1100f, 700f), Physicality.Physical);
        yield return TestObstacle.CreateRectangle(Rectangle.FromCenterAndSize(new Vector2(0f, -2000f), 1400f, 600f), Physicality.Virtual);
    }

    private static Vector2 RandomPoint(Random random)
    {
        var limitX = Context.Field.Width + Context.Field.BoundaryWidth;
        var limitY = Context.Field.Height + Context.Field.BoundaryWidth;
        return new Vector2(
            random.NextSingle() * 2f * limitX - limitX,
            random.NextSingle() * 2f * limitY - limitY);
    }

    private static Trajectory2D CreateLinearTrajectory(Vector2 start, Vector2 end, float duration)
    {
        var velocity = duration <= 0f ? Vector2.Zero : (end - start) / duration;

        return new Trajectory2D
        {
            TrajectoryX = Trajectory1DPieced.Create(new Trajectory1DConstantAcc
            {
                StartPosition = start.X,
                StartVelocity = velocity.X,
                Acceleration = 0f,
                StartTime = 0f,
                EndTime = duration,
            }),
            TrajectoryY = Trajectory1DPieced.Create(new Trajectory1DConstantAcc
            {
                StartPosition = start.Y,
                StartVelocity = velocity.Y,
                Acceleration = 0f,
                StartTime = 0f,
                EndTime = duration,
            }),
        };
    }

    private readonly record struct TestObstacle
    {
        public Circle Circle { get; init; }
        public Rectangle Rectangle { get; init; }
        public bool IsCircle { get; init; }
        public Physicality Physicality { get; init; }

        public static TestObstacle CreateCircle(Vector2 center, float radius, Physicality physicality) => new()
        {
            Circle = new Circle { Center = center, Radius = radius },
            Physicality = physicality,
            IsCircle = true,
        };

        public static TestObstacle CreateRectangle(Rectangle rectangle, Physicality physicality) => new()
        {
            Rectangle = rectangle,
            Physicality = physicality,
            IsCircle = false,
        };

        public bool Inside(Vector2 point, float margin)
        {
            return IsCircle ? Circle.Inside(point, margin) : Rectangle.Inside(point, margin);
        }

        public float Distance(Vector2 point)
        {
            return IsCircle ? Circle.Distance(point) : Rectangle.Distance(point);
        }

        public bool Collides(LineSegment segment, float margin)
        {
            if (IsCircle)
            {
                var closest = segment.ClosestPoint(Circle.Center);
                var radius = Circle.Radius + margin;
                return Vector2.DistanceSquared(closest, Circle.Center) <= radius * radius;
            }

            var expanded = Rectangle.FromCornerAndSize(Rectangle.Min - new Vector2(margin), Rectangle.Size + new Vector2(2f * margin));
            if (expanded.Inside(segment.Start) || expanded.Inside(segment.End))
                return true;

            var delta = segment.End - segment.Start;
            var tMin = 0f;
            var tMax = 1f;

            return ClipAxis(segment.Start.X, delta.X, expanded.Min.X, expanded.Max.X, ref tMin, ref tMax) &&
                   ClipAxis(segment.Start.Y, delta.Y, expanded.Min.Y, expanded.Max.Y, ref tMin, ref tMax) &&
                   tMax >= tMin;
        }

        private static bool ClipAxis(float start, float delta, float min, float max, ref float tMin, ref float tMax)
        {
            if (MathF.Abs(delta) < 1e-6f)
                return start >= min && start <= max;

            var invDelta = 1f / delta;
            var t1 = (min - start) * invDelta;
            var t2 = (max - start) * invDelta;
            if (t1 > t2)
                (t1, t2) = (t2, t1);

            tMin = MathF.Max(tMin, t1);
            tMax = MathF.Min(tMax, t2);
            return tMin <= tMax;
        }
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
                VisionTime = Timestamp.Zero,
                Ball = new FilteredBall
                {
                    Timestamp = Timestamp.Zero,
                    LastVisibleTimestamp = Timestamp.Zero,
                    State = new BallState { Position3D = Vector3.Zero },
                },
                OppRobots = [],
                OwnRobots = [],
                Referee = new State
                {
                    Color = TeamColor.Blue,
                    Gc = new SslReferee { BlueTeamOnPositiveHalf = true },
                },
                Field = FieldSize.DivisionA,
                Timer = new TyrTimer(),
                Knowledge = new Knowledge(),
                RoleAssignment = RoleAssignmentResult.Empty,
            };
        }

        public void Dispose()
        {
            Context.Data.Value = _previous!;
        }
    }
}
