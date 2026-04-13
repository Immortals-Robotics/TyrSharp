using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Math.Shapes;
using Tyr.Soccer.Navigation.Trajectory;

namespace Tyr.Soccer.Navigation.Obstacle;

[Configurable]
public partial class Map
{
    [ConfigEntry] private static float GridCellSize { get; set; } = 400f;
    [ConfigEntry] private static float MaxTrajectorySegmentLength { get; set; } = 125f;
    [ConfigEntry] private static float MinTrajectoryStepTime { get; set; } = 0.01f;

    private readonly List<ObstacleEntry> _obstacles = [];
    private readonly List<int> _candidateBuffer = [];

    private List<int>[] _grid = [];
    private int[] _seenStamp = [];
    private int _queryStamp = 1;

    private bool _indexDirty = true;

    private float _fieldLimitX;
    private float _fieldLimitY;
    private Vector2 _gridMin;
    private int _gridWidth;
    private int _gridHeight;

    public Physicality Physicality { get; set; } = Physicality.All;

    public float BaseMargin { get; set; }

    public void Add(Circle circle, Physicality physicality)
    {
        _obstacles.Add(ObstacleEntry.From(circle, physicality));
        _indexDirty = true;
        Draw.DrawCircle(circle, Color.Rose700.WithAlpha(0.5f));
    }

    public void AddCircle(Vector2 center, float radius, Physicality physicality)
    {
        Add(new Circle { Center = center, Radius = radius }, physicality);
    }

    public void Add(Rectangle rect, Physicality physicality)
    {
        _obstacles.Add(ObstacleEntry.From(rect, physicality));
        _indexDirty = true;
        Draw.DrawRectangle(rect, Color.Rose700.WithAlpha(0.5f));
    }

    public bool Inside(Vector2 point, float margin = 0.0f, Physicality physicality = Physicality.All)
    {
        EnsureSpatialIndex();

        if (IsOutsideField(point))
            return true;

        var marginTotal = BaseMargin + margin;
        var activePhysicality = Physicality & physicality;
        if (activePhysicality == Physicality.None)
            return false;

        var pointMargin = new Vector2(marginTotal);
        CollectCandidates(point - pointMargin, point + pointMargin, activePhysicality);

        foreach (var obstacleIndex in _candidateBuffer)
        {
            if (_obstacles[obstacleIndex].Inside(point, marginTotal))
                return true;
        }

        return false;
    }

    public float Distance(Vector2 point, Physicality physicality = Physicality.All)
    {
        var activePhysicality = Physicality & physicality;
        if (activePhysicality == Physicality.None)
            return float.MaxValue;

        var minDistance = float.MaxValue;

        for (var i = 0; i < _obstacles.Count; i++)
        {
            var obstacle = _obstacles[i];
            if ((obstacle.Physicality & activePhysicality) == 0)
                continue;

            var d = obstacle.Distance(point);
            if (d < minDistance)
                minDistance = d;
            if (minDistance <= 0f)
                return minDistance;
        }

        return minDistance;
    }

    public bool CollisionDetect(LineSegment segment, Physicality physicality = Physicality.All)
    {
        EnsureSpatialIndex();

        var activePhysicality = Physicality & physicality;
        if (activePhysicality == Physicality.None)
            return false;

        if (IsOutsideField(segment.Start) || IsOutsideField(segment.End))
            return true;

        var baseMargin = BaseMargin;
        var marginVec = new Vector2(baseMargin);
        var min = Vector2.Min(segment.Start, segment.End) - marginVec;
        var max = Vector2.Max(segment.Start, segment.End) + marginVec;

        CollectCandidates(min, max, activePhysicality);

        foreach (var obstacleIndex in _candidateBuffer)
        {
            if (_obstacles[obstacleIndex].SegmentCollides(segment, baseMargin))
                return true;
        }

        return false;
    }

    public (bool collided, float time) HasCollision(
        Trajectory2D trajectory,
        float lookAhead = 3.0f, float stepT = 0.1f,
        Physicality physicality = Physicality.All)
    {
        return TraceTrajectory(trajectory, lookAhead, stepT, physicality, stopWhenInside: true);
    }

