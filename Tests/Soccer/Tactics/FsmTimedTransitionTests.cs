using System.Numerics;
using Tyr.Common.Data;
using Tyr.Common.Data.Ssl.Vision.Geometry;
using Tyr.Common.Referee.Data;
using Tyr.Common.Time;
using Tyr.Common.Vision.Data;
using Tyr.Soccer;
using Tyr.Soccer.Knowledge;
using Tyr.Soccer.Tactics.Fsm;
using SslReferee = Tyr.Common.Data.Ssl.Gc.Referee;
using TyrTimer = Tyr.Common.Time.Timer;

namespace Tyr.Tests.Soccer.Tactics;

public sealed class FsmTimedTransitionTests
{
    [Fact]
    public void ImmediateTransition_FiresOnFirstTick_WhenHoldTimeIsZero()
    {
        using var context = new SoccerContextScope();
        var fsm = CreateFsm();

        fsm.AddTransition(TestState.A, TestState.B, () => true);

        fsm.Tick();

        Assert.Equal(TestState.B, fsm.Current?.Type);
    }

    [Fact]
    public void TimedTransition_WaitsUntilHoldDurationElapses()
    {
        using var context = new SoccerContextScope();
        var fsm = CreateFsm();

        fsm.AddTransition(TestState.A, TestState.B, () => true, DeltaTime.FromMilliseconds(50));

        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(49));
        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(50));
        fsm.Tick();
        Assert.Equal(TestState.B, fsm.Current?.Type);
    }

    [Fact]
    public void TimedTransition_ResetsWhenConditionTurnsFalse()
    {
        using var context = new SoccerContextScope();
        var fsm = CreateFsm();
        var condition = true;

        fsm.AddTransition(TestState.A, TestState.B, () => condition, DeltaTime.FromMilliseconds(50));

        fsm.Tick();
        context.SetTime(DeltaTime.FromMilliseconds(30));
        fsm.Tick();

        condition = false;
        context.SetTime(DeltaTime.FromMilliseconds(40));
        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        condition = true;
        context.SetTime(DeltaTime.FromMilliseconds(89));
        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(138));
        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(139));
        fsm.Tick();
        Assert.Equal(TestState.B, fsm.Current?.Type);
    }

    [Fact]
    public void TimedTransition_ResetsWhenLeavingAndReEnteringFromState()
    {
        using var context = new SoccerContextScope();
        var fsm = CreateFsm();
        var holdCondition = true;
        var switchAway = false;
        var returnToA = false;

        fsm.AddTransition(TestState.A, TestState.C, () => switchAway);
        fsm.AddTransition(TestState.A, TestState.B, () => holdCondition, DeltaTime.FromMilliseconds(50));
        fsm.AddTransition(TestState.C, TestState.A, () => returnToA);

        fsm.Tick();
        context.SetTime(DeltaTime.FromMilliseconds(30));
        fsm.Tick();

        switchAway = true;
        context.SetTime(DeltaTime.FromMilliseconds(40));
        fsm.Tick();
        Assert.Equal(TestState.C, fsm.Current?.Type);

        switchAway = false;
        returnToA = true;
        context.SetTime(DeltaTime.FromMilliseconds(41));
        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(90));
        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(91));
        fsm.Tick();
        Assert.Equal(TestState.B, fsm.Current?.Type);
    }

    [Fact]
    public void TimedTransition_WithNullFromState_UsesSameHoldSemantics()
    {
        using var context = new SoccerContextScope();
        var fsm = CreateFsm();

        fsm.AddTransition((TestState?)null, TestState.B, () => true, DeltaTime.FromMilliseconds(50));

        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(49));
        fsm.Tick();
        Assert.Equal(TestState.A, fsm.Current?.Type);

        context.SetTime(DeltaTime.FromMilliseconds(50));
        fsm.Tick();
        Assert.Equal(TestState.B, fsm.Current?.Type);
    }

    private static Fsm<TestState> CreateFsm()
    {
        var fsm = new Fsm<TestState>(TestState.A);
        fsm.AddState(new TestStateImpl(TestState.A));
        fsm.AddState(new TestStateImpl(TestState.B));
        fsm.AddState(new TestStateImpl(TestState.C));
        return fsm;
    }

    private enum TestState
    {
        A,
        B,
        C
    }

    private sealed class TestStateImpl(TestState type) : IState<TestState>
    {
        public TestState Type { get; } = type;

        public void Enter()
        {
        }

        public Tyr.Soccer.Skills.ISkill? Tick() => null;

        public void Exit()
        {
        }
    }

    private sealed class SoccerContextScope : IDisposable
    {
        private readonly ContextData? _previous;

        public SoccerContextScope()
        {
            _previous = Context.Data.Value;
            Context.Data.Value = new ContextData
            {
                Color = TeamColor.Blue,
                VisionTime = Timestamp.Zero - Ai.VisionPredictionTime,
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
            };
        }

        public void SetTime(DeltaTime time)
        {
            Context.Data.Value = Context.Data.Value! with
            {
                VisionTime = Timestamp.Zero + time - Ai.VisionPredictionTime
            };
        }

        public void Dispose()
        {
            Context.Data.Value = _previous!;
        }
    }
}
