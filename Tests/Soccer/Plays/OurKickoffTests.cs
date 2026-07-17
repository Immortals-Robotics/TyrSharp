using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Extensions;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Soccer;
using Tyr.Soccer.Plays;
using Tyr.Soccer.Role;
using Vision = Tyr.Common.Vision.Data;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using Xunit;

namespace Tests.Soccer.Plays;

/// Rule-level invariants only — these must survive any retuning of the
/// kickoff formation. Exact positions, role counts, and kick powers are
/// deliberately not asserted.
public sealed class OurKickoffTests : IDisposable
{
    private readonly ContextData? _previous = Context.Data.Value;

    [Theory]
    [InlineData(false)] // we play on the left half
    [InlineData(true)] // we play on the right half
    public void OurKickoff_PlacesAllWaitersInsideOurHalf(bool blueOnPositiveHalf)
    {
        SetupKickoffContext(blueOnPositiveHalf, ready: false);

        var formation = new OurKickoff().Tick();

        var waiters = formation.RequiredRoles.Concat(formation.DesiredRoles).OfType<Waiter>().ToList();
        Assert.NotEmpty(waiters);

        foreach (var waiter in waiters)
        {
            // Rule: at kickoff, robots must be positioned inside our own half.
            Assert.True(Context.SideSign * waiter.Target.X > 0,
                $"Waiter target {waiter.Target} is not inside our half (SideSign {Context.SideSign})");
            Assert.True(
                MathF.Abs(waiter.Target.X) <= Context.Field.Width &&
                MathF.Abs(waiter.Target.Y) <= Context.Field.Height,
                $"Waiter target {waiter.Target} is outside the field");
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OurKickoff_DoesNotKickBeforeReadySignal(bool blueOnPositiveHalf)
    {
        SetupKickoffContext(blueOnPositiveHalf, ready: false);

        var formation = new OurKickoff().Tick();

        // Rule: the kickoff must not be taken before the ready signal.
        var kicker = Assert.IsType<CircleBall>(formation.RequiredRoles.Single(r => r is CircleBall));
        Assert.False(kicker.CanKick);
        Assert.Equal(0f, kicker.ShootPower);
    }

    private static void SetupKickoffContext(bool blueOnPositiveHalf, bool ready)
    {
        var referee = new State
        {
            GameState = GameState.Kickoff,
            Color = TeamColor.Blue,
            Ready = ready,
            Timestamp = Timestamp.Zero,
            Gc = new SslReferee { BlueTeamOnPositiveHalf = blueOnPositiveHalf }
        };

        Context.Data.Value = new ContextData
        {
            Color = TeamColor.Blue,
            VisionTime = Timestamp.FromSeconds(1),
            Ball = new Vision.FilteredBall { State = new Vision.BallState { Position3D = Vector2.Zero.Xyz() } },
            OppRobots = [],
            OwnRobots = [],
            Referee = referee,
            Field = FieldSize.DivisionB,
            Timer = new Tyr.Common.Time.Timer(),
            Knowledge = null!,
            RoleAssignment = null!
        };
    }

    public void Dispose()
    {
        Context.Data.Value = _previous!;
    }
}