    public (bool collided, float time) HasCollision(
        Trajectory2DChained trajectory,
        float lookAhead = 3.0f, float stepT = 0.1f,
        Physicality physicality = Physicality.All)
    {
        return TraceTrajectory(trajectory, lookAhead, stepT, physicality, stopWhenInside: true);
    }

    public (bool reachedFree, float time) ReachFree(
        Trajectory2D trajectory,
        float lookAhead = 3.0f, float stepT = 0.1f,
        Physicality physicality = Physicality.All)
    {
        var tEnd = Math.Min(trajectory.EndTime, trajectory.StartTime + lookAhead);
        var currentT = trajectory.StartTime;
        var currentPos = trajectory.GetPosition(currentT);

        if (!Inside(currentPos, 0f, physicality))
            return (true, currentT);

        while (currentT < tEnd)
        {
            var nextT = Math.Min(tEnd, currentT + ComputeTrajectoryStep(trajectory, currentT, stepT));
            if (nextT <= currentT)
                break;

            var nextPos = trajectory.GetPosition(nextT);
            if (!Inside(nextPos, 0f, physicality))
                return (true, nextT);

            currentT = nextT;
            currentPos = nextPos;
        }

        return (false, float.MaxValue);
    }

    public (bool reachedFree, float time) ReachFree(
        Trajectory2DChained trajectory,
        float lookAhead = 3.0f, float stepT = 0.1f,
        Physicality physicality = Physicality.All)
    {
        var tEnd = Math.Min(trajectory.EndTime, trajectory.StartTime + lookAhead);
        var currentT = trajectory.StartTime;
        var currentPos = trajectory.GetPosition(currentT);

        if (!Inside(currentPos, 0f, physicality))
            return (true, currentT);

        while (currentT < tEnd)
        {
            var nextT = Math.Min(tEnd, currentT + ComputeTrajectoryStep(trajectory, currentT, stepT));
            if (nextT <= currentT)
                break;

            var nextPos = trajectory.GetPosition(nextT);
            if (!Inside(nextPos, 0f, physicality))
                return (true, nextT);

            currentT = nextT;
            currentPos = nextPos;
        }

        return (false, float.MaxValue);
    }

    public void Reset()
    {
        _obstacles.Clear();
        _candidateBuffer.Clear();
        Physicality = Physicality.All;
        BaseMargin = 0.0f;
        _indexDirty = true;

        var fieldMargin = Context.Field.BoundaryWidth - Context.Field.RobotRadius + 10f;
        _fieldLimitX = Context.Field.Width + fieldMargin;
        _fieldLimitY = Context.Field.Height + fieldMargin;
    }

    private (bool result, float time) TraceTrajectory(
        Trajectory2D trajectory,
        float lookAhead,
        float stepT,
        Physicality physicality,
        bool stopWhenInside)
    {
        var tEnd = Math.Min(trajectory.EndTime, trajectory.StartTime + lookAhead);
        var currentT = trajectory.StartTime;
        var currentPos = trajectory.GetPosition(currentT);
        var currentInside = Inside(currentPos, 0f, physicality);

        if (currentInside == stopWhenInside)
            return (true, currentT);

        while (currentT < tEnd)
        {
            var nextT = Math.Min(tEnd, currentT + ComputeTrajectoryStep(trajectory, currentT, stepT));
            if (nextT <= currentT)
                break;

            var nextPos = trajectory.GetPosition(nextT);
            var segment = new LineSegment { Start = currentPos, End = nextPos };

            if (stopWhenInside)
            {
                if (CollisionDetect(segment, physicality))
                    return (true, nextT);
            }
            else
            {
                var nextInside = Inside(nextPos, 0f, physicality);
                if (!nextInside)
                    return (true, nextT);
            }

            currentT = nextT;
            currentPos = nextPos;
            currentInside = Inside(currentPos, 0f, physicality);
            if (currentInside == stopWhenInside)
                return (true, currentT);
        }

        return (false, float.MaxValue);
    }

