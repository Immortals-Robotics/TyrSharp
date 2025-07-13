namespace Tyr.Soccer.Navigation.Trajectory;

public class Trajectory1DPieced : ITrajectory<float>
{
    private const int MaxParts = 3;
    private readonly Trajectory1DConstantAcc[] _pieces = new Trajectory1DConstantAcc[MaxParts];
    private int _count = 0;

    private const int InvalidIdx = -1;

    public void AddPiece(Trajectory1DConstantAcc piece)
    {
        Assert.IsTrue(_count < MaxParts);
        _pieces[_count] = piece;
        _count++;
    }

    private int FindPieceIndex(float t)
    {
        for (var i = 0; i < _count; i++)
        {
            if (t <= _pieces[i].EndTime)
                return i;
        }

        Log.ZLogError($"No piece found for t = {t}");
        return InvalidIdx;
    }

    private Trajectory1DConstantAcc First => _pieces[0];
    private Trajectory1DConstantAcc Last => _pieces[_count - 1];

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

    public float StartTime => _count == 0 ? 0f : First.StartTime;
    public float EndTime => _count == 0 ? 0f : Last.EndTime;
    public float Duration => Math.Max(0f, EndTime - StartTime);
}