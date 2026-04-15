using System.Numerics;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics.Fsm;

public class Fsm<TState>(TState? initialState)
    where TState : Enum
{
    record Transition(TState? From, TState? To, Func<bool> Condition);

    private readonly Dictionary<TState, IState<TState>> _states = [];
    private readonly List<Transition> _transitions = [];

    private bool _initialized;

    public IState<TState>? Current { get; private set; }

    public void AddTransition(TState? from, TState? to, Func<bool> condition)
        => _transitions.Add(new Transition(from, to, condition));

    public void AddState(IState<TState> state)
        => _states[state.Type] = state;

    public ISkill? Tick()
    {
        if (!_initialized)
        {
            TransitionTo(initialState);
            _initialized = true;
        }

        var transitionsCount = 0;
        bool transitioned;
        do
        {
            transitioned = false;

            foreach (var transition in _transitions)
            {
                var fromStateMatch =
                    transition.From == null ||
                    (Current != null && EqualityComparer<TState>.Default.Equals(transition.From, Current.Type));

                if (fromStateMatch && transition.Condition())
                {
                    TransitionTo(transition.To);
                    transitioned = true;
                    break;
                }
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
        if (state != null)
        {
            Assert.Contains(_states, state, $"State {state} not found in FSM.");
        }

        Current?.Exit();
        Current = state != null ? _states[state] : null;
        Current?.Enter();
    }
}
