using Tyr.Common.Data;
using Tyr.Common.Data.Referee;
using Gc = Tyr.Common.Data.Ssl.Gc;
using Tracker = Tyr.Common.Data.Ssl.Vision.Tracker;
using Vector3 = System.Numerics.Vector3;

namespace Tyr.Tests.Referee;

public class RefereeTests
{
    private static Tracker.Frame FrameAt(Vector3 pos) => new()
    {
        Balls = [new Tracker.Ball { Position = pos }]
    };

    private static void SimulateBallMovement(ref Tyr.Referee.Referee referee, float distance, int frames)
    {
        for (int i = 1; i <= frames; i++)
        {
            referee.Process(FrameAt(new Vector3(distance, 0, 0)), null);
        }
    }

    [Theory]
    [InlineData(200f, 10, GameState.Running)] // Expected to transition
    [InlineData(45f, 10, GameState.Kickoff)] // Expected to stay in Kickoff
    public void Referee_BallMovementAffectsStateTransition(float distance, int frames, GameState expectedState)
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero),
            new Gc.Referee { Command = Gc.Command.PrepareKickoffBlue, CommandCounter = 1 });
        referee.Process(null,
            new Gc.Referee
            {
                Command = Gc.Command.NormalStart, 
                CommandCounter = 2,
                CurrentActionTimeRemainingMicroseconds = (long)(10 * 1e6)
            });

        SimulateBallMovement(ref referee, distance, frames);

        Assert.Equal(expectedState, referee.State.GameState);
    }

    [Fact]
    public void Referee_EntersHaltState_WhenHaltCommandReceived()
    {
        var referee = new Tyr.Referee.Referee();

        var changed = referee.Process(FrameAt(Vector3.Zero),
            new Gc.Referee { Command = Gc.Command.Halt, CommandCounter = 1 });

        Assert.True(changed);
        Assert.Equal(GameState.Halt, referee.State.GameState);
    }

    [Fact]
    public void Referee_SetsReadyTrue_WhenNormalStartAfterKickoff()
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero),
            new Gc.Referee { Command = Gc.Command.PrepareKickoffBlue, CommandCounter = 1 });
        var changed = referee.Process(null, new Gc.Referee { Command = Gc.Command.NormalStart, CommandCounter = 2 });

        Assert.True(changed);
        Assert.True(referee.State.Ready);
    }

    [Fact]
    public void Referee_IgnoresRepeatedCommands_WithSameCommandCounter()
    {
        var referee = new Tyr.Referee.Referee();
        var cmd = new Gc.Referee { Command = Gc.Command.Stop, CommandCounter = 1 };

        var changed1 = referee.Process(FrameAt(Vector3.Zero), cmd);
        var changed2 = referee.Process(null, cmd);

        Assert.True(changed1);
        Assert.False(changed2);
    }

    [Fact]
    public void Referee_SetsTeamColorFromCommand()
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero),
            new Gc.Referee { Command = Gc.Command.PrepareKickoffYellow, CommandCounter = 1 });

        Assert.Equal(TeamColor.Yellow, referee.State.Color);
    }

    [Fact]
    public void Referee_PublishesStopState_WhenStopCommandReceived()
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero), new Gc.Referee { Command = Gc.Command.Stop, CommandCounter = 1 });

        Assert.Equal(GameState.Stop, referee.State.GameState);
    }

    [Fact]
    public void Referee_PublishesTimeoutState_WhenTimeoutCommandReceived()
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero), new Gc.Referee { Command = Gc.Command.TimeoutBlue, CommandCounter = 1 });

        Assert.Equal(GameState.Timeout, referee.State.GameState);
    }

    [Fact]
    public void Referee_EntersBallPlacement_WhenBallPlacementCommandReceived()
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero),
            new Gc.Referee { Command = Gc.Command.BallPlacementBlue, CommandCounter = 1 });

        Assert.Equal(GameState.BallPlacement, referee.State.GameState);
    }

    [Fact]
    public void Referee_KeepsReadyFalse_UntilNormalStart()
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero),
            new Gc.Referee { Command = Gc.Command.PreparePenaltyBlue, CommandCounter = 1 });

        Assert.False(referee.State.Ready);
    }

    [Fact]
    public void Referee_PublishesPenaltyState_WhenPenaltyCommandReceived()
    {
        var referee = new Tyr.Referee.Referee();

        referee.Process(FrameAt(Vector3.Zero),
            new Gc.Referee { Command = Gc.Command.PreparePenaltyBlue, CommandCounter = 1 });

        Assert.Equal(GameState.Penalty, referee.State.GameState);
    }
}