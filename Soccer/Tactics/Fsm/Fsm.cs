using System.Numerics;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Common.Time;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics.Fsm;

public class Fsm<TState>(TState? initialState)
    where TState : struct, Enum
{
    private sealed class Transition(TState? from, TState? to, Func<bool> condition, DeltaTime requiredTrueFor)
    {
        public TState? From { get; } = from;
        public TState? To { get; } = to;
        public Func<bool> Condition { get; } = condition;
        public DeltaTime RequiredTrueFor { get; } = requiredTrueFor;
        public Timestamp? TrueSince { get; set; }
    }

    private readonly Dictionary<TState, IState<TState>> _states = [];
    private readonly List<Transition> _transitions = [];

    private bool _initialized;

    public IState<TState>? Current { get; private set; }

    public void AddTransition(TState? from, TState? to, Func<bool> condition, DeltaTime requiredTrueFor = default)
        => _transitions.Add(new Transition(from, to, condition, requiredTrueFor));

    public void AddState(IState<TState> state)
        => _states[state.Type] = state;

    public ISkill? Tick()
    {
        if (!_initialized)
        {
            TransitionTo(initialState);
            _initialized = true;
        }

        var now = Context.Time;
        var transitionsCount = 0;
        bool transitioned;
        do
        {
            transitioned = false;

            foreach (var transition in _transitions)
            {
                var fromStateMatch =
                    !transition.From.HasValue ||
                    (Current != null && EqualityComparer<TState>.Default.Equals(transition.From.Value, Current.Type));

                if (!fromStateMatch)
                {
                    transition.TrueSince = null;
                    continue;
                }

                if (!transition.Condition())
                {
                    transition.TrueSince = null;
                    continue;
                }

                if (transition.RequiredTrueFor > DeltaTime.Zero)
                {
                    transition.TrueSince ??= now;

                    if (now - transition.TrueSince.Value < transition.RequiredTrueFor)
                    {
                        continue;
                    }
                }

                TransitionTo(transition.To);
                transitioned = true;
                break;
            }
        } while (transitioned && ++transitionsCount < 10);

        return Current?.Tick();
    }

    public void DrawDebug(Robot.Robot robot)
    {
        Draw.DrawText($"State: {Current?.Type.ToString() ?? "None"}",
            robot.Position + new Vector2(0, 300f), 100f, Color.Neutral400, TextAlignment.BottomCenter);
    }

    private void TransitionTo(TState? state)
    {
        if (state.HasValue)
        {
            Assert.Contains(_states, state.Value, $"State {state} not found in FSM.");
        }

        Current?.Exit();
        Current = state.HasValue ? _states[state.Value] : null;
        Current?.Enter();
    }
}
