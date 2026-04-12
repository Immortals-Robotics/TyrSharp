using System.Drawing;
using System.Numerics;
using Tyr.Common.Debug;
using Tyr.Common.Debug.Drawing;
using Tyr.Common.Debug.Drawing.Drawables;
using Tyr.Soccer.Skills;
using Color = Tyr.Common.Debug.Drawing.Color;

namespace Tyr.Soccer.Tactics.Fsm;

public class TacticStateMachine<TState> where TState : struct, Enum
{
    private TState _currentState;
    private TState? _pendingPreviousState;
    private bool _currentStateEntered;
    private readonly Dictionary<TState, TacticStateConfig<TState>> _states;

    public TState CurrentState => _currentState;

    public TacticStateMachine(TState initialState, Dictionary<TState, TacticStateConfig<TState>> states)
    {
        _currentState = initialState;
        _currentStateEntered = false;
        _states = states;
    }

    public void Reset(TState state)
    {
        Log.ZLogDebug($"FSM Reset to: {state}");

        if (_currentStateEntered && !EqualityComparer<TState>.Default.Equals(_currentState, state))
        {
            _pendingPreviousState = _currentState;
        }

        _currentState = state;
        _currentStateEntered = false;
    }

    public ISkill Tick(Robot.Robot robot)
    {
        if (!TryEnterCurrentState(robot))
        {
            return new Halt();
        }

        int transitionsCount = 0;
        
        while (transitionsCount < 10)
        {
            if (!_states.TryGetValue(_currentState, out var config))
            {
                Log.ZLogWarning($"State {_currentState} not found in state machine.");
                return new Halt();
            }

            bool transitioned = false;
            foreach (var transition in config.Transitions)
            {
                if (transition.Condition(robot))
                {
                    if (!_states.TryGetValue(transition.NextState, out var nextConfig))
                    {
                        Log.ZLogWarning($"Transition target {transition.NextState} not found in state machine.");
                        return new Halt();
                    }

                    Log.ZLogDebug($"FSM Transition: {_currentState} -> {transition.NextState}");
                    config.ExitAction?.Invoke(robot);
                    _currentState = transition.NextState;
                    nextConfig.EnterAction?.Invoke(robot);
                    _currentStateEntered = true;
                    
                    transitioned = true;
                    break;
                }
            }
            
            if (!transitioned)
            {
                if (transitionsCount == 0)
                {
                    Draw.DrawText( $"FSM State: {_currentState}", 
                        robot.Position + new Vector2(0.0f, 100f), 100f,
                        Color.Stone300, TextAlignment.BottomCenter);
                }
                return config.TickAction(robot);
            }
            
            transitionsCount++;
        }
        
        Log.ZLogWarning($"FSM trapped in transition loop. Forced Halt. State: {_currentState}");
        return new Halt();
    }

    private bool TryEnterCurrentState(Robot.Robot robot)
    {
        if (_currentStateEntered)
        {
            return true;
        }

        if (!_states.TryGetValue(_currentState, out var currentConfig))
        {
            Log.ZLogWarning($"State {_currentState} not found in state machine.");
            return false;
        }

        if (_pendingPreviousState is { } previousState &&
            _states.TryGetValue(previousState, out var previousConfig))
        {
            previousConfig.ExitAction?.Invoke(robot);
        }

        _pendingPreviousState = null;
        currentConfig.EnterAction?.Invoke(robot);
        _currentStateEntered = true;
        return true;
    }
}
