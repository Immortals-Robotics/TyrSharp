using Tyr.Common.Debug;

namespace Tyr.Gui.Db;

public interface IDebugDb : IDisposable
{
    void Append<T>(T entry) where T : IEntry;
    IEnumerable<T> Query<T>(string module, Timestamp t0, Timestamp t1) where T : IEntry;
    IEnumerable<T> QueryAll<T>(Timestamp t0, Timestamp t1) where T : IEntry;
    Meta GetSourceLocation(int id);

    void AppendFrame(Frame frame);
    IEnumerable<Frame> QueryFrames(string module, Timestamp t0, Timestamp t1);
    (Timestamp Start, Timestamp End)? GetFrameAt(string module, Timestamp t);
}
