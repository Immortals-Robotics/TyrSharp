using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics.Fsm;

public class Fsm<TState>(TState? initialState)
    where TState : Enum
{
    record Transition(TState? From, TState? To, Func<bool> Condition);
    
    private readonly Dictionary<TState, IState<TState>> _states = [];
    private readonly List<Transition> _transitions = [];

    private bool _initialized;
    
    private IState<TState>? _current;

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
        var transitioned = false;
        do
        {
            transitioned = false;
            
            foreach (var transition in _transitions)
            {
                var fromStateMatch =
                    transition.From == null ||
                    (_current != null && EqualityComparer<TState>.Default.Equals(transition.From, _current.Type));

                if (fromStateMatch && transition.Condition())
                {
                    TransitionTo(transition.To);
                    transitioned = true;
                    break;
                }
            }
        } while(transitioned && ++transitionsCount < 10);

        return _current?.Tick();
    }
    
    private void TransitionTo(TState? state)
    {
        if (state != null)
        {
            Assert.Contains(_states, state, $"State {state} not found in FSM.");
        }

        _current?.Exit();
        _current = state != null ? _states[state] : null;
        _current?.Enter();
    }
}