using MemoryPack;

namespace Tyr.Soccer.Navigation.Trajectory;

[MemoryPackable]
public partial class Trajectory1DPieced : ITrajectory<float>
{
    private const int MaxParts = 3;
    private const int InvalidIdx = -1;

    public List<Trajectory1DConstantAcc> Pieces { get; private set; } = [];

    public void AddPiece(Trajectory1DConstantAcc piece)
    {
        Assert.IsTrue(Pieces.Count < MaxParts);
        Pieces.Add(piece);
    }

    private int FindPieceIndex(float t)
    {
        for (var i = 0; i < Pieces.Count; i++)
        {
            if (t <= Pieces[i].EndTime)
                return i;
        }

        Log.ZLogError($"No piece found for t = {t}");
        return InvalidIdx;
    }

    private Trajectory1DConstantAcc First => Pieces[0];
    private Trajectory1DConstantAcc Last => Pieces[^1];

    public float GetPosition(float t)
    {
        t = Math.Clamp(t, StartTime, EndTime);
        var idx = FindPieceIndex(t);
        return idx == InvalidIdx ? 0f : Pieces[idx].GetPosition(t);
    }

    public float GetVelocity(float t)
    {
        t = Math.Clamp(t, StartTime, EndTime);
        var idx = FindPieceIndex(t);
        return idx == InvalidIdx ? 0f : Pieces[idx].GetVelocity(t);
    }

    public float GetAcceleration(float t)
    {
        t = Math.Clamp(t, StartTime, EndTime);
        var idx = FindPieceIndex(t);
        return idx == InvalidIdx ? 0f : Pieces[idx].GetAcceleration(t);
    }

    public float StartTime => Pieces.Count == 0 ? 0f : First.StartTime;
    public float EndTime => Pieces.Count == 0 ? 0f : Last.EndTime;
    public float Duration => Math.Max(0f, EndTime - StartTime);
}
