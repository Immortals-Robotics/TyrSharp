using Tyr.Soccer.Skills;

namespace Tyr.Soccer.Tactics.Fsm;

public interface IState<out TState> where TState : struct, Enum
{
    TState Type { get; }
    
    void Enter();
    ISkill? Tick();
    void Exit();
}
