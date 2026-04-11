using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics.Fsm;

public class TacticStateMachineBuilder<TState> where TState : struct, Enum
{
    private readonly Dictionary<TState, TacticStateConfig<TState>> _states = new();
    private TState _initialState;
    private bool _initialStateSet = false;

    public TacticStateConfigBuilder<TState> Configure(TState state)
    {
        if (!_initialStateSet)
        {
            _initialState = state;
            _initialStateSet = true;
        }

        if (!_states.TryGetValue(state, out var config))
        {
            config = new TacticStateConfig<TState>();
            _states[state] = config;
        }

        return new TacticStateConfigBuilder<TState>(config, this);
    }

    public TacticStateMachine<TState> Build()
    {
        return new TacticStateMachine<TState>(_initialState, _states);
    }
}

public class TacticStateConfig<TState> where TState : struct, Enum
{
    public Func<Robot.Robot, ISkill> TickAction { get; set; } = _ => new Halt();
    public Action<Robot.Robot>? EnterAction { get; set; }
    public Action<Robot.Robot>? ExitAction { get; set; }
    public List<StateTransition<TState>> Transitions { get; } = new();
}

public class StateTransition<TState> where TState : struct, Enum
{
    public TState NextState { get; set; }
    public Func<Robot.Robot, bool> Condition { get; set; } = _ => true;
}

public class TacticStateConfigBuilder<TState> where TState : struct, Enum
{
    private readonly TacticStateConfig<TState> _config;
    private readonly TacticStateMachineBuilder<TState> _parent;

    public TacticStateConfigBuilder(TacticStateConfig<TState> config, TacticStateMachineBuilder<TState> parent)
    {
        _config = config;
        _parent = parent;
    }

    public TacticStateConfigBuilder<TState> OnTick(Func<Robot.Robot, ISkill> action)
    {
        _config.TickAction = action;
        return this;
    }

    public TacticStateConfigBuilder<TState> OnEnter(Action<Robot.Robot> action)
    {
        _config.EnterAction = action;
        return this;
    }

    public TacticStateConfigBuilder<TState> OnExit(Action<Robot.Robot> action)
    {
        _config.ExitAction = action;
        return this;
    }

    public TransitionBuilder<TState> TransitionTo(TState nextState)
    {
        var transition = new StateTransition<TState> { NextState = nextState };
        _config.Transitions.Add(transition);
        return new TransitionBuilder<TState>(transition, this);
    }

    public TacticStateConfigBuilder<TState> Configure(TState state)
    {
        return _parent.Configure(state);
    }

    public TacticStateMachine<TState> Build()
    {
        return _parent.Build();
    }
}

public class TransitionBuilder<TState> where TState : struct, Enum
{
    private readonly StateTransition<TState> _transition;
    private readonly TacticStateConfigBuilder<TState> _parent;

    public TransitionBuilder(StateTransition<TState> transition, TacticStateConfigBuilder<TState> parent)
    {
        _transition = transition;
        _parent = parent;
    }

    public TacticStateConfigBuilder<TState> When(Func<Robot.Robot, bool> condition)
    {
        _transition.Condition = condition;
        return _parent;
    }
}
