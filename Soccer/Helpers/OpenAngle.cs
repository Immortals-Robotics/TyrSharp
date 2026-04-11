using System.Numerics;
using Tyr.Common.Config;
using Tyr.Common.Data;
using Tyr.Common.Math;
using Tyr.Soccer.Robot;

namespace Tyr.Soccer.Helpers;

internal readonly record struct OpenAngle(Angle Center, Angle Magnitude)
{
    private const int MaxObstacleArcs = 64;
    private const int OpenAngleSamples = 100;

    private static readonly AngleMedianFilter[] CenterFilters = CreateCenterFilters();

    public static OpenAngle CalculateOpenAngleToGoal(Vector2 pos, Robot.Robot robot)
    {
        var midGoal = Context.Field.OppGoal();
        var topGoal = Context.Field.OppGoalPostTop();
        var bottomGoal = Context.Field.OppGoalPostBottom();

        topGoal += Vector2.Normalize(midGoal - topGoal) * 25f;
        bottomGoal += Vector2.Normalize(midGoal - bottomGoal) * 25f;

        var midGoalAngle = MathF.Atan2(midGoal.Y - pos.Y, midGoal.X - pos.X);
        var goalStart = MathF.Atan2(bottomGoal.Y - pos.Y, bottomGoal.X - pos.X);
        var goalEnd = MathF.Atan2(topGoal.Y - pos.Y, topGoal.X - pos.X);

        var wraps = MathF.Abs(goalStart - goalEnd) > MathF.PI;
        if (goalStart > goalEnd)
        {
            (goalStart, goalEnd) = (goalEnd, goalStart);
        }

        if (wraps)
        {
            (goalStart, goalEnd) = (goalEnd, goalStart);
        }

        Span<float> blockedStarts = stackalloc float[MaxObstacleArcs];
        Span<float> blockedEnds = stackalloc float[MaxObstacleArcs];
        var obstacleCount = 0;

        for (var i = 0; i < Context.OwnRobots.Count; i++)
        {
            var own = Context.OwnRobots[i];
            if (!own.Seen || own.Id == robot.Id)
            {
                continue;
            }

            if (AddObstacleArc(pos, own.Position, goalStart, goalEnd, wraps, blockedStarts, blockedEnds, ref obstacleCount))
            {
                return FilterCenter(robot.Id, new OpenAngle(Angle.FromRad(midGoalAngle), Angle.Zero));
            }
        }

        for (var i = 0; i < Context.OppRobots.Count; i++)
        {
            if (AddObstacleArc(pos, Context.OppRobots[i].State.Position, goalStart, goalEnd, wraps, blockedStarts, blockedEnds, ref obstacleCount))
            {
                return FilterCenter(robot.Id, new OpenAngle(Angle.FromRad(midGoalAngle), Angle.Zero));
            }
        }

        if (obstacleCount == 0)
        {
            return FilterCenter(robot.Id, new OpenAngle(Angle.FromRad(midGoalAngle), Angle.FromRad(MathF.Abs(goalEnd - goalStart))));
        }

        var step = (goalEnd - goalStart) / OpenAngleSamples;
        if (wraps)
        {
            step += 2f * MathF.PI / OpenAngleSamples;
        }

        Span<bool> blocked = stackalloc bool[OpenAngleSamples];
        for (var i = 0; i < OpenAngleSamples; i++)
        {
            var angle = goalStart + i * step;

            for (var j = 0; j < obstacleCount; j++)
            {
                var start = blockedStarts[j];
                var end = blockedEnds[j];
                var angleToCheck = angle;

                if (wraps)
                {
                    if (start < 0f) start += 2f * MathF.PI;
                    if (end < 0f) end += 2f * MathF.PI;
                    if (angleToCheck < 0f) angleToCheck += 2f * MathF.PI;
                }

                if (angleToCheck >= start && angleToCheck < end)
                {
                    blocked[i] = true;
                    break;
                }
            }
        }

        var maxFree = 0;
        var freeCount = 0;
        var bestSample = 0f;

        for (var i = 0; i < OpenAngleSamples; i++)
        {
            if (blocked[i])
            {
                if (freeCount > maxFree)
                {
                    maxFree = freeCount;
                    bestSample = i - maxFree * 0.5f;
                }

                freeCount = 0;
            }
            else
            {
                freeCount++;
            }
        }

        if (freeCount > maxFree)
        {
            maxFree = freeCount;
            bestSample = OpenAngleSamples - maxFree * 0.5f;
        }

        if (maxFree == 0)
        {
            return FilterCenter(robot.Id, new OpenAngle(Angle.FromRad(midGoalAngle), Angle.Zero));
        }

        var bestAngle = Angle.FromRad(goalStart + bestSample * step).RadNormalized;
        return FilterCenter(robot.Id, new OpenAngle(Angle.FromRad(bestAngle), Angle.FromRad(maxFree * step)));
    }

    private static bool AddObstacleArc(
        Vector2 origin,
        Vector2 obstacle,
        float goalStart,
        float goalEnd,
        bool wraps,
        Span<float> blockedStarts,
        Span<float> blockedEnds,
        ref int obstacleCount)
    {
        if (obstacleCount >= MaxObstacleArcs)
        {
            return false;
        }

        var distance = Vector2.Distance(origin, obstacle);
        if (Utils.ApproximatelyZero(distance))
        {
            return false;
        }

        var start = MathF.Atan2(obstacle.Y - origin.Y, obstacle.X - origin.X);
        var delta = MathF.Abs(MathF.Atan(90f / distance));
        var end = start + delta;
        start -= delta;

        start = Angle.FromRad(start).RadNormalized;
        end = Angle.FromRad(end).RadNormalized;

        var startCompare = start;
        var endCompare = end;
        var goalStartCompare = goalStart;
        var goalEndCompare = goalEnd;

        if (wraps)
        {
            if (startCompare < 0f) startCompare += 2f * MathF.PI;
            if (endCompare < 0f) endCompare += 2f * MathF.PI;
            if (goalStartCompare < 0f) goalStartCompare += 2f * MathF.PI;
            if (goalEndCompare < 0f) goalEndCompare += 2f * MathF.PI;
        }

        if (endCompare > goalEndCompare && startCompare < goalStartCompare)
        {
            return true;
        }

        if (startCompare > goalEndCompare || endCompare < goalStartCompare)
        {
            return false;
        }

        if (startCompare < goalStartCompare)
        {
            start = goalStart;
        }

        if (endCompare > goalEndCompare)
        {
            end = goalEnd;
        }

        blockedStarts[obstacleCount] = start;
        blockedEnds[obstacleCount] = end;
        obstacleCount++;
        return false;
    }

    private static OpenAngle FilterCenter(int robotId, OpenAngle openAngle)
    {
        if ((uint)robotId >= (uint)CenterFilters.Length)
        {
            return openAngle;
        }

        var filter = CenterFilters[robotId];
        filter.Add(openAngle.Center);
        return openAngle with { Center = filter.Current };
    }

    private static AngleMedianFilter[] CreateCenterFilters()
    {
        var filters = new AngleMedianFilter[CommonConfigs.MaxRobots];
        for (var i = 0; i < filters.Length; i++)
        {
            filters[i] = new AngleMedianFilter();
        }

        return filters;
    }

    private sealed class AngleMedianFilter(int size = 10)
    {
        private readonly float[] _buffer = new float[size];
        private readonly float[] _sortBuffer = new float[size];
        private int _count;
        private int _nextIndex;

        public Angle Current { get; private set; }

        public void Add(Angle value)
        {
            var reference = value.RadNormalized;
            _buffer[_nextIndex] = reference;
            _nextIndex = (_nextIndex + 1) % _buffer.Length;

            if (_count < _buffer.Length)
            {
                _count++;
            }

            for (var i = 0; i < _count; i++)
            {
                var sample = _buffer[i];

                while (sample - reference > MathF.PI)
                {
                    sample -= 2f * MathF.PI;
                }

                while (sample - reference < -MathF.PI)
                {
                    sample += 2f * MathF.PI;
                }

                _sortBuffer[i] = sample;
            }

            var span = _sortBuffer.AsSpan(0, _count);
            span.Sort();
            Current = Angle.FromRad(span[span.Length / 2]);
        }
    }
}
