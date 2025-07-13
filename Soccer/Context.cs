using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Vision = Tyr.Common.Vision.Data;
using Referee = Tyr.Common.Referee.Data;
using Timer = Tyr.Common.Time.Timer;

namespace Tyr.Soccer;

internal sealed record ContextData
{
    internal required TeamColor Color { get; init; }
    internal required TeamSide Side { get; init; }

    internal required Vision.FilteredFrame World { get; init; }
    internal required Referee.State Referee { get; init; }
    internal required FieldSize Field { get; init; }

    internal required Timer Timer { get; init; }

    // TODO: robots
}

internal static class Context
{
    /// Thread-local soccer context for the current AI instance.
    /// Must be set before using any soccer logic on this thread.
    internal static AsyncLocal<ContextData> Data { get; set; } = null!;

    internal static TeamColor Color => Data.Value!.Color;
    internal static TeamSide Side => Data.Value!.Side;

    internal static Vision.FilteredFrame World => Data.Value!.World;
    internal static Referee.State Referee => Data.Value!.Referee;
    internal static FieldSize Field => Data.Value!.Field;

    internal static Timer Timer => Data.Value!.Timer;
}