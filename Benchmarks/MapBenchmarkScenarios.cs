using System.Diagnostics;
using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Extensions;
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

internal static class MapBenchmarkScenarios
{
    public static IReadOnlyList<BenchmarkScenario> CreateScenarios(BenchmarkConfig config)
    {
        return
        [
            new BenchmarkScenario(
                "Map.Inside.Representative",
                "Representative point-inside queries over robot and virtual obstacles",
                () => RunInside(config)),
            new BenchmarkScenario(
                "Map.Distance.Representative",
                "Representative distance queries over robot and virtual obstacles",
                () => RunDistance(config)),
            new BenchmarkScenario(
                "Map.CollisionDetect.Representative",
                "Exact line-segment collision queries over robot and virtual obstacles",
                () => RunCollisionDetect(config)),
            new BenchmarkScenario(
                "Map.HasCollision.Representative",
                "Adaptive trajectory collision checks over robot and virtual obstacles",
                () => RunHasCollision(config)),
        ];
    }

    private static ScenarioResult RunInside(BenchmarkConfig config)
    {
        return RunMapScenario(config.QueryEntryCount, (map, samples) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var hits = 0;

            for (var i = 0; i < samples.Count; i++)
            {
                if (map.Inside(samples[i]))
                    hits++;
            }

            stopwatch.Stop();
            return new ScenarioResult(stopwatch.Elapsed, samples.Count, hits);
        });
    }

    private static ScenarioResult RunDistance(BenchmarkConfig config)
    {
        return RunMapScenario(config.QueryEntryCount, (map, samples) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var sum = 0f;

            for (var i = 0; i < samples.Count; i++)
                sum += map.Distance(samples[i]);

            stopwatch.Stop();
            return new ScenarioResult(stopwatch.Elapsed, samples.Count, (int)sum);
        });
    }

    private static ScenarioResult RunCollisionDetect(BenchmarkConfig config)
    {
        return RunMapScenario(config.QueryEntryCount, (map, samples) =>
        {
            var stopwatch = Stopwatch.StartNew();
            var hits = 0;

            for (var i = 1; i < samples.Count; i++)
            {
                if (map.CollisionDetect(new LineSegment { Start = samples[i - 1], End = samples[i] }))
                    hits++;
            }

            stopwatch.Stop();
            return new ScenarioResult(stopwatch.Elapsed, samples.Count - 1, hits);
        });
    }

    private static ScenarioResult RunHasCollision(BenchmarkConfig config)
    {
        return RunMapScenario(Math.Max(2_000, config.QueryEntryCount / 4), (map, samples) =>
        {
            var trajectories = new List<Trajectory2D>(samples.Count / 2);
            for (var i = 1; i < samples.Count; i += 2)
            {
                trajectories.Add(TrajectoryBangBang.Make2D(
                    samples[i - 1],
                    Vector2.Zero,
                    samples[i],
                    VelocityProfile.Mamooli));
            }

            var stopwatch = Stopwatch.StartNew();
            var hits = 0;

            for (var i = 0; i < trajectories.Count; i++)
            {
                if (map.HasCollision(trajectories[i]).collided)
                    hits++;
            }

            stopwatch.Stop();
            return new ScenarioResult(stopwatch.Elapsed, trajectories.Count, hits);
        });
    }

    private static ScenarioResult RunMapScenario(
        int sampleCount,
        Func<Map, List<Vector2>, ScenarioResult> run)
    {
        ContextData? previous = Context.Data.Value;
        try
        {
            Context.Data.Value = CreateContextData();

            var map = CreateRepresentativeMap();
            var samples = CreateSamplePoints(sampleCount);
            return run(map, samples);
        }
        finally
        {
            Context.Data.Value = previous!;
        }
    }

    private static Map CreateRepresentativeMap()
    {
        var map = new Map();
        map.Reset();
        map.BaseMargin = Context.Field.RobotRadius;

        for (var i = 0; i < 11; i++)
        {
            var y = -3500f + i * 700f;
            map.AddCircle(new Vector2(-2200f + (i % 3) * 180f, y), 90f + (i % 4) * 15f, Physicality.Physical);
            map.AddCircle(new Vector2(2200f - (i % 3) * 180f, -y), 90f + ((i + 2) % 4) * 15f, Physicality.Physical);
        }

        map.Add(Rectangle.FromCenterAndSize(new Vector2(-5100f, 0f), 2100f, 3600f), Physicality.Virtual);
        map.Add(Rectangle.FromCenterAndSize(new Vector2(5100f, 0f), 2100f, 3600f), Physicality.Virtual);
        map.Add(Rectangle.FromCenterAndSize(new Vector2(0f, 0f), 250f, 9000f), Physicality.Virtual);
        map.Add(Rectangle.FromCenterAndSize(new Vector2(0f, 0f), 12000f, 220f), Physicality.Virtual);

        for (var i = 0; i < 12; i++)
        {
            var x = -4200f + i * 700f;
            var y = (i % 2 == 0 ? -1f : 1f) * (600f + i * 80f);
            map.AddCircle(new Vector2(x, y), 180f, Physicality.Virtual);
        }

        return map;
    }

    private static List<Vector2> CreateSamplePoints(int sampleCount)
    {
        var random = new Random(42);
        var marginX = Context.Field.Width + Context.Field.BoundaryWidth;
        var marginY = Context.Field.Height + Context.Field.BoundaryWidth;
        var samples = new List<Vector2>(sampleCount);

        for (var i = 0; i < sampleCount; i++)
        {
            samples.Add(new Vector2(
                random.Get(-marginX, marginX),
                random.Get(-marginY, marginY)));
        }

        return samples;
    }

    private static ContextData CreateContextData()
    {
        return new ContextData
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
}
