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
    private readonly Dictionary<TState, TacticStateConfig<TState>> _states;

    public TState CurrentState => _currentState;

    public TacticStateMachine(TState initialState, Dictionary<TState, TacticStateConfig<TState>> states)
    {
        _currentState = initialState;
        _states = states;
    }

    public void Reset(TState state)
    {
        Log.ZLogDebug($"FSM Reset to: {state}");
        _currentState = state;
    }

    public ISkill Tick(Robot.Robot robot)
    {
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
                    Log.ZLogDebug($"FSM Transition: {_currentState} -> {transition.NextState}");
                    config.ExitAction?.Invoke(robot);
                    _currentState = transition.NextState;
                    
                    if (_states.TryGetValue(_currentState, out var nextConfig))
                    {
                        nextConfig.EnterAction?.Invoke(robot);
                    }
                    
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
}