    private (bool result, float time) TraceTrajectory(
        Trajectory2DChained trajectory,
        float lookAhead,
        float stepT,
        Physicality physicality,
        bool stopWhenInside)
    {
        var tEnd = Math.Min(trajectory.EndTime, trajectory.StartTime + lookAhead);
        var currentT = trajectory.StartTime;
        var currentPos = trajectory.GetPosition(currentT);
        var currentInside = Inside(currentPos, 0f, physicality);

        if (currentInside == stopWhenInside)
            return (true, currentT);

        while (currentT < tEnd)
        {
            var nextT = Math.Min(tEnd, currentT + ComputeTrajectoryStep(trajectory, currentT, stepT));
            if (nextT <= currentT)
                break;

            var nextPos = trajectory.GetPosition(nextT);
            var segment = new LineSegment { Start = currentPos, End = nextPos };

            if (stopWhenInside)
            {
                if (CollisionDetect(segment, physicality))
                    return (true, nextT);
            }
            else
            {
                var nextInside = Inside(nextPos, 0f, physicality);
                if (!nextInside)
                    return (true, nextT);
            }

            currentT = nextT;
            currentPos = nextPos;
            currentInside = Inside(currentPos, 0f, physicality);
            if (currentInside == stopWhenInside)
                return (true, currentT);
        }

        return (false, float.MaxValue);
    }

    private float ComputeTrajectoryStep(Trajectory2D trajectory, float currentT, float maxStepTime)
    {
        if (maxStepTime <= 0f)
            return MinTrajectoryStepTime;

        var speed = trajectory.GetVelocity(currentT).Length();
        if (speed <= 1e-3f)
            return maxStepTime;

        var adaptive = MaxTrajectorySegmentLength / speed;
        return Math.Clamp(adaptive, MinTrajectoryStepTime, maxStepTime);
    }

    private float ComputeTrajectoryStep(Trajectory2DChained trajectory, float currentT, float maxStepTime)
    {
        if (maxStepTime <= 0f)
            return MinTrajectoryStepTime;

        var speed = trajectory.GetVelocity(currentT).Length();
        if (speed <= 1e-3f)
            return maxStepTime;

        var adaptive = MaxTrajectorySegmentLength / speed;
        return Math.Clamp(adaptive, MinTrajectoryStepTime, maxStepTime);
    }

    private void EnsureSpatialIndex()
    {
        if (!_indexDirty)
            return;

        var cellSize = Math.Max(1f, GridCellSize);
        _gridMin = new Vector2(-_fieldLimitX, -_fieldLimitY);
        var gridMax = new Vector2(_fieldLimitX, _fieldLimitY);

        _gridWidth = Math.Max(1, (int)MathF.Ceiling((gridMax.X - _gridMin.X) / cellSize));
        _gridHeight = Math.Max(1, (int)MathF.Ceiling((gridMax.Y - _gridMin.Y) / cellSize));

        var gridCellCount = _gridWidth * _gridHeight;
        if (_grid.Length != gridCellCount)
        {
            _grid = new List<int>[gridCellCount];
            for (var i = 0; i < gridCellCount; i++)
                _grid[i] = [];
        }
        else
        {
            for (var i = 0; i < _grid.Length; i++)
                _grid[i].Clear();
        }

        if (_seenStamp.Length < _obstacles.Count)
            _seenStamp = new int[_obstacles.Count];

        for (var i = 0; i < _obstacles.Count; i++)
        {
            var obstacle = _obstacles[i];
            GetCellRange(obstacle.Min, obstacle.Max, out var minX, out var minY, out var maxX, out var maxY);

            for (var y = minY; y <= maxY; y++)
            {
                var rowOffset = y * _gridWidth;
                for (var x = minX; x <= maxX; x++)
                    _grid[rowOffset + x].Add(i);
            }
        }

        _indexDirty = false;
    }

    private void CollectCandidates(Vector2 min, Vector2 max, Physicality activePhysicality)
    {
        EnsureSpatialIndex();
        _candidateBuffer.Clear();
        AdvanceQueryStamp();

        GetCellRange(min, max, out var minX, out var minY, out var maxX, out var maxY);

        for (var y = minY; y <= maxY; y++)
        {
            var rowOffset = y * _gridWidth;
            for (var x = minX; x <= maxX; x++)
            {
                var cell = _grid[rowOffset + x];
                for (var i = 0; i < cell.Count; i++)
                {
                    var obstacleIndex = cell[i];
                    if (_seenStamp[obstacleIndex] == _queryStamp)
                        continue;

                    var obstacle = _obstacles[obstacleIndex];
                    if ((obstacle.Physicality & activePhysicality) == 0)
                        continue;

                    _seenStamp[obstacleIndex] = _queryStamp;
                    _candidateBuffer.Add(obstacleIndex);
                }
            }
        }
    }

