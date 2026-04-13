using System.Runtime.CompilerServices;

namespace Tyr.Soccer.Navigation.Trajectory;

[InlineArray(Trajectory1DPieced.MaxParts)]
public struct TrajectoryPieceBuffer
{
    private Trajectory1DConstantAcc _element0;
}

public readonly struct Trajectory1DPieced
{
    internal const int MaxParts = 3;
    private const int InvalidIdx = -1;

    private readonly TrajectoryPieceBuffer _pieces;
    public int Count { get; }

    private Trajectory1DPieced(int count, TrajectoryPieceBuffer pieces)
    {
        Count = count;
        _pieces = pieces;
    }

    public static Trajectory1DPieced Empty => default;

    public static Trajectory1DPieced Create(
        Trajectory1DConstantAcc piece0)
    {
        var pieces = default(TrajectoryPieceBuffer);
        pieces[0] = piece0;
        return new Trajectory1DPieced(1, pieces);
    }

    public static Trajectory1DPieced Create(
        Trajectory1DConstantAcc piece0,
        Trajectory1DConstantAcc piece1)
    {
        var pieces = default(TrajectoryPieceBuffer);
        pieces[0] = piece0;
        pieces[1] = piece1;
        return new Trajectory1DPieced(2, pieces);
    }

    public static Trajectory1DPieced Create(
        Trajectory1DConstantAcc piece0,
        Trajectory1DConstantAcc piece1,
        Trajectory1DConstantAcc piece2)
    {
        var pieces = default(TrajectoryPieceBuffer);
        pieces[0] = piece0;
        pieces[1] = piece1;
        pieces[2] = piece2;
        return new Trajectory1DPieced(3, pieces);
    }

    private int FindPieceIndex(float t)
    {
        for (var i = 0; i < Count; i++)
        {
            if (t <= _pieces[i].EndTime)
                return i;
        }

        Log.ZLogError($"No piece found for t = {t}");
        return InvalidIdx;
    }

    public float GetPosition(float t)
    {
        t = Math.Clamp(t, StartTime, EndTime);
        var idx = FindPieceIndex(t);
        return idx == InvalidIdx ? 0f : _pieces[idx].GetPosition(t);
    }

    public float GetVelocity(float t)
    {
        t = Math.Clamp(t, StartTime, EndTime);
        var idx = FindPieceIndex(t);
        return idx == InvalidIdx ? 0f : _pieces[idx].GetVelocity(t);
    }

    public float GetAcceleration(float t)
    {
        t = Math.Clamp(t, StartTime, EndTime);
        var idx = FindPieceIndex(t);
        return idx == InvalidIdx ? 0f : _pieces[idx].GetAcceleration(t);
    }
 
    public Trajectory1DConstantAcc this[int index]
    {
        get
        {
            Assert.IsTrue(index >= 0 && index < Count);
            return _pieces[index];
        }
    }

    public float StartTime => Count == 0 ? 0f : _pieces[0].StartTime;
    public float EndTime => Count == 0 ? 0f : _pieces[Count - 1].EndTime;
    public float Duration => Math.Max(0f, EndTime - StartTime);
}