    private void AdvanceQueryStamp()
    {
        if (_queryStamp == int.MaxValue)
        {
            Array.Fill(_seenStamp, 0);
            _queryStamp = 1;
            return;
        }

        _queryStamp++;
    }

    private void GetCellRange(Vector2 min, Vector2 max, out int minX, out int minY, out int maxX, out int maxY)
    {
        minX = ClampCellX(min.X);
        maxX = ClampCellX(max.X);
        minY = ClampCellY(min.Y);
        maxY = ClampCellY(max.Y);
    }

    private int ClampCellX(float x)
    {
        var raw = (int)MathF.Floor((x - _gridMin.X) / Math.Max(1f, GridCellSize));
        return Math.Clamp(raw, 0, _gridWidth - 1);
    }

    private int ClampCellY(float y)
    {
        var raw = (int)MathF.Floor((y - _gridMin.Y) / Math.Max(1f, GridCellSize));
        return Math.Clamp(raw, 0, _gridHeight - 1);
    }

    private bool IsOutsideField(Vector2 point)
    {
        return MathF.Abs(point.X) > _fieldLimitX || MathF.Abs(point.Y) > _fieldLimitY;
    }

    private enum ObstacleKind : byte
    {
        Circle,
        Rectangle,
    }

    private readonly record struct ObstacleEntry
    {
        public required ObstacleKind Kind { get; init; }
        public required Physicality Physicality { get; init; }
        public required Circle Circle { get; init; }
        public required Rectangle Rectangle { get; init; }
        public required Vector2 Min { get; init; }
        public required Vector2 Max { get; init; }

        public static ObstacleEntry From(Circle circle, Physicality physicality)
        {
            var radiusVec = new Vector2(circle.Radius);
            return new ObstacleEntry
            {
                Kind = ObstacleKind.Circle,
                Physicality = physicality,
                Circle = circle,
                Rectangle = default,
                Min = circle.Center - radiusVec,
                Max = circle.Center + radiusVec,
            };
        }

        public static ObstacleEntry From(Rectangle rect, Physicality physicality)
        {
            return new ObstacleEntry
            {
                Kind = ObstacleKind.Rectangle,
                Physicality = physicality,
                Circle = default,
                Rectangle = rect,
                Min = rect.Min,
                Max = rect.Max,
            };
        }

        public bool Inside(Vector2 point, float margin)
        {
            return Kind switch
            {
                ObstacleKind.Circle => Circle.Inside(point, margin),
                _ => Rectangle.Inside(point, margin),
            };
        }

        public float Distance(Vector2 point)
        {
            return Kind switch
            {
                ObstacleKind.Circle => Circle.Distance(point),
                _ => Rectangle.Distance(point),
            };
        }

        public bool SegmentCollides(LineSegment segment, float margin)
        {
            return Kind switch
            {
                ObstacleKind.Circle => SegmentCollidesCircle(segment, Circle, margin),
                _ => SegmentCollidesRectangle(segment, Rectangle, margin),
            };
        }

        private static bool SegmentCollidesCircle(LineSegment segment, Circle circle, float margin)
        {
            var radius = circle.Radius + margin;
            var closest = segment.ClosestPoint(circle.Center);
            return Vector2.DistanceSquared(closest, circle.Center) <= radius * radius;
        }

        private static bool SegmentCollidesRectangle(LineSegment segment, Rectangle rect, float margin)
        {
            var expanded = Rectangle.FromCornerAndSize(rect.Min - new Vector2(margin), rect.Size + new Vector2(2f * margin));
            if (expanded.Inside(segment.Start) || expanded.Inside(segment.End))
                return true;

            var delta = segment.End - segment.Start;
            var tMin = 0f;
            var tMax = 1f;

            if (!ClipAxis(segment.Start.X, delta.X, expanded.Min.X, expanded.Max.X, ref tMin, ref tMax))
                return false;

            if (!ClipAxis(segment.Start.Y, delta.Y, expanded.Min.Y, expanded.Max.Y, ref tMin, ref tMax))
                return false;

            return tMax >= tMin;
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
}
